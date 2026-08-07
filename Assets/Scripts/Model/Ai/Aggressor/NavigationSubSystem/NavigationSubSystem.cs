using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BoardTools;
using Movement;
using Players;
using Ship;
using UnityEngine;
using ActionsList;
using Obstacles;

namespace AI.Aggressor
{
    public static class NavigationSubSystem
    {
        private static GenericPlayer CurrentPlayer;

        private static Dictionary<PlayerNo, VirtualBoard> VirtualBoards;

        // Cache dei template Straight1 usati per la sola proiezione (lettura posizione finale)
        // durante la valutazione AI del Barrel Roll. Creati una sola volta, mai distrutti,
        // sempre disattivati: evitano Instantiate/Destroy ripetuti (costosi) ad ogni singola
        // valutazione. Due istanze separate (Left/Right) perché ManeuverDirection è impostata
        // nel costruttore e influenza la rotazione applicata (directionFix).
        private static ManeuverTemplate SharedBrScanTemplateLeft;
        private static ManeuverTemplate SharedBrScanTemplateRight;

        private static ManeuverTemplate GetSharedBrScanTemplate(ManeuverDirection direction)
        {
            if (direction == ManeuverDirection.Left)
            {
                if (SharedBrScanTemplateLeft == null || !SharedBrScanTemplateLeft.IsAlive)
                    SharedBrScanTemplateLeft = new ManeuverTemplate(ManeuverBearing.Straight, ManeuverDirection.Left, ManeuverSpeed.Speed1);
                return SharedBrScanTemplateLeft;
            }
            else
            {
                if (SharedBrScanTemplateRight == null || !SharedBrScanTemplateRight.IsAlive)
                    SharedBrScanTemplateRight = new ManeuverTemplate(ManeuverBearing.Straight, ManeuverDirection.Right, ManeuverSpeed.Speed1);
                return SharedBrScanTemplateRight;
            }
        }
        private static VirtualBoard VirtualBoard
        {
            get { return VirtualBoards[CurrentPlayer.PlayerNo]; }
            set { VirtualBoards[CurrentPlayer.PlayerNo] = value; }
        }

        private static int OrderOfActivation;

        private static NavigationResult CurrentNavigationResult;

        public static void CalculateNavigation(Action callback)
        {
            CurrentPlayer = Roster.GetPlayer(Phases.CurrentSubPhase.RequiredPlayer);

            ConfigureVirtualBoards();

            GameManagerScript.Instance.StartCoroutine
            (
                StartCalculations(callback)
            );
        }

        private static IEnumerator StartCalculations(Action callback)
        {
            ShowCalculationsStart();

            SwitchEnemyShipsToSimpleVirtualPositions();
            yield return PredictAllFinalPositionsOfOwnShips();

            RestoreRealBoard();

            List<GenericShip> orderOfActivation = GenerateOrderOfActivation();

            yield return FindBestManeuversForShips(orderOfActivation);

            RestoreRealBoard();
            ShowCalculationsEnd();

            callback();
        }

        private static void SwitchEnemyShipsToSimpleVirtualPositions()
        {
            foreach (GenericShip ship in CurrentPlayer.EnemyShips.Values)
            {
                PredictSimpleFinalPositionOfEnemyShip(ship);
            }
        }

        private static void PredictSimpleFinalPositionOfEnemyShip(GenericShip ship)
        {
            Selection.ThisShip = ship;

            GenericMovement savedMovement = ship.AssignedManeuver;

            // Decide what maneuvers to use as temporary
            string temporyManeuver = (ship.State.IsIonized) ? "1.F.S" : "2.F.S";
            bool isTemporaryManeuverAdded = false;
            if (!ship.HasManeuver(temporyManeuver))
            {
                isTemporaryManeuverAdded = true;
                ship.Maneuvers.Add(temporyManeuver, MovementComplexity.Easy);
            }
            GenericMovement movement = ShipMovementScript.MovementFromString(temporyManeuver);

            // Check maneuver
            ship.SetAssignedManeuver(movement, isSilent: true);
            movement.Initialize();
            movement.IsSimple = true;

            MovementPrediction prediction = new MovementPrediction(ship, movement);
            prediction.CalculateOnlyFinalPositionIgnoringCollisions();

            if (isTemporaryManeuverAdded)
            {
                ship.Maneuvers.Remove(temporyManeuver);
            }

            if (savedMovement != null)
            {
                ship.SetAssignedManeuver(savedMovement, isSilent: true);
            }
            else
            {
                ship.ClearAssignedManeuver();
            }

            VirtualBoard.SetVirtualPositionInfo(ship, prediction.FinalPositionInfo, temporyManeuver);
        }

        private static IEnumerator PredictAllFinalPositionsOfOwnShips()
        {
            foreach (GenericShip ship in CurrentPlayer.EnemyShips.Values)
            {
                VirtualBoard.SwitchToVirtualPosition(ship);
            }

            foreach (GenericShip ship in CurrentPlayer.Ships.Values)
            {
                yield return PredictFinalPosionsOfOwnShip(ship);
            }

            foreach (GenericShip ship in CurrentPlayer.EnemyShips.Values)
            {
                VirtualBoard.SwitchToRealPosition(ship);
            }
        }

