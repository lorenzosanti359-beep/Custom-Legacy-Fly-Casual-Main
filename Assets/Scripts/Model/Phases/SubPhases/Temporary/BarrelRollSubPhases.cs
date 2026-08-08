using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BoardTools;
using System;
using System.Linq;
using Editions;
using Obstacles;
using ActionsList;
using Actions;
using Movement;
using Ship;
using Bombs;
using Players;

namespace SubPhases
{

    public class BarrelRollPlanningSubPhase : GenericSubPhase
    {
        //Saves forward-center-bottom temporary ship bases and their collisions
        private class BarrelRollShiftData
        {
            public GameObject TemporaryShipBase { get; private set; }
            public Direction Direction { get; private set; }
            public ObstaclesStayDetectorForced Collider { get; private set; }

            public BarrelRollShiftData(Direction direction, GameObject temporaryShipBase)
            {
                Direction = direction;
                TemporaryShipBase = temporaryShipBase;
            }

            public IEnumerator CheckCollisions()
            {
                Collider = TemporaryShipBase.GetComponentInChildren<ObstaclesStayDetectorForced>();
                Collider.TheShip = (Phases.CurrentSubPhase as BarrelRollPlanningSubPhase).TheShip;
                Collider.ReCheckCollisionsStart();

                yield return Tools.WaitForFrames(3);
            }
        }

        public GenericAction HostAction { get; set; }

        // ── AI CONTROL ────────────────────────────────────────────────────────────
        // Impostati da BarrelRollAction.ActionTake() quando l'azione è guidata dall'AI.
        // IsAiControlled = true bypassa i due DecisionSubPhase (template e posizione).
        // AiPreselectedDirection : Direction.Left / Direction.Right
        // AiPreselectedShift     : Direction.None = Center (unica opzione supportata per ora)
        public bool      IsAiControlled        { get; set; } = false;
        public Direction AiPreselectedDirection { get; set; } = Direction.Left;
        public Direction AiPreselectedShift     { get; set; } = Direction.None;
        // ─────────────────────────────────────────────────────────────────────────

        // Se true, quando l'AI fallisce silenziosamente un Barrel Roll (nessun messaggio
        // a schermo, vedi CancelBarrelRoll) logga comunque il motivo in console per lo
        // sviluppatore. Il fallimento indica un disallineamento tra l'euristica di
        // pre-validazione (NavigationSubSystem, AABB) e il controllo reale a collider
        // (ObstaclesStayDetectorForced) eseguito qui: non è normale che accada se la
        // pre-validazione funziona correttamente. Disattivare a debug concluso.
        private const bool BR_LOG_AI_SILENT_FAILURES = true;

        public override List<GameCommandTypes> AllowedGameCommandTypes
        {
            get
            {
                return new List<GameCommandTypes>() { GameCommandTypes.PressNext };
            }
        }

        protected List<ManeuverTemplate> AvailableRepositionTemplates = new List<ManeuverTemplate>();

        List<BarrelRollShiftData> BarrelRollShiftVariants = new List<BarrelRollShiftData>();
        public ObstaclesStayDetectorForced TemporaryBaseCollider
        {
            get
            {
                return BarrelRollShiftVariants.First(n => n.Direction == SelectedShift).Collider;
            }
        }
        public GameObject TemporaryShipBaseFinal;

        protected ManeuverTemplate SelectedTemplate;

        protected Direction SelectedDirectionPrimary;
        protected Direction SelectedDirectionSecondary;
        protected Direction SelectedShift;

        public bool IsTractorBeamBarrelRoll = false;
        public bool IsIgnoreObstacles = false;

        private Players.GenericPlayer controller;
        public Players.GenericPlayer Controller
        {
            get
            {
                return controller ?? TheShip.Owner;
            }
            set
            {
                controller = value;
            }
        }

        public List<ActionFailReason> BarrelRollProblems { get; private set; } = new List<ActionFailReason>();

        public bool inReposition;

        private bool IsDecloak;

        public override void Start()
        {
            Name = "Barrel Roll planning";
            IsTemporary = true;
            UpdateHelpInfo();

            StartBarrelRollPlanning();
        }

        // Core

        protected void StartBarrelRollPlanning(bool isDeckloak = false)
        {
            IsDecloak = isDeckloak;

            // ── AI: bypass completo del sistema Triggers per questa decisione ──────
            // Come in BoostPlanningSubPhase.StartBoostPlanning(): il controllo avviene
            // PRIMA di registrare qualunque trigger, non dentro l'event handler di un
            // trigger già scatenato. Questo evita l'intera classe di bug incontrata
            // finora (Triggers.FinishTrigger() che invoca comunque il continuo anche
            // quando si intende annullare).
            if (TheShip.Owner.PlayerType == PlayerType.Ai)
            {
                GenerateListOfAvailableTemplates();

                AiSinglePlan aiBrPlan = TheShip.AiPlans.GetPlanByActionName("Barrel Roll");
                if (aiBrPlan != null)
                {
                    TheShip.AiPlans.RemovePlan(aiBrPlan);
                    ApplyAiPlan(aiBrPlan);
                }
                else
                {
                    // Nessun piano salvato per questa nave (non dovrebbe accadere se
                    // GetActionPriority() ha già valutato positivamente il Barrel Roll,
                    // ma per sicurezza annulliamo invece di procedere alla cieca).
                    CancelBarrelRoll();
                }

                return;
            }

            AskToSelectTemplate(PerfromTemplatePlanning);
        }

        // Applica il piano calcolato da NavigationSubSystem.TryBarrelRollPossibilities.
        // Formato di aiPlan.actionName: "BR:<direzione>:<shift>", es. "BR:Left:Center".
        // Nota: solo Left/Right e Center sono generati oggi (design deliberato,
        // vedi commento in TryBarrelRollPossibilities); se in futuro Forward/Backwards
        // verranno supportati, questo parsing andrà esteso di conseguenza.
        private void ApplyAiPlan(AiSinglePlan aiPlan)
        {
            string[] parts = aiPlan.actionName.Split(':');
            Direction direction = (parts.Length > 1 && parts[1] == "Left") ? Direction.Left : Direction.Right;

            IsAiControlled = true;
            AiPreselectedDirection = direction;
            AiPreselectedShift = Direction.None; // Center, unica opzione supportata per ora

            ManeuverTemplate aiTemplate = AvailableRepositionTemplates.FirstOrDefault(t => t.Bearing == ManeuverBearing.Straight);
            if (aiTemplate != null)
            {
                SelectTemplate(aiTemplate, direction);
            }
            // Se aiTemplate è null, SelectedTemplate resta null: il guardiano in
            // PerfromTemplatePlanning() annullerà correttamente il Barrel Roll.

            // Chiamata diretta, non tramite Triggers.FinishTrigger(): evita di
            // innescare la cascata che ha causato i bug precedenti.
            PerfromTemplatePlanning();
        }

        public virtual void PerfromTemplatePlanning()
        {
            // ── Guardiano ────────────────────────────────────────────────────────
            // Triggers.FinishTrigger() invoca SEMPRE questo metodo come continuazione,
            // anche quando chi lo chiama intendeva annullare (es. AI senza template
            // Straight disponibile). Non c'è modo di "chiudere" il trigger senza
            // invocare questo continuo: il controllo va quindi fatto qui, non a monte.
            if (SelectedTemplate == null)
            {
                CancelBarrelRoll();
                return;
            }

            Selection.ThisShip.CallUpdateChosenBarrelRollTemplate(ref SelectedTemplate);

            Edition.Current.BarrelRollTemplatePlanning();
        }

        public void PerfromTemplatePlanningSecondEdition()
        {
            GameManagerScript.Instance.StartCoroutine(
                CheckCollisionsOfTemporaryElements(AskBarrelRollShift)
            );
        }

        private void AskBarrelRollShift()
        {
            // ── AI: stesso bypass, seconda decisione (posizione lungo il template) ──
            if (IsAiControlled)
            {
                SetBarrelRollPositionAi(AiPreselectedShift);

                // Chiamata diretta: ConfirmBarrelRollPosition() ha già il guardiano
                // su BarrelRollProblems e gestisce da sé l'eventuale annullamento.
                ConfirmBarrelRollPosition();
                return;
            }

            Triggers.RegisterTrigger(new Trigger()
            {
                Name = "Barrel Roll position",
                TriggerType = TriggerTypes.OnAbilityDirect,
                TriggerOwner = Controller.PlayerNo,
                EventHandler = StartAskBarrelRollShiftSubphase
            });

            Triggers.ResolveTriggers(TriggerTypes.OnAbilityDirect, ConfirmBarrelRollPosition);
        }

        public void ConfirmBarrelRollPosition()
        {
            // ── Guardiano ────────────────────────────────────────────────────────
            // Stesso principio di PerfromTemplatePlanning: Triggers.FinishTrigger()
            // invoca sempre questo metodo, anche dal ramo AI in cui la posizione
            // Center è invalida. In quel caso SelectedTemplate è già stato distrutto
            // (DestroyTemporaryElements in SetBarrelRollPositionAi/StartAskBarrelRollShiftSubphase),
            // quindi CheckBarrelRollThroughObstacle() esploderebbe su Collider nullo.
            // Verifichiamo qui, prima di procedere, invece di provare a impedire la
            // chiamata a monte (impossibile con l'architettura attuale dei trigger).
            if (BarrelRollProblems.Count > 0)
            {
                CancelBarrelRoll();
                return;
            }

            CheckBarrelRollThroughObstacle();
            CheckMines();
            SyncCollisions(TemporaryBaseCollider);
            DestroyTemporaryElements();

            StartRepositionExecution();
        }

        private void CheckBarrelRollThroughObstacle()
        {
            if (SelectedTemplate.Collider.OverlapsAsteroidNow || TemporaryBaseCollider.OverlapsAsteroidNow)
            {
                if (HostAction is BarrelRollAction)
                {
                    (HostAction as BarrelRollAction).IsThroughObstacle = true;
                }
            }
        }

        public void StartRepositionExecution()
        {
            StartRepositionExecutionSubphase();
        }

        // Subs

        private void AskToSelectTemplate(Action callback)
        {
            GenerateListOfAvailableTemplates();

            if (AvailableRepositionTemplates.Count > 0)
            {
                RegisterDirectionDecisionTrigger(callback);
            }
            else
            {
                //
            }
        }

        protected virtual void GenerateListOfAvailableTemplates()
        {
            List<ManeuverTemplate> allowedTemplates = Selection.ThisShip.GetAvailableBarrelRollTemplates(HostAction);

            foreach (ManeuverTemplate barrelRollTemplate in allowedTemplates)
            {
                AvailableRepositionTemplates.Add(barrelRollTemplate);
            }
        }

        private void RegisterDirectionDecisionTrigger(Action callback)
        {
            Triggers.RegisterTrigger(new Trigger()
            {
                Name = "Select direction and template",
                TriggerType = TriggerTypes.OnAbilityDirect,
                TriggerOwner = Controller.PlayerNo,
                EventHandler = StartSelectTemplateSubphase
            });

            Triggers.ResolveTriggers(TriggerTypes.OnAbilityDirect, callback);
        }

        protected void StartSelectTemplateSubphase(object sender, System.EventArgs e)
        {
            // Nota: l'AI non registra mai il trigger che porta qui — il bypass avviene
            // a monte, in StartBarrelRollPlanning()/ApplyAiPlan(). Questo metodo gestisce
            // solo il percorso umano.
            BarrelRollDirectionDecisionSubPhase selectBarrelRollTemplate = (BarrelRollDirectionDecisionSubPhase)Phases.StartTemporarySubPhaseNew(
                Name,
                typeof(BarrelRollDirectionDecisionSubPhase),
                Triggers.FinishTrigger
            );

            GenerateSelectTemplateDecisions(selectBarrelRollTemplate);

            selectBarrelRollTemplate.DescriptionShort = GetBarrelRollDescriptions();

            selectBarrelRollTemplate.DefaultDecisionName = selectBarrelRollTemplate.GetDecisions().First().Name;

            selectBarrelRollTemplate.RequiredPlayer = Controller.PlayerNo;

            selectBarrelRollTemplate.Start();
        }

        private string GetBarrelRollDescriptions()
        {
            return (!IsDecloak) ? "Barrel Roll: Select template" : "Decloak: Select template";
        }

        protected virtual void GenerateSelectTemplateDecisions(DecisionSubPhase subphase)
        {
            // Straight templates
            foreach (ManeuverTemplate template in AvailableRepositionTemplates)
            {
                if (template.Bearing == ManeuverBearing.Straight)
                {
                    subphase.AddDecision(
                        "Left " + template.NameNoDirection,
                        (EventHandler)delegate {
                            SelectTemplate(template, Direction.Left);
                            DecisionSubPhase.ConfirmDecision();
                        }
                    );

                    subphase.AddDecision(
                        "Right " + template.NameNoDirection,
                        (EventHandler)delegate {
                            SelectTemplate(template, Direction.Right);
                            DecisionSubPhase.ConfirmDecision();
                        }
                    );
                }
            }

            List<ManeuverSpeed> speedsToCheck = new List<ManeuverSpeed> { ManeuverSpeed.Speed1, ManeuverSpeed.Speed2, ManeuverSpeed.Speed3 };

            foreach(ManeuverSpeed speed in speedsToCheck)
            {
                // Bank templates
                ManeuverTemplate bankLeft = AvailableRepositionTemplates.FirstOrDefault(n => n.Bearing == ManeuverBearing.Bank && n.Direction == ManeuverDirection.Left && n.Speed == speed);
                ManeuverTemplate bankRight = AvailableRepositionTemplates.FirstOrDefault(n => n.Bearing == ManeuverBearing.Bank && n.Direction == ManeuverDirection.Right && n.Speed == speed);

                if (bankLeft != null && bankRight != null)
                {
                    subphase.AddDecision(
                        "Left " + bankRight.NameNoDirection + " Forward",
                        (EventHandler)delegate
                        {
                            SelectTemplate(bankRight, Direction.Left, Direction.Top);
                            DecisionSubPhase.ConfirmDecision();
                        }
                    );

                    subphase.AddDecision(
                        "Right " + bankLeft.NameNoDirection + " Forward",
                        (EventHandler)delegate
                        {
                            SelectTemplate(bankLeft, Direction.Right, Direction.Top);
                            DecisionSubPhase.ConfirmDecision();
                        }
                    );

                    subphase.AddDecision(
                        "Left " + bankLeft.NameNoDirection + " Backwards",
                        (EventHandler)delegate
                        {
                            SelectTemplate(bankLeft, Direction.Left, Direction.Bottom);
                            DecisionSubPhase.ConfirmDecision();
                        }
                    );

                    subphase.AddDecision(
                        "Right " + bankRight.NameNoDirection + " Backwards",
                        (EventHandler)delegate
                        {
                            SelectTemplate(bankRight, Direction.Right, Direction.Bottom);
                            DecisionSubPhase.ConfirmDecision();
                        }
                    );
                }

                // turn templates
                ManeuverTemplate turnLeft = AvailableRepositionTemplates.FirstOrDefault(n => n.Bearing == ManeuverBearing.Turn && n.Direction == ManeuverDirection.Left && n.Speed == speed);
                ManeuverTemplate turnRight = AvailableRepositionTemplates.FirstOrDefault(n => n.Bearing == ManeuverBearing.Turn && n.Direction == ManeuverDirection.Right && n.Speed == speed);

                if (turnLeft != null && turnRight != null)
                {
                    subphase.AddDecision(
                        "Left " + turnRight.NameNoDirection + " Forward",
                        (EventHandler)delegate
                        {
                            SelectTemplate(turnRight, Direction.Left, Direction.Top);
                            DecisionSubPhase.ConfirmDecision();
                        }
                    );

                    subphase.AddDecision(
                        "Right " + turnLeft.NameNoDirection + " Forward",
                        (EventHandler)delegate
                        {
                            SelectTemplate(turnLeft, Direction.Right, Direction.Top);
                            DecisionSubPhase.ConfirmDecision();
                        }
                    );

                    subphase.AddDecision(
                        "Left " + turnLeft.NameNoDirection + " Backwards",
                        (EventHandler)delegate
                        {
                            SelectTemplate(turnLeft, Direction.Left, Direction.Bottom);
                            DecisionSubPhase.ConfirmDecision();
                        }
                    );

                    subphase.AddDecision(
                        "Right " + turnRight.NameNoDirection + " Backwards",
                        (EventHandler)delegate
                        {
                            SelectTemplate(turnRight, Direction.Right, Direction.Bottom);
                            DecisionSubPhase.ConfirmDecision();
                        }
                    );
                }

            }
        }

        public void SelectTemplate(ManeuverTemplate template, Direction directionPrimary, Direction directionSecondary = Direction.None)
        {
            SelectedTemplate = template;
            SelectedDirectionPrimary = directionPrimary;
            SelectedDirectionSecondary = directionSecondary;
            if (HostAction is BarrelRollAction)
            {
                (HostAction as BarrelRollAction).SelectedTemplate = template;
            }
        }

        protected virtual IEnumerator CheckCollisionsOfTemporaryElements(Action callback)
        {
            yield return CheckTemplate();

            if (!IsColliderDataAllowed(SelectedTemplate.Collider))
            {
                CancelBarrelRoll();
            }
            else
            {
                yield return CheckPotentialFinalPositions();

                if (IsPotentialFinalPositionsAnyAllowed())
                {
                    callback();
                }
                else
                {
                    CancelBarrelRoll();
                }
            }
        }

