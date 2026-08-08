using Upgrade;
using Ship;
using System.Collections.Generic;
using System;
using ActionsList;
using UnityEngine;
using Players;

namespace UpgradesList.SecondEdition
{
    public class AfterBurners : GenericUpgrade, IVariableCost
    {
        public AfterBurners() : base()
        {
            UpgradeInfo = new UpgradeCardInfo(
                "AfterBurners",
                UpgradeType.Modification,
                cost: 0,
                abilityType: typeof(Abilities.SecondEdition.AfterBurnersAbility),
                charges: 2,
                restriction: new BaseSizeRestriction(BaseSize.Small)
            );
        }

        public void UpdateCost(GenericShip ship)
        {
            Dictionary<int, int> initiativeToCost = new Dictionary<int, int>()
            {
                {0, 4}, {1, 4}, {2, 4}, {3, 4}, {4, 5}, {5, 6}, {6, 7}
            };
            UpgradeInfo.Cost = initiativeToCost[ship.PilotInfo.Initiative];
        }
    }
}

namespace Abilities.SecondEdition
{
    //After you fully execute a speed 3-5 maneuver you may spend 1 charge to perform a boost action, even while stressed.
    public class AfterBurnersAbility : GenericAbility
    {
        public override void ActivateAbility()
        {
            HostShip.OnMovementFinish += CheckAbility;
        }

        public override void DeactivateAbility()
        {
            HostShip.OnMovementFinish -= CheckAbility;
        }

        private void CheckAbility(GenericShip ship)
        {
            if (HostShip.AssignedManeuver == null)
            {
                Debug.LogWarning($"[AfterBurners] AssignedManeuver is null for {HostShip.PilotInfo?.PilotName}");
                return;
            }

            bool speedIsCorrect = HostShip.AssignedManeuver.Speed >= 3 && HostShip.AssignedManeuver.Speed <= 5;
            bool hasCharges = HostUpgrade.State.Charges > 0;
            bool notBumped = !HostShip.IsBumped;
            bool isAi = HostShip.Owner.PlayerType == PlayerType.Ai;  // ← FIX: usa PlayerType.Ai

            Debug.Log($"[AfterBurners] CheckAbility: ship={HostShip.PilotInfo?.PilotName}, speed={HostShip.AssignedManeuver.Speed}, speedOk={speedIsCorrect}, charges={HostUpgrade.State.Charges}, bumped={HostShip.IsBumped}, isAi={isAi}");

            if (speedIsCorrect && notBumped && hasCharges)
            {
                if (isAi)
                {
                    Debug.Log("[AfterBurners] Registering AI trigger");
                    RegisterAbilityTrigger(TriggerTypes.OnMovementFinish, AskUseAbilityAi);
                }
                else
                {
                    RegisterAbilityTrigger(TriggerTypes.OnMovementFinish, AskUseAbility);
                }
            }
        }

        private void AskUseAbilityAi(object sender, EventArgs e)
        {
            Debug.Log($"[AfterBurners AI] AskUseAbilityAi started for {HostShip.PilotInfo?.PilotName}");

            GenericShip savedActiveShip = Selection.ActiveShip;

            try
            {
                Selection.ActiveShip = HostShip;

                BoostAction boostAction = new BoostAction()
                {
                    CanBePerformedWhileStressed = true
                };

                int boostPriority = AI.Aggressor.NavigationSubSystem.TryActionPossibilities(boostAction);

                Debug.Log($"[AfterBurners AI] TryActionPossibilities result = {boostPriority}");

                if (boostPriority > 0)
                {
                    Debug.Log("[AfterBurners AI] Boost accepted, spending charge");

                    HostUpgrade.State.SpendCharge();

                    var phase = Phases.StartTemporarySubPhaseNew<SubPhases.BoostPlanningSubPhase>(
                        "AfterBurners Boost",
                        delegate {
                            Triggers.FinishTrigger();
                        }
                    );

                    phase.SelectedBoostHelper = null;
                    phase.HostAction = boostAction;
                    phase.Start();
                }
                else
                {
                    Debug.Log("[AfterBurners AI] Boost rejected, priority <= 0");
                    Triggers.FinishTrigger();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AfterBurners AI] Exception: {ex}");
                Triggers.FinishTrigger();
            }
            finally
            {
                Selection.ActiveShip = savedActiveShip;
            }
        }

        private void AskUseAbility(object sender, EventArgs e)
        {
            HostShip.BeforeActionIsPerformed += RegisterSpendChargeTrigger;
            HostShip.AskPerformFreeAction(
                new BoostAction() { CanBePerformedWhileStressed = true },
                CleanUp,
                HostUpgrade.UpgradeInfo.Name,
                "After you fully execute a speed 3-5 maneuver you may spend 1 Charge to perform a Boost action, even while stressed.",
                HostUpgrade
            );
        }

        private void RegisterSpendChargeTrigger(GenericAction action, ref bool isFreeAction)
        {
            HostShip.BeforeActionIsPerformed -= RegisterSpendChargeTrigger;
            RegisterAbilityTrigger(
                TriggerTypes.OnFreeAction,
                delegate {
                    HostUpgrade.State.SpendCharge();
                    Triggers.FinishTrigger();
                }
            );
        }

        private void CleanUp()
        {
            HostShip.BeforeActionIsPerformed -= RegisterSpendChargeTrigger;
            Triggers.FinishTrigger();
        }
    }
}