        private static IEnumerator PredictFinalPosionsOfOwnShip(GenericShip ship)
        {
            Selection.ChangeActiveShip(ship);
            VirtualBoard.SwitchToRealPosition(ship);

            Dictionary<string, NavigationResult> navigationResults = new Dictionary<string, NavigationResult>();
            foreach (var maneuver in ship.GetManeuvers())
            {
                GenericMovement movement = ShipMovementScript.MovementFromString(maneuver.Key);
                ship.SetAssignedManeuver(movement, isSilent: true);
                movement.Initialize();
                movement.IsSimple = true;

                MovementPrediction prediction = new MovementPrediction(ship, movement);
                prediction.CalculateOnlyFinalPositionIgnoringCollisions();

                VirtualBoard.SetVirtualPositionInfo(ship, prediction.FinalPositionInfo, prediction.CurrentMovement.ToString());
                VirtualBoard.SwitchToVirtualPosition(ship);

                float minDistanceToEnemyShip, minDistanceToNearestEnemyInShotRange, minAngle;
                int enemiesInShotRange, enemiesTargetingThisShip;
                ProcessHeavyGeometryCalculations(ship, out minDistanceToEnemyShip, out minDistanceToNearestEnemyInShotRange, out minAngle, out enemiesInShotRange, out enemiesTargetingThisShip);


                NavigationResult result = new NavigationResult()
                {
                    movement = prediction.CurrentMovement,
                    distanceToNearestEnemy = minDistanceToEnemyShip,
                    distanceToNearestEnemyInShotRange = minDistanceToNearestEnemyInShotRange,
                    angleToNearestEnemy = minAngle,
                    enemiesInShotRange = enemiesInShotRange,
                    isBumped = prediction.IsBumped,
                    isLandedOnObstacle = prediction.IsLandedOnAsteroid,
                    isOffTheBoard = prediction.IsOffTheBoard,
                    isEscaped = determineEscaped(ship.EscapeEdge, prediction),
                    FinalPositionInfo = prediction.FinalPositionInfo,
                    isFleeing = ship.IsFleeing
                };
                result.CalculatePriority();

                navigationResults.Add(maneuver.Key, result);

                VirtualBoard.SwitchToRealPosition(ship);

                yield return true;
            }

            ship.ClearAssignedManeuver();
            VirtualBoard.UpdateNavigationResults(ship, navigationResults);
            Selection.DeselectThisShip();
        }

        private static List<GenericShip> GenerateOrderOfActivation()
        {
            OrderOfActivation = 0;

            List<GenericShip> orderOfActivation = new List<GenericShip>();

            List<GenericShip> AllShips = new List<GenericShip>(Roster.AllShips.Values.ToList());

            while (AllShips.Count > 0)
            {
                int lowestInitiative = AllShips.Min(n => n.State.Initiative);

                GenericShip shipToActivate = AllShips
                    .Where(n => n.State.Initiative == lowestInitiative)
                    .OrderBy(n => GetMinDistanceToEnemyShip(n))
                    .OrderByDescending(n => n.Owner.PlayerNo == Phases.PlayerWithInitiative)
                    .First();

                orderOfActivation.Add(shipToActivate);
                AllShips.Remove(shipToActivate);
            }

            if (DebugManager.DebugAiNavigation)
            {
                string orderOfActivationText = "";
                foreach (GenericShip ship in orderOfActivation)
                {
                    orderOfActivationText += (ship.ShipId + ", ");
                }
            }

            return orderOfActivation;
        }

        private static IEnumerator FindBestManeuversForShips(List<GenericShip> orderOfActivation)
        {
            while (orderOfActivation.Count > 0)
            {
                SetVirtualPositionsForShipsWithPreviousActivations(orderOfActivation);

                GenericShip ship = orderOfActivation.First();
                orderOfActivation.Remove(ship);

                if (ship.Owner.PlayerNo == CurrentPlayer.PlayerNo)
                {
                    yield return FindBestManeuver(ship);
                }
                else
                {
                    yield return PredictCollisionDetectionOfEnemyShip(ship);
                }
            }
        }

        private static IEnumerator FindBestManeuver(GenericShip ship)
        {
            Selection.ChangeActiveShip(ship);

            int bestPriority = int.MinValue;
            KeyValuePair<string, NavigationResult> maneuverToCheck = new KeyValuePair<string, NavigationResult>();

            do
            {
                VirtualBoard.SwitchToRealPosition(ship);

                bestPriority = VirtualBoard.Ships[ship].NavigationResults.Max(n => n.Value.Priority);
                maneuverToCheck = VirtualBoard.Ships[ship].NavigationResults.Where(n => n.Value.Priority == bestPriority).First();

                GenericMovement movement = ShipMovementScript.MovementFromString(maneuverToCheck.Key);

                ship.SetAssignedManeuver(movement, isSilent: true);
                movement.Initialize();
                movement.IsSimple = true;

                MovementPrediction prediction = new MovementPrediction(ship, movement);
                yield return prediction.CalculateMovementPredicition();

                VirtualBoard.SetVirtualPositionInfo(ship, prediction.FinalPositionInfo, prediction.CurrentMovement.ToString());
                VirtualBoard.SwitchToVirtualPosition(ship);

                CurrentNavigationResult = new NavigationResult()
                {
                    movement = prediction.CurrentMovement,
                    isBumped = prediction.IsBumped,
                    isLandedOnObstacle = prediction.IsLandedOnAsteroid,
                    obstaclesHit = prediction.AsteroidsHit.Count,
                    isOffTheBoard = prediction.IsOffTheBoard,
                    isEscaped = determineEscaped(ship.EscapeEdge, prediction),
                    minesHit = prediction.MinesHit.Count,
                    isOffTheBoardNextTurn = false,
                    isHitAsteroidNextTurn = false,
                    FinalPositionInfo = prediction.FinalPositionInfo,
                    isFleeing = ship.IsFleeing
                };

                foreach (GenericShip enemyShip in CurrentPlayer.EnemyShips.Values)
                {
                    VirtualBoard.SwitchToVirtualPosition(enemyShip);
                }

                if (!prediction.IsOffTheBoard)
                {
                    yield return CheckNextTurnRecursive(ship);

                    float minDistanceToEnemyShip, minDistanceToNearestEnemyInShotRange, minAngle;
                    int enemiesInShotRange, enemiesTargetingThisShip;

                    ProcessHeavyGeometryCalculations(ship, out minDistanceToEnemyShip, out minDistanceToNearestEnemyInShotRange, out minAngle, out enemiesInShotRange, out enemiesTargetingThisShip);

                    CurrentNavigationResult.distanceToNearestEnemy = minDistanceToEnemyShip;
                    CurrentNavigationResult.distanceToNearestEnemyInShotRange = minDistanceToNearestEnemyInShotRange;
                    CurrentNavigationResult.angleToNearestEnemy = minAngle;
                    CurrentNavigationResult.enemiesInShotRange = enemiesInShotRange;
                }

                CurrentNavigationResult.CalculatePriority();

                VirtualBoard.Ships[ship].NavigationResults[maneuverToCheck.Key] = CurrentNavigationResult;

                bestPriority = VirtualBoard.Ships[ship].NavigationResults.Max(n => n.Value.Priority);

                VirtualBoard.SwitchToRealPosition(ship);

                maneuverToCheck = VirtualBoard.Ships[ship].NavigationResults.First(n => n.Key == maneuverToCheck.Key);

                foreach (GenericShip enemyShip in CurrentPlayer.EnemyShips.Values)
                {
                    VirtualBoard.SwitchToRealPosition(enemyShip);
                }

            } while (maneuverToCheck.Value.Priority != bestPriority);

            VirtualBoard.Ships[ship].SetPlannedManeuverCode(maneuverToCheck.Key, ++OrderOfActivation);
            ship.ClearAssignedManeuver();
            Selection.DeselectThisShip();
        }