        protected virtual void CancelBarrelRoll()
        {
            DestroyTemporaryElements(isAll: true);

            // ── Pattern verificato su BoostAction/BoostPlanningSubPhase ────────────
            // Il codice di Boost (funzionante, incluso il fallimento AI) documenta
            // esplicitamente che passare per Rules.Actions.ActionIsFailed() / 
            // ship.CallActionIsReadyToBeFailed() per un giocatore AI causa un
            // comportamento successivo anomalo (commento originale: "figure out why
            // this allows the AI to take a different action"). La soluzione adottata
            // lì è chiamare RevertActionOnFail() direttamente per l'AI, bypassando la
            // cascata di eventi. Replichiamo lo stesso pattern qui.
            //
            // FIX: ShowInformationAboutProblems() era chiamato incondizionatamente,
            // anche per l'AI — mostrava a schermo un messaggio d'errore per un
            // fallimento che l'AI gestisce silenziosamente (l'utente non ha mai fatto
            // alcuna scelta interattiva a cui il messaggio si riferisse). È un residuo
            // del percorso umano non escluso quando è stato aggiunto il bypass AI.
            // Spostato nel solo ramo umano, dove il messaggio ha effettivamente senso.
            if (IsAiControlled)
            {
                if (BR_LOG_AI_SILENT_FAILURES)
                {
                    string reasons = string.Join(", ", BarrelRollProblems);
                    Debug.Log($"[BarrelRoll-AI] Fallimento silenzioso (nessun messaggio a schermo): " +
                              $"nave ShipId={TheShip.ShipId}, motivi=[{reasons}]. La nave tenterà un'altra azione. " +
                              $"Indica un possibile disallineamento tra la pre-validazione euristica " +
                              $"(NavigationSubSystem) e il controllo reale a collider eseguito qui.");
                }
                HostAction.RevertActionOnFail(false);
            }
            else
            {
                ShowInformationAboutProblems();
                WhenCancelBarrelRollWithProblems(BarrelRollProblems);
            }
        }

        private void ShowInformationAboutProblems()
        {
            foreach (var problem in BarrelRollProblems)
            {
                switch (problem)
                {
                    case ActionFailReason.Bumped:
                        Messages.ShowError("Barrel Roll would cause this ship to overlap another ship");
                        break;
                    case ActionFailReason.OffTheBoard:
                        Messages.ShowError("Barrel Roll would cause this ship to leave the battlefield");
                        break;
                    case ActionFailReason.ObstacleHit:
                        Messages.ShowError("Barrel Roll would cause this ship to overlap an obstacle");
                        break;
                    default:
                        break;
                }
            }
        }

        private IEnumerator CheckTemplate()
        {
            ShowBarrelRollTemplate();

            SelectedTemplate.Collider.TheShip = TheShip;
            SelectedTemplate.Collider.ReCheckCollisionsStart();

            yield return Tools.WaitForFrames(3);
        }

        private bool IsColliderDataAllowed(ObstaclesStayDetectorForced collider, bool isBaseFinalPosition = false)
        {
            List<GenericDeviceGameObject> potentiallyHitMines = new List<GenericDeviceGameObject>();
            if (collider.OverlapedMinesNow.Count > 0)
            {
                foreach (var mineHit in collider.OverlapedMinesNow)
                {
                    GenericDeviceGameObject MineObject = mineHit.transform.parent.GetComponent<GenericDeviceGameObject>();
                    if (!TheShip.MinesHit.Contains(MineObject))
                    {
                        potentiallyHitMines.Add(MineObject);
                    }
                }
                TheShip.MinesHit.AddRange(potentiallyHitMines);
            }
            if (collider.OverlapsShipNow && isBaseFinalPosition)
            {
                BarrelRollProblems.Add(ActionFailReason.Bumped);
                TheShip.MinesHit = TheShip.MinesHit.Except(potentiallyHitMines).ToList();

            }
            else if (!TheShip.IsIgnoreObstacles
                && !TheShip.IsIgnoreObstaclesDuringBarrelRoll()
                && !IsIgnoreObstacles
                && collider.OverlapsAsteroidNow
                && !TheShip.IgnoreObstacleTypes.Contains(typeof(Asteroid)))
            {
                BarrelRollProblems.Add(ActionFailReason.ObstacleHit);
                TheShip.MinesHit = TheShip.MinesHit.Except(potentiallyHitMines).ToList();
            }
            else if (collider.OffTheBoardNow)
            {
                BarrelRollProblems.Add(ActionFailReason.OffTheBoard);
                TheShip.MinesHit = TheShip.MinesHit.Except(potentiallyHitMines).ToList();
            }
            if (TheShip.IsIgnoreObstaclesDuringBarrelRoll()
                && collider.OverlapsAsteroidNow
                && !IsIgnoreObstacles
                && !TheShip.IgnoreObstacleTypes.Contains(typeof(Asteroid)))
            {
                TheShip.IsHitObstacles = true;
                foreach (GenericObstacle hitObstacle in collider.OverlappedAsteroidsNow)
                {
                    if (!TheShip.ObstaclesHit.Contains(hitObstacle))
                    {
                        TheShip.ObstaclesHit.Add(hitObstacle);
                    }
                }

            }
            return BarrelRollProblems.Count == 0;
        }

        private IEnumerator CheckPotentialFinalPositions()
        {
            List<Direction> directions = new List<Direction>() {
                Direction.Top,
                Direction.None,
                Direction.Bottom
            };

            foreach (var direction in directions)
            {
                BarrelRollShiftData currentData = new BarrelRollShiftData(
                    direction,
                    ShowTemporaryShipBase(direction, isVisible: false)
                );

                BarrelRollShiftVariants.Add(currentData);
                yield return currentData.CheckCollisions();
            }
        }