        private static bool determineEscaped(string escapeEdge, MovementPrediction prediction)
        {
            switch (escapeEdge)
            {
                case null:
                    return false;
                case "north":
                    return prediction.IsOffTheBoardNorth;
                case "south":
                    return prediction.IsOffTheBoardSouth;
                case "east":
                    return prediction.IsOffTheBoardEast;
                case "west":
                    return prediction.IsOffTheBoardWest;
                default:
                    return false;
            }
        }

        private static Vector3 determineEscapeEdge(string escapeEdge)
        {
            switch (escapeEdge)
            {
                case "north":
                    return Board.GetBoard().Find("OffTheBoardHolder").Find("OffTheBoardNorth").transform.position;
                case "south":
                    return Board.GetBoard().Find("OffTheBoardHolder").Find("OffTheBoardSouth").transform.position;
                case "east":
                    return Board.GetBoard().Find("OffTheBoardHolder").Find("OffTheBoardEast").transform.position;
                case "west":
                    return Board.GetBoard().Find("OffTheBoardHolder").Find("OffTheBoardWest").transform.position;
                default:
                    return Board.GetBoard().Find("OffTheBoardHolder").Find("OffTheBoardNorth").transform.position;
            }
        }

         private static void ProcessHeavyGeometryCalculations(GenericShip ship, out float minDistanceToEnemyShip, out float minDistanceToNearestEnemyInShotRange, out float minAngle, out int enemiesInShotRange, out int enemiesTargetingThisShip)
{
    // Inizializza output
    minDistanceToEnemyShip = float.MaxValue;
    minDistanceToNearestEnemyInShotRange = 0;
    minAngle = float.MaxValue;
    enemiesInShotRange = 0;
    enemiesTargetingThisShip = 0;

    // CONTROLLO 1: ship deve essere valido
    if (ship == null)
    {
        Debug.LogError("[NavigationSubSystem] ProcessHeavyGeometryCalculations: ship is null!");
        return;
    }

    // CONTROLLO 2: ship deve avere armi primarie
    if (ship.PrimaryWeapons == null || !ship.PrimaryWeapons.Any())
    {
        string shipName = (ship.PilotInfo != null) ? ship.PilotInfo.PilotName : "Unknown";
        Debug.LogWarning($"[NavigationSubSystem] Ship '{shipName}' has no primary weapons! Skipping geometry calculations.");
        return;
    }

    // CONTROLLO 3: ship deve avere un owner valido
    if (ship.Owner == null || ship.Owner.EnemyShips == null)
    {
        Debug.LogError($"[NavigationSubSystem] Ship '{ship.PilotInfo?.PilotName}' has no valid owner or enemy ships list!");
        return;
    }

    List<GenericShip> potentialTargets = ship.Owner.EnemyShips.Values.ToList();
    if (ship.StrikeTargets != null && ship.StrikeTargets.Count > 0)
    {
        potentialTargets = ship.StrikeTargets.Values.ToList();
    }

    // CONTROLLO 4: deve esserci almeno un bersaglio potenziale
    if (potentialTargets == null || potentialTargets.Count == 0)
    {
        Debug.LogWarning($"[NavigationSubSystem] Ship '{ship.PilotInfo?.PilotName}' has no potential targets!");
        return;
    }

    foreach (GenericShip enemyShip in potentialTargets)
    {
        // CONTROLLO 5: enemyShip deve essere valido
        if (enemyShip == null)
        {
            Debug.LogWarning("[NavigationSubSystem] Found null enemy ship in potential targets! Skipping.");
            continue;
        }

        // CONTROLLO 6: enemyShip deve avere armi primarie
        if (enemyShip.PrimaryWeapons == null || !enemyShip.PrimaryWeapons.Any())
        {
            string enemyName = (enemyShip.PilotInfo != null) ? enemyShip.PilotInfo.PilotName : "Unknown";
            Debug.LogWarning($"[NavigationSubSystem] Enemy ship '{enemyName}' has no primary weapons! Skipping this target.");
            continue;
        }

        // OTTIMIZZAZIONE: Uso del Pool con try/finally per garantire il rilascio
        DistanceInfo distInfo = null;
        ShotInfo shotInfo = null;
        ShotInfo shotInfoEnemy = null;

        try
        {
            distInfo = DistanceInfoPool.Get(ship, enemyShip);
            shotInfo = ShotInfoPool.Get(ship, enemyShip, ship.PrimaryWeapons.First());
            shotInfoEnemy = ShotInfoPool.Get(enemyShip, ship, enemyShip.PrimaryWeapons.First());

            // CONTROLLO 7: verifica che i pool abbiano restituito oggetti validi
            if (distInfo == null || shotInfo == null || shotInfoEnemy == null)
            {
                Debug.LogError($"[NavigationSubSystem] Pool returned null object! ship={ship.PilotInfo?.PilotName}, enemy={enemyShip.PilotInfo?.PilotName}");
                continue;
            }

            if (distInfo.MinDistance != null && distInfo.MinDistance.DistanceReal < minDistanceToEnemyShip)
            {
                minDistanceToEnemyShip = distInfo.MinDistance.DistanceReal;
            }

            if (shotInfo.IsShotAvailable)
            {
                enemiesInShotRange++;
                if (minDistanceToNearestEnemyInShotRange < shotInfo.DistanceReal)
                {
                    minDistanceToNearestEnemyInShotRange = shotInfo.DistanceReal;
                }
            }

            if (shotInfoEnemy.IsShotAvailable == true)
            {
                enemiesTargetingThisShip++;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NavigationSubSystem] Exception during pool operations for ship '{ship.PilotInfo?.PilotName}' vs '{enemyShip.PilotInfo?.PilotName}': {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            // Rilascio sicuro: controlla null prima di restituire al pool
            if (distInfo != null) DistanceInfoPool.Return(distInfo);
            if (shotInfo != null) ShotInfoPool.Return(shotInfo);
            if (shotInfoEnemy != null) ShotInfoPool.Return(shotInfoEnemy);
        }

        Vector3 forward = ship.GetFrontFacing();
        Vector3 toEnemyShip = enemyShip.GetCenter() - ship.GetCenter();
        float angle = Mathf.Abs(Vector3.SignedAngle(forward, toEnemyShip, Vector3.down));
        if (angle < minAngle) minAngle = angle;

        if (ship.IsFleeing)
        {
            Vector3 escapeEdge = determineEscapeEdge(ship.EscapeEdge);
            float DistanceToEscapeEdge = Vector3.Distance(ship.GetPosition(), escapeEdge);
            minDistanceToEnemyShip = Vector3.Distance(ship.GetPosition(), escapeEdge);
            minDistanceToNearestEnemyInShotRange = Vector3.Distance(ship.GetPosition(), escapeEdge);
            toEnemyShip = escapeEdge - ship.GetCenter();
            minAngle = Mathf.Abs(Vector3.SignedAngle(forward, toEnemyShip, Vector3.down));
            enemiesInShotRange = 0;
        }
    }
}

        private static void SetVirtualPositionsForShipsWithPreviousActivations(List<GenericShip> orderOfActivation)
        {
            foreach (GenericShip ship in Roster.AllShips.Values)
            {
                if (!orderOfActivation.Contains(ship))
                {
                    VirtualBoard.SwitchToVirtualPosition(ship);
                }
            }
        }

        private static IEnumerator PredictCollisionDetectionOfEnemyShip(GenericShip ship)
        {
            Selection.ThisShip = ship;

            GenericMovement savedMovement = ship.AssignedManeuver;

            // Decide what maneuvers to use as temporary
            string temporyManeuver = (ship.State.IsIonized) ? "1.F.S" : "2.F.S";
            bool isTemporaryManeuverAdded = false;
            if (!ship.HasManeuver(temporyManeuver))
            {
                isTemporaryManeuverAdded = true;
                ship.Maneuvers.Add(temporyManeuver, MovementComplexity.Easy);
            }
            GenericMovement movement = ShipMovementScript.MovementFromString(temporyManeuver);

            // Check maneuver
            ship.SetAssignedManeuver(movement, isSilent: true);
            movement.Initialize();
            movement.IsSimple = true;

            MovementPrediction prediction = new MovementPrediction(ship, movement);
            yield return prediction.CalculateMovementPredicition();

            if (isTemporaryManeuverAdded)
            {
                ship.Maneuvers.Remove(temporyManeuver);
            }

            if (savedMovement != null)
            {
                ship.SetAssignedManeuver(savedMovement, isSilent: true);
            }
            else
            {
                ship.ClearAssignedManeuver();
            }

            VirtualBoard.SetVirtualPositionInfo(ship, prediction.FinalPositionInfo, temporyManeuver);
        }

        private static IEnumerator CheckNextTurnRecursive(GenericShip ship)
        {
            VirtualBoard.RemoveCollisionsExcept(ship);

            bool HasAnyManeuverWithoutOffBoardFinish = false;
            bool HasAnyManeuverWithoutAsteroidCollision = false;

            foreach (string turnManeuver in ship.GetManeuvers().Keys)
            {
                GenericMovement movement = ShipMovementScript.MovementFromString(turnManeuver);

                ship.SetAssignedManeuver(movement, isSilent: true);
                movement.Initialize();
                movement.IsSimple = true;

                MovementPrediction prediction = new MovementPrediction(ship, movement);
                yield return prediction.CalculateMovementPredicition();

                if (!CurrentNavigationResult.isOffTheBoard || CurrentNavigationResult.isEscaped) HasAnyManeuverWithoutOffBoardFinish = true;
                if (CurrentNavigationResult.obstaclesHit == 0) HasAnyManeuverWithoutAsteroidCollision = true;
            }

            CurrentNavigationResult.isOffTheBoardNextTurn = !HasAnyManeuverWithoutOffBoardFinish;
            CurrentNavigationResult.isHitAsteroidNextTurn = !HasAnyManeuverWithoutAsteroidCollision;

            VirtualBoard.ReturnCollisionsExcept(ship);
        }

        private static List<string> GetShortestTurnManeuvers(GenericShip ship)
        {
            List<string> bestTurnManeuvers = new List<string>();

            ManeuverHolder bestTurnManeuver = ship.GetManeuverHolders()
                .Where(n =>
                    n.Bearing == ManeuverBearing.Turn
                    && n.Direction == ManeuverDirection.Left
                )
                .OrderBy(n => n.SpeedIntUnsigned)
                .FirstOrDefault();
            bestTurnManeuvers.Add(bestTurnManeuver.ToString());

            bestTurnManeuver = ship.GetManeuverHolders()
                .Where(n =>
                    n.Bearing == ManeuverBearing.Turn
                    && n.Direction == ManeuverDirection.Right
                )
                .OrderBy(n => n.SpeedIntUnsigned)
                .FirstOrDefault();
            bestTurnManeuvers.Add(bestTurnManeuver.ToString());

            return bestTurnManeuvers;
        }

        public static GenericShip GetNextShipWithoutAssignedManeuver()
        {
            return Roster.GetPlayer(Phases.CurrentSubPhase.RequiredPlayer).Ships.Values
                .Where(n => n.AssignedManeuver == null && !n.State.IsIonized && !n.State.IsDisabled)
                .OrderBy(n => VirtualBoard.Ships[n].OrderToActivate)
                .FirstOrDefault();
        }

        public static GenericShip GetNextShipWithoutFinishedManeuver()
        {
            return Roster.GetPlayer(Phases.CurrentSubPhase.RequiredPlayer).Ships.Values
                .Where(n => !n.IsManeuverPerformed)
                .OrderBy(n => VirtualBoard.Ships[n].OrderToActivate)
                .FirstOrDefault();
        }

        public static void AssignPlannedManeuver(Action callBack)
        {
            ShipMovementScript.SendAssignManeuverCommand(VirtualBoard.Ships[Selection.ThisShip].PlannedManeuverCode);
            GameManagerScript.Wait(0.2f, delegate { Selection.DeselectThisShip(); callBack(); });
        }

        // Low Priority

        private static void ConfigureVirtualBoards()
        {
            if (Phases.RoundCounter == 1) VirtualBoards = new Dictionary<PlayerNo, VirtualBoard>()
            {
                { PlayerNo.Player1, new VirtualBoard() },
                { PlayerNo.Player2, new VirtualBoard() }
            };

            VirtualBoard.Update();
        }

        private static void RestoreRealBoard()
        {
            VirtualBoard.RestoreBoard();
        }

        private static void ShowCalculationsStart()
        {
            Roster.ToggleCalculatingStatus(Phases.CurrentSubPhase.RequiredPlayer, true);
        }

        private static void ShowCalculationsEnd()
        {
            Roster.ToggleCalculatingStatus(Phases.CurrentSubPhase.RequiredPlayer, false);
        }

          private static float GetMinDistanceToEnemyShip(GenericShip ship)
{
    if (ship == null)
    {
        Debug.LogError("[NavigationSubSystem] GetMinDistanceToEnemyShip: ship is null!");
        return float.MaxValue;
    }

    if (ship.Owner == null || ship.Owner.EnemyShips == null)
    {
        Debug.LogError($"[NavigationSubSystem] Ship '{ship.PilotInfo?.PilotName}' has no valid owner!");
        return float.MaxValue;
    }

    float minDistanceToEnemyShip = float.MaxValue;
    foreach (GenericShip enemyShip in ship.Owner.EnemyShips.Values)
    {
        if (enemyShip == null)
        {
            Debug.LogWarning("[NavigationSubSystem] Found null enemy ship! Skipping.");
            continue;
        }

        DistanceInfo distInfo = null;
        try
        {
            distInfo = DistanceInfoPool.Get(ship, enemyShip);
            if (distInfo != null && distInfo.MinDistance != null)
            {
                if (distInfo.MinDistance.DistanceReal < minDistanceToEnemyShip)
                {
                    minDistanceToEnemyShip = distInfo.MinDistance.DistanceReal;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NavigationSubSystem] Exception in GetMinDistanceToEnemyShip: {ex.Message}");
        }
        finally
        {
            if (distInfo != null) DistanceInfoPool.Return(distInfo);
        }
    }
    return minDistanceToEnemyShip;
}

        private static bool IsActivationBeforeCurrentShip(GenericShip ship)
        {
            return ship.State.Initiative < Selection.ActiveShip.State.Initiative
                || (ship.State.Initiative == Selection.ActiveShip.State.Initiative && ship.Owner.PlayerNo == Phases.PlayerWithInitiative && ship.Owner.PlayerNo != Selection.ActiveShip.Owner.PlayerNo)
                || (ship.State.Initiative == Selection.ActiveShip.State.Initiative && ship.ShipId < Selection.ActiveShip.ShipId && ship.Owner.PlayerNo == Selection.ActiveShip.Owner.PlayerNo);
        }

        public static int TryActionPossibilities(GenericAction actionToTry, bool isBeforeManeuverPhase = false)
        {
            VirtualBoard myBoard = new VirtualBoard();
            GenericShip thisShip = Selection.ActiveShip;
            String bestBoostName = "Straight 1";
            int result = 0;

            if (VirtualBoard.Ships[thisShip].NavigationResults == null || isBeforeManeuverPhase)
            {
                return 0;
            }
            int bestPriority = VirtualBoard.Ships[thisShip].NavigationResults.Max(n => n.Value.Priority);

            NavigationResult StartingPosition = VirtualBoard.Ships[thisShip].NavigationResults.First(n => n.Key == thisShip.AssignedManeuver.ToString()).Value;

            float minDistanceToEnemyShip, minDistanceToNearestEnemyInShotRange, minAngle;
            int enemiesInShotRange, enemiesTargetingThisShip;

            ProcessHeavyGeometryCalculations(thisShip, out minDistanceToEnemyShip, out minDistanceToNearestEnemyInShotRange, out minAngle, out enemiesInShotRange, out enemiesTargetingThisShip);

            StartingPosition.distanceToNearestEnemy = minDistanceToEnemyShip;
            StartingPosition.distanceToNearestEnemyInShotRange = minDistanceToNearestEnemyInShotRange;
            StartingPosition.angleToNearestEnemy = minAngle;
            StartingPosition.enemiesInShotRange = enemiesInShotRange;
            StartingPosition.enemiesTargetingThisShip = enemiesTargetingThisShip;

            int startingResult = CalculateBoostPositionPriority(StartingPosition);

            List<BoostMove> AvailableBoostMoves = thisShip.GetAvailableBoostTemplates(new BoostAction());

            int bestBoostResult = 0;
            GenericMovement bestBoostMove = null;
            bool bestMoveStresses = false;

            foreach (BoostMove move in AvailableBoostMoves)
            {
                string selectedBoostHelper = move.Name;
                GenericMovement boostMovement;
                switch (selectedBoostHelper)
                {
                    case "Straight 1":
                        boostMovement = new StraightBoost(1, ManeuverDirection.Forward, ManeuverBearing.Straight, MovementComplexity.None);
                        break;
                    case "Bank 1 Left":
                        boostMovement = new BankBoost(1, ManeuverDirection.Left, ManeuverBearing.Bank, MovementComplexity.None);
                        break;
                    case "Bank 1 Right":
                        boostMovement = new BankBoost(1, ManeuverDirection.Right, ManeuverBearing.Bank, MovementComplexity.None);
                        break;
                    case "Turn 1 Right":
                        boostMovement = new TurnBoost(1, ManeuverDirection.Right, ManeuverBearing.Turn, MovementComplexity.None);
                        break;
                    case "Turn 1 Left":
                        boostMovement = new TurnBoost(1, ManeuverDirection.Left, ManeuverBearing.Turn, MovementComplexity.None);
                        break;
                    default:
                        boostMovement = new StraightBoost(1, ManeuverDirection.Forward, ManeuverBearing.Straight, MovementComplexity.None);
                        break;
                }

                boostMovement.Initialize();

                myBoard.UpdatePositionInfo(thisShip);
                myBoard.SwitchToRealPosition(thisShip);
                MovementPrediction prediction = new MovementPrediction(thisShip, boostMovement);
                prediction.CalculateOnlyFinalPositionIgnoringCollisions();

                myBoard.SetVirtualPositionInfo(thisShip, prediction.FinalPositionInfo, prediction.CurrentMovement.ToString());
                myBoard.SwitchToVirtualPosition(thisShip);

                NavigationResult BoostResult = new NavigationResult()
                {
                    movement = prediction.CurrentMovement,
                    isBumped = prediction.IsBumped,
                    isLandedOnObstacle = prediction.IsLandedOnAsteroid,
                    obstaclesHit = prediction.AsteroidsHit.Count,
                    isOffTheBoard = prediction.IsOffTheBoard,
                    isEscaped = determineEscaped(thisShip.EscapeEdge, prediction),
                    minesHit = prediction.MinesHit.Count,
                    isOffTheBoardNextTurn = false,
                    isHitAsteroidNextTurn = false,
                    FinalPositionInfo = prediction.FinalPositionInfo,
                    isFleeing = thisShip.IsFleeing
                };

                if (!prediction.IsOffTheBoard)
                {
                    CheckNextTurnRecursive(thisShip);

                    ProcessHeavyGeometryCalculations(thisShip, out minDistanceToEnemyShip, out minDistanceToNearestEnemyInShotRange, out minAngle, out enemiesInShotRange, out enemiesTargetingThisShip);

                    BoostResult.distanceToNearestEnemy = minDistanceToEnemyShip;
                    BoostResult.distanceToNearestEnemyInShotRange = minDistanceToNearestEnemyInShotRange;
                    BoostResult.angleToNearestEnemy = minAngle;
                    BoostResult.enemiesInShotRange = enemiesInShotRange;
                    BoostResult.enemiesTargetingThisShip = enemiesTargetingThisShip;
                }

                myBoard.SwitchToRealPosition(thisShip);

                int currentBoostResult = CalculateBoostPositionPriority(BoostResult);

                if (move.IsRed || move.IsPurple)
                {
                    // Make red maneuvers a little less optimal.
                    currentBoostResult -= 10;
                }

                if (currentBoostResult > bestBoostResult)
                {
                    bestBoostResult = currentBoostResult;
                    bestBoostMove = boostMovement;
                    bestMoveStresses = move.IsRed;
                    bestBoostName = selectedBoostHelper;
                }
            }

            if (bestBoostResult > startingResult)
            {
                AiSinglePlan bestPlan = new AiSinglePlan();
                result = bestBoostResult;
                bestPlan.Priority = bestBoostResult;
                bestPlan.currentAction = new BoostAction();
                bestPlan.currentActionMove = bestBoostMove;
                bestPlan.actionName = bestBoostName;
                bestPlan.isRedAction = bestMoveStresses;

                thisShip.AiPlans.AddPlan(bestPlan);
            }

            return result;
        }

        // Determine how good the position we have been passed is.
        private static int CalculateBoostPositionPriority(NavigationResult CurrentPosition)
        {
            int Priority = 0;
            if (CurrentPosition.isOffTheBoard)
            {
                return 0;
            }
            if (CurrentPosition.isLandedOnObstacle) Priority -= 20000;

            if (CurrentPosition.isOffTheBoardNextTurn) Priority -= 20000;

            if (CurrentPosition.enemiesInShotRange > 0)
            {
                Priority += 20;
                if (CurrentPosition.distanceToNearestEnemyInShotRange < 1)
                {
                    Priority += 10;
                }
            }
            Priority -= CurrentPosition.enemiesTargetingThisShip * 40;
            if (CurrentPosition.enemiesTargetingThisShip == 0)
            {
                Priority += 10;
                if (CurrentPosition.enemiesInShotRange > 0)
                {
                    Priority += 30;
                }
            }

            if (CurrentPosition.obstaclesHit > 0)
            {
                Priority -= CurrentPosition.obstaclesHit * 2000;
            }
            Priority -= CurrentPosition.minesHit * 2000;

            if (CurrentPosition.isBumped)
            {
                Priority -= 1000;
            }

            if (Priority < 0)
            {
                Priority = 0;
            }

            return Priority;
        }

        // ── BARREL ROLL AI ──────────────────────────────────────────────────────────

        // Manovre di scansione per libertà di movimento: velocità 1, solo basic bearings.
        // Usate per valutare quante opzioni sicure ha la nave dal turno successivo.
        private static readonly string[] ThreatScanKeys =
            { "1.F.S", "1.L.B", "1.R.B", "1.L.T", "1.R.T" };

        /// <summary>
        /// Valuta tutte le posizioni possibili post-barrel roll e restituisce lo score
        /// della migliore se è superiore alla baseline (posizione attuale).
        /// Salva il piano in AiPlans se il barrel roll è conveniente.
        /// Tutto sincrono: nessuna coroutine, nessun WaitForFixedUpdate.
        /// </summary>
        public static int TryBarrelRollPossibilities(ActionsList.BarrelRollAction actionToTry)
        {
            GenericShip thisShip = Selection.ActiveShip;

            // ── Toggle unico della qualità di collisione ostacoli ──────────────────
            // GetBullseyeObstaclePenalty usa Physics.OverlapBox, che rileva solo
            // MeshCollider convessi. Gli ostacoli sono convex=true/isTrigger=true solo
            // in modalità Low. Il toggle è fatto UNA volta per l'intera valutazione
            // (baseline + tutti i candidati) invece che ad ogni chiamata, per evitare
            // di ribakare i MeshCollider più volte nello stesso frame. Sicuro perché
            // l'intero metodo è sincrono: nessun yield tra Set e restore.
            CollisionDetectionQuality savedQuality = ObstaclesManager.CollisionDetectionQuality;
            ObstaclesManager.SetObstaclesCollisionDetectionQuality(CollisionDetectionQuality.Low);

            try
            {
                // Baseline: quante manovre sicure abbiamo dalla posizione attuale (post-manovra)
                int startingScore = GetTacticalScoreFromCurrentPosition(thisShip);
                startingScore += GetEdgePenalty(thisShip.GetPosition());
                startingScore += GetBullseyeObstaclePenalty(thisShip);

                int bestBrScore = 0;
                string bestBrPlanName = null;

                float halfBase = thisShip.ShipBase.HALF_OF_SHIPSTAND_SIZE;
                float[] shifts    = {  halfBase,  0f, -halfBase };
                string[] shiftNames = { "Forward", "Center", "Backwards" };

                var directions = new[]
                {
                    new { Name = "Left",  AnchorPos = thisShip.GetLeft(),  TemplateDir = Direction.Left,  ManDir = ManeuverDirection.Left  },
                    new { Name = "Right", AnchorPos = thisShip.GetRight(), TemplateDir = Direction.Right, ManDir = ManeuverDirection.Right }
                };

                foreach (var dir in directions)
                {
                    ManeuverTemplate brTemplate = GetSharedBrScanTemplate(dir.ManDir);
                    brTemplate.ApplyTemplate(thisShip, dir.AnchorPos, dir.TemplateDir);
                    Vector3 finisherPos = brTemplate.GetFinalPosition();
                    Vector3 finalAngles = thisShip.GetAngles(); // Straight BR non cambia il facing
                    brTemplate.SetVisible(false); // mai renderizzato: solo riposizionato, mai distrutto

                    // Se il finisher di base è già fuori dal tavolo, salta questa direzione
                    if (!IsPositionInsideBoardBounds(finisherPos)) continue;

                    for (int i = 0; i < shifts.Length; i++)
                    {
                        Vector3 shiftDir = thisShip.TransformDirection(Vector3.forward);
                        Vector3 finalPos = finisherPos + shiftDir * shifts[i];

                        // Posizione fuori dal tavolo: skip
                        if (!IsPositionInsideBoardBounds(finalPos)) continue;

                        // Posizione sovrapposta a un ostacolo: skip
                        if (IsPositionOverlappingObstacle(finalPos, thisShip)) continue;

                        // Posizione sovrapposta a un'altra nave: skip (Bug 3, rinviato deliberatamente
                        // fino ad oggi — stesso stile di IsPositionOverlappingObstacle, stessa
                        // approssimazione AABB già accettata per gli ostacoli)
                        if (IsPositionOverlappingShip(finalPos, thisShip)) continue;

                        // Sposta temporaneamente la nave per simulare le manovre da questa posizione
                        ShipPositionInfo savedPos = thisShip.GetPositionInfo();
                        thisShip.SetPositionInfo(new ShipPositionInfo(finalPos, finalAngles));

                        int tacticalScore   = GetTacticalScoreFromCurrentPosition(thisShip);
                        // Fix incoerenza: la penalità bullseye va valutata anche sui candidati,
                        // non solo sulla baseline, altrimenti il confronto è asimmetrico
                        // (esattamente il problema di design segnalato nella sessione precedente).
                        int bullseyePenalty = GetBullseyeObstaclePenalty(thisShip);

                        // Ripristina posizione reale
                        thisShip.SetPositionInfo(savedPos);

                        int edgePenalty = GetEdgePenalty(finalPos);
                        int totalScore  = tacticalScore + edgePenalty + bullseyePenalty;

                        if (totalScore > bestBrScore)
                        {
                            bestBrScore    = totalScore;
                            bestBrPlanName = dir.Name + ":" + shiftNames[i];
                        }
                    }
                }

                // Il barrel roll è conveniente solo se migliora la situazione attuale
                if (bestBrScore > startingScore && bestBrPlanName != null)
                {
                    // Unico canale AI→subphase: AiPlans, esattamente come Boost.
                    // bestBrPlanName formato: "Left:Center" o "Right:Center" — la subphase
                    // (BarrelRollPlanningSubPhase.ApplyAiPlan) fa il parsing di actionName.
                    AiSinglePlan bestPlan = new AiSinglePlan();
                    bestPlan.Priority          = bestBrScore;
                    bestPlan.currentAction     = new ActionsList.BarrelRollAction();
                    bestPlan.currentActionMove = null;
                    bestPlan.actionName        = "BR:" + bestBrPlanName;
                    bestPlan.isRedAction       = false;

                    thisShip.AiPlans.AddPlan(bestPlan);
                    return bestBrScore;
                }

                return 0;
            }
            finally
            {
                // Ripristina la qualità di collisione originale a prescindere dall'esito.
                ObstaclesManager.SetObstaclesCollisionDetectionQuality(savedQuality);
            }
        }

        /// <summary>
        /// Conta quante delle 5 manovre di scansione (vel. 1, basic bearings) portano
        /// la nave in una posizione valida: dentro i limiti del tavolo e non sovrapposta
        /// a un ostacolo. Usa CalculateOnlyFinalPositionIgnoringCollisions: sincrono,
        /// senza physics. La nave deve essere già posizionata correttamente prima di
        /// chiamare questo metodo.
        /// </summary>
        private static int GetTacticalScoreFromCurrentPosition(GenericShip ship)
        {
            int safeCount = 0;

            foreach (string key in ThreatScanKeys)
            {
                if (!ship.HasManeuver(key)) continue;

                GenericMovement movement = ShipMovementScript.MovementFromString(key, ship);
                movement.TheShip = ship;
                movement.Initialize();
                movement.IsSimple = true;

                MovementPrediction prediction = new MovementPrediction(ship, movement);
                prediction.CalculateOnlyFinalPositionIgnoringCollisions();

                if (prediction.FinalPositionInfo == null) continue;
                if (!IsPositionInsideBoardBounds(prediction.FinalPositionInfo.Position)) continue;
                if (IsPositionOverlappingObstacle(prediction.FinalPositionInfo.Position, ship)) continue;

                safeCount++;
            }

            // max +75 (5 manovre × 15).
            return safeCount * 15;
        }

        /// <summary>
        /// Controlla se una posizione simulata si sovrappone a un ostacolo piazzato.
        /// Usa i Bounds AABB del MeshCollider: sincrono, nessun physics engine.
        /// La rotazione usata è quella attuale della nave (fallback per manovre straight).
        /// Nota: AABB è una approssimazione — può produrre falsi positivi su navi ruotate
        /// di ~45°. Accettabile per un'euristica AI; raffinabile con OBB se necessario.
        /// </summary>
        private static bool IsPositionOverlappingObstacle(Vector3 worldPosition, GenericShip ship)
        {
            float halfBase = ship.ShipBase.HALF_OF_SHIPSTAND_SIZE;

            // worldPosition è l'origine del modello (fronte-centro della base).
            // Il centro geometrico della base è halfBase indietro lungo il forward della nave.
            Vector3 forward = ship.TransformDirection(Vector3.forward);
            Vector3 shipBaseCenter = worldPosition - forward * halfBase;

            // AABB approssimativo della base nave nella posizione simulata.
            // Y sottile (0.5f) per evitare falsi positivi in altezza.
            Bounds shipBounds = new Bounds(shipBaseCenter, new Vector3(halfBase * 2f, 0.5f, halfBase * 2f));

            foreach (GenericObstacle obstacle in ObstaclesManager.GetPlacedObstacles())
            {
                if (obstacle.Collider == null) continue;
                if (shipBounds.Intersects(obstacle.Collider.bounds)) return true;
            }

            return false;
        }

        /// <summary>
        /// Bug 3 (rinviato deliberatamente nella sessione originale): controlla se una
        /// posizione candidata si sovrapporrebbe a un'altra nave sul tavolo. Stesso stile
        /// e stessa approssimazione AABB di IsPositionOverlappingObstacle — coerente col
        /// livello di precisione già accettato per questo tipo di controllo preventivo.
        /// Il controllo autorevole (con fisica reale) avviene comunque dopo, durante
        /// l'esecuzione effettiva del Barrel Roll.
        /// </summary>
        private static bool IsPositionOverlappingShip(Vector3 worldPosition, GenericShip ship)
        {
            float halfBase = ship.ShipBase.HALF_OF_SHIPSTAND_SIZE;

            Vector3 forward = ship.TransformDirection(Vector3.forward);
            Vector3 shipBaseCenter = worldPosition - forward * halfBase;

            Bounds shipBounds = new Bounds(shipBaseCenter, new Vector3(halfBase * 2f, 0.5f, halfBase * 2f));

            foreach (GenericShip otherShip in Roster.AllShips.Values)
            {
                if (otherShip == ship) continue;
                if (otherShip.IsDestroyed) continue;
                if (otherShip.Collider == null) continue;

                if (shipBounds.Intersects(otherShip.Collider.bounds)) return true;
            }

            return false;
        }

        /// <summary>
        /// Controlla se una posizione mondo è dentro i limiti del tavolo,
        /// con margine pari a metà stand per evitare falsi positivi vicino al bordo.
        /// </summary>
        private static bool IsPositionInsideBoardBounds(Vector3 worldPosition)
        {
            Vector3 boardPos = Board.WorldIntoBoard(worldPosition);
            float halfX  = Board.SIZE_X / 2f;
            float halfZ  = Board.SIZE_Y / 2f;
            float margin = Board.SHIP_STAND_SIZE / 2f;
            return Mathf.Abs(boardPos.x) < (halfX - margin)
                && Mathf.Abs(boardPos.z) < (halfZ - margin);
        }

        /// <summary>
        /// Penalizza posizioni vicine al bordo del tavolo.
        /// Fascia critica (entro il 5% della larghezza): -100.
        /// Fascia di attenzione (5-10%): -50.
        /// Valori calibrati rispetto al range di TacticalScore (0-75).
        /// Da ricalibrare dopo i test.
        /// </summary>
        private static int GetEdgePenalty(Vector3 worldPosition)
        {
            Vector3 boardPos = Board.WorldIntoBoard(worldPosition);
            float distX = Board.SIZE_X / 2f - Mathf.Abs(boardPos.x);
            float distZ = Board.SIZE_Y / 2f - Mathf.Abs(boardPos.z);
            float distToNearestEdge = Mathf.Min(distX, distZ);

            if (distToNearestEdge < Board.SIZE_X * 0.05f) return -100;
            if (distToNearestEdge < Board.SIZE_X * 0.10f) return -50;
            return 0;
        }

        /// <summary>
        /// Penalizza la baseline quando c'è un ostacolo nel corridoio frontale
        /// corrispondente al bullseye arc, a distanza di una basetta.
        /// Geometria: rettangolo largo HALF_OF_BULLSEYEARC_SIZE*2 (= 0.5f),
        /// lungo SHIPSTAND_SIZE (= halfBase*2), centrato sul forward della nave
        /// a partire dal bordo frontale.
        /// Valore -60: abbastanza forte da rendere preferibile quasi qualsiasi
        /// posizione BR laterale libera da ostacoli.
        /// </summary>
        // Larghezza del corridoio bullseye: metà armata, valore di regolamento.
        private const float BULLSEYE_HALF_WIDTH  = 0.25f;
        // Mezza altezza della zona di controllo: sottile per evitare falsi positivi
        // in verticale; valore ereditato dall'implementazione precedente, non
        // riverificato contro le dimensioni reali dei modelli 3D degli ostacoli.
        private const float BULLSEYE_HALF_HEIGHT = 0.25f;

        /// <summary>
        /// Penalizza la posizione se un ostacolo occupa il corridoio frontale bullseye.
        /// ── Option B ─────────────────────────────────────────────────────────────
        /// Usa Physics.OverlapBox contro i MeshCollider reali degli ostacoli invece di
        /// un AABB world-aligned o di un test punto-in-rettangolo. Risolve insieme:
        ///  1) il bug di rotazione (Bug 2 originale): l'orientamento del box è
        ///     ship.GetRotation(), quindi ruota esattamente con la nave;
        ///  2) il problema di dimensione dell'ostacolo segnalato in revisione: non si
        ///     testa più solo il centro dell'ostacolo con un margine stimato, ma la sua
        ///     forma reale (hull convesso), quindi un Gas Cloud grande e un asteroide
        ///     piccolo vengono valutati correttamente senza margini arbitrari.
        /// Richiede che ObstaclesManager.CollisionDetectionQuality sia già Low (convex
        /// = true, isTrigger = true) al momento della chiamata: il toggle è gestito dal
        /// chiamante (TryBarrelRollPossibilities) per evitare di ribakare i collider ad
        /// ogni chiamata. Se chiamato con quality High, Physics.OverlapBox NON rileva i
        /// MeshCollider concavi degli ostacoli (limite del motore fisico, non un bug):
        /// il risultato sarebbe silenziosamente "nessun ostacolo", quindi NON richiamare
        /// questo metodo senza il toggle a monte.
        /// </summary>
        private static int GetBullseyeObstaclePenalty(GenericShip ship)
        {
            float halfBase = ship.ShipBase.HALF_OF_SHIPSTAND_SIZE;

            Vector3 forward   = ship.TransformDirection(Vector3.forward);
            Vector3 boxCenter = ship.GetPosition() + forward * halfBase;
            Vector3 halfExtents = new Vector3(BULLSEYE_HALF_WIDTH, BULLSEYE_HALF_HEIGHT, halfBase);
            Quaternion boxRotation = ship.GetRotation();

            Collider[] hits = Physics.OverlapBox(
                boxCenter,
                halfExtents,
                boxRotation,
                ~0, // tutti i layer: non abbiamo un layer dedicato confermato per gli ostacoli
                QueryTriggerInteraction.Collide // esplicito: i collider ostacolo sono trigger in Low quality
            );

            foreach (Collider hit in hits)
            {
                if (hit.CompareTag("Obstacle")) return -60;
            }

            return 0;
        }
    }
}