        private bool IsPotentialFinalPositionsAnyAllowed()
        {
            bool isAllowed = false;
            foreach (BarrelRollShiftData barrelRollData in BarrelRollShiftVariants)
            {
                BarrelRollProblems = new List<ActionFailReason>();
                if (IsColliderDataAllowed(barrelRollData.Collider, isBaseFinalPosition:true))
                {
                    isAllowed = true;
                }
            }
            return isAllowed;
        }

        private void StartAskBarrelRollShiftSubphase(object sender, System.EventArgs e)
        {
            // Nota: l'AI non registra mai il trigger che porta qui — il bypass avviene
            // a monte, in AskBarrelRollShift(). Questo metodo gestisce solo il percorso umano.
            BarrelRollPositionDecisionSubPhase selectBarrelRollPosition = (BarrelRollPositionDecisionSubPhase)Phases.StartTemporarySubPhaseNew(
                 Name,
                 typeof(BarrelRollPositionDecisionSubPhase),
                 Triggers.FinishTrigger
            );

            selectBarrelRollPosition.AddDecision("Forward", delegate { SetBarrelRollPosition(Direction.Top); }, isCentered: true);
            selectBarrelRollPosition.AddDecision("Center", delegate { SetBarrelRollPosition(Direction.None); }, isCentered: true);
            selectBarrelRollPosition.AddDecision("Backwards", delegate { SetBarrelRollPosition(Direction.Bottom); }, isCentered: true);

            selectBarrelRollPosition.DescriptionShort = "Barrel Roll: Select position";

            selectBarrelRollPosition.DefaultDecisionName = "Center";

            selectBarrelRollPosition.RequiredPlayer = Controller.PlayerNo;

            selectBarrelRollPosition.ShowSkipButton = false;
            selectBarrelRollPosition.OnNextButtonIsPressed = DecisionSubPhase.ConfirmDecision;

            selectBarrelRollPosition.Start();
        }

        private void SetBarrelRollPosition(Direction direction)
        {
            SelectedShift = direction;

            foreach (var barrelRollShiftVariant in BarrelRollShiftVariants)
            {
                ToggleTemporaryShipBaseVisibility(
                    barrelRollShiftVariant.TemporaryShipBase,
                    barrelRollShiftVariant.Direction == SelectedShift
                );
            }

            BarrelRollProblems = new List<ActionFailReason>();

            if (!IsColliderDataAllowed(TemporaryBaseCollider, isBaseFinalPosition:true))
            {
                Messages.ShowError("This final position is not valid, choose another position");
                UI.HideNextButton();
            }
            else
            {
                UI.ShowNextButton();
                UI.HighlightNextButton();
            }

            DecisionSubPhase.ResetInput();
        }

        // Equivalente di SetBarrelRollPosition ma senza dipendenze da una
        // DecisionSubPhase attiva (UI.ShowNextButton/HideNextButton, ResetInput).
        // Usato esclusivamente dal percorso AI in StartAskBarrelRollShiftSubphase.
        private void SetBarrelRollPositionAi(Direction direction)
        {
            SelectedShift = direction;

            foreach (var barrelRollShiftVariant in BarrelRollShiftVariants)
            {
                ToggleTemporaryShipBaseVisibility(
                    barrelRollShiftVariant.TemporaryShipBase,
                    barrelRollShiftVariant.Direction == SelectedShift
                );
            }

            BarrelRollProblems = new List<ActionFailReason>();
            IsColliderDataAllowed(TemporaryBaseCollider, isBaseFinalPosition: true);
        }

        private void ShowBarrelRollTemplate()
        {
            SelectedTemplate.ApplyTemplate(
                TheShip,
                (SelectedDirectionPrimary == Direction.Left) ? TheShip.GetLeft() : TheShip.GetRight(),
                SelectedDirectionPrimary
            );
        }

        private GameObject ShowTemporaryShipBase(Direction shiftDirection, bool isVisible = true)
        {
            GameObject prefab = (GameObject)Resources.Load(TheShip.ShipBase.TemporaryPrefabPath, typeof(GameObject));
            GameObject temporaryShipBase = MonoBehaviour.Instantiate(
                prefab,
                SelectedTemplate.GetFinalPosition(),
                SelectedTemplate.GetFinalRotation(),
                Board.GetBoard()
            );

            int directionModifier = (SelectedDirectionPrimary == Direction.Left) ? -1 : 1;

            float finalShift = 0;
            switch (shiftDirection)
            {
                case Direction.Top:
                    finalShift += (SelectedTemplate.IsSideTemplate) ? 0.5f : 0.25f;
                    break;
                case Direction.Bottom:
                    finalShift -= (SelectedTemplate.IsSideTemplate) ? 0.5f : 0.25f;
                    break;
                default:
                    break;
            }

            temporaryShipBase.transform.localEulerAngles += new Vector3(0, directionModifier * -90, 0);

            Vector3 shift = new Vector3(
                directionModifier * TheShip.ShipBase.HALF_OF_SHIPSTAND_SIZE,
                0,
                TheShip.ShipBase.HALF_OF_SHIPSTAND_SIZE + finalShift
            );
            Vector3 absPosition = temporaryShipBase.transform.TransformPoint(shift);

            temporaryShipBase.transform.position = absPosition;

            temporaryShipBase.transform.Find("ShipBase").Find("ShipStandInsert").Find("ShipStandInsertImage").Find("default").GetComponent<Renderer>().material = TheShip.Model.transform.Find("RotationHelper").Find("RotationHelper2").Find("ShipAllParts").Find("ShipBase").Find("ShipStandInsert").Find("ShipStandInsertImage").Find("default").GetComponent<Renderer>().material;
            temporaryShipBase.transform.Find("ShipBase").Find("ObstaclesStayDetector").gameObject.AddComponent<ObstaclesStayDetectorForced>();

            ToggleTemporaryShipBaseVisibility(temporaryShipBase, isVisible);

            return temporaryShipBase;
        }

        private void ToggleTemporaryShipBaseVisibility(GameObject shipBase, bool isVisible)
        {
            foreach (Renderer renderer in shipBase.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = isVisible;
            }
        }

        protected class BarrelRollDirectionDecisionSubPhase : DecisionSubPhase { }

        protected class BarrelRollPositionDecisionSubPhase : DecisionSubPhase { }

        public void WhenCancelBarrelRollWithProblems(List<ActionFailReason> barrelRollProblems)
        {
            if (HostAction == null) HostAction = new BarrelRollAction() { HostShip = TheShip };
            Rules.Actions.ActionIsFailed(TheShip, HostAction, barrelRollProblems);
        }

        //OLD
        public void TryConfirmBarrelRollNetwork(string templateName, Vector3 shipPosition, Vector3 movementTemplatePosition)
        {
            /*StopDrag();

            SelectTemplate((ActionsHolder.BarrelRollTemplateVariants) Enum.Parse(typeof(ActionsHolder.BarrelRollTemplateVariants), templateName));

            ShowBarrelRollTemplate();
            BarrelRollTemplate.transform.position = movementTemplatePosition;

            ShowTemporaryShipBase();
            TemporaryShipBase.transform.position = shipPosition;
            TemporaryShipBase.transform.rotation = GetCurrentBarrelRollHelperTemplateFinisherBasePositionGO().transform.rotation;

            TryConfirmBarrelRollPosition();*/
        }

        private void DestroyTemporaryElements(bool isAll = false)
        {
            foreach (var data in BarrelRollShiftVariants)
            {
                if (data.Direction == SelectedShift && !isAll)
                {
                    TemporaryShipBaseFinal = data.TemporaryShipBase;
                }
                else
                {
                    GameObject.Destroy(data.TemporaryShipBase);
                }
            }
            BarrelRollShiftVariants = new List<BarrelRollShiftData>();
            SelectedTemplate.DestroyTemplate();
        }

        private void CheckMines()
        {
            foreach (var mineCollider in SelectedTemplate.Collider.OverlapedMinesNow)
            {
                GenericDeviceGameObject mineObject = mineCollider.transform.parent.GetComponent<GenericDeviceGameObject>();
                if (!TheShip.MinesHit.Contains(mineObject)) TheShip.MinesHit.Add(mineObject);
            }
        }

        private void SyncCollisions(ObstaclesStayDetectorForced collider)
        {
            TheShip.ObstaclesLanded = new List<GenericObstacle>(collider.OverlappedAsteroidsNow);
            if (!TheShip.IsIgnoreObstaclesDuringBarrelRoll())
            {
                collider.OverlappedAsteroidsNow
                    .Where((a) => !TheShip.ObstaclesHit.Contains(a)).ToList()
                    .ForEach(TheShip.ObstaclesHit.Add);
            }
        }

        protected virtual void StartRepositionExecutionSubphase()
        {
            Pause();

            TheShip.ToggleShipStandAndPeg(false);

            BarrelRollExecutionSubPhase executionSubphase = (BarrelRollExecutionSubPhase)Phases.StartTemporarySubPhaseNew(
                "Barrel Roll execution",
                typeof(BarrelRollExecutionSubPhase),
                CallBack
            );

            executionSubphase.TheShip = TheShip;
            executionSubphase.TemporaryShipBase = TemporaryShipBaseFinal;
            executionSubphase.Direction = SelectedDirectionPrimary;
            executionSubphase.IsTractorBeamBarrelRoll = IsTractorBeamBarrelRoll;

            executionSubphase.Start();
        }

        public override void Next()
        {
            Phases.CurrentSubPhase = PreviousSubPhase;
            UpdateHelpInfo();
        }

        public override bool ThisShipCanBeSelected(GenericShip ship, int mouseKeyIsPressed)
        {
            return false;
        }

        public override bool AnotherShipCanBeSelected(GenericShip anotherShip, int mouseKeyIsPressed)
        {
            return false;
        }

    }

    public class BarrelRollExecutionSubPhase : GenericSubPhase
    {
        private float progressCurrent;
        private float progressTarget;

        private float initialRotation;
        private float plannedRotation;

        private bool performingAnimation;

        public bool IsTractorBeamBarrelRoll;

        public GameObject TemporaryShipBase;
        public Direction Direction;

        public override void Start()
        {
            Name = "Barrel Roll execution";
            IsTemporary = true;
            UpdateHelpInfo();

            StartBarrelRollExecution();
        }

        private void StartBarrelRollExecution()
        {
            Rules.Collision.ClearBumps(TheShip);

            progressCurrent = 0;
            progressTarget = Vector3.Distance(TheShip.GetPosition(), TemporaryShipBase.transform.position);

            initialRotation = (TheShip.GetAngles().y < 180) ? TheShip.GetAngles().y : -(360 - TheShip.GetAngles().y);
            plannedRotation = (TemporaryShipBase.transform.eulerAngles.y - initialRotation < 180) ? TemporaryShipBase.transform.eulerAngles.y : -(360 - TemporaryShipBase.transform.eulerAngles.y);

            if (!IsTractorBeamBarrelRoll) Sounds.PlayFly(TheShip);

            performingAnimation = true;
        }

        public override void Update()
        {
            if (performingAnimation) DoBarrelRollAnimation();
        }

        private void DoBarrelRollAnimation()
        {
            float progressStep = Time.deltaTime * 0.2f * (0.5f + Options.AnimationSpeed * 10f);
            progressStep = Mathf.Min(progressStep, progressTarget - progressCurrent);
            progressCurrent += progressStep;

            TheShip.SetPosition(Vector3.MoveTowards(TheShip.GetPosition(), TemporaryShipBase.transform.position, progressStep));

            if (!IsTractorBeamBarrelRoll && !TheShip.IsLandedModel)
            {
                TheShip.RotateModelDuringBarrelRoll(progressCurrent / progressTarget, (Direction == Direction.Left) ? -1 : 1);
                TheShip.SetRotationHelper2Angles(new Vector3(0, progressCurrent / progressTarget * (plannedRotation - initialRotation), 0));
                TheShip.MoveUpwards(progressCurrent / progressTarget);
            }

            if (progressCurrent >= progressTarget)
            {
                performingAnimation = false;
                FinishReposition();
            }
        }

        protected virtual void FinishReposition()
        {
            FinishBarrelRollAnimation();
        }

        public void FinishBarrelRollAnimation()
        {
            performingAnimation = false;

            TheShip.ApplyRotationHelpers();
            TheShip.ResetRotationHelpers();
            TheShip.SetAngles(TemporaryShipBase.transform.eulerAngles);

            MonoBehaviour.DestroyImmediate(TemporaryShipBase);

            GameManagerScript Game = GameObject.Find("GameManager").GetComponent<GameManagerScript>();
            Game.Movement.CollidedWith = null;

            MovementTemplates.HideLastMovementRuler();

            TheShip.ToggleShipStandAndPeg(true);
            if (TheShip.IsLandedModel) TheShip.TogglePeg(false);

            TheShip.CallPositionIsReadyToFinish(FinishBarrelRollAnimationPart2);
        }

        protected virtual void FinishBarrelRollAnimationPart2()
        {
            Phases.FinishSubPhase(typeof(BarrelRollExecutionSubPhase));
            CallBack();
        }

        public override void Next()
        {
            Phases.CurrentSubPhase = Phases.CurrentSubPhase.PreviousSubPhase;
            Phases.CurrentSubPhase = Phases.CurrentSubPhase.PreviousSubPhase;
            UpdateHelpInfo();
        }

        public override bool ThisShipCanBeSelected(GenericShip ship, int mouseKeyIsPressed)
        {
            bool result = false;
            return result;
        }

        public override bool AnotherShipCanBeSelected(GenericShip anotherShip, int mouseKeyIsPressed)
        {
            bool result = false;
            return result;
        }

    }

}