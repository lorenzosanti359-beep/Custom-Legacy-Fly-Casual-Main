using ActionsList;
using Ship;
using System;
using Upgrade;

namespace Ship
{
    namespace SecondEdition.LambdaClassT4AShuttle
    {
        public class LieutenantSai : LambdaClassT4AShuttle
        {
            public LieutenantSai() : base()
            {
                PilotInfo = new PilotCardInfo(
                    "Lieutenant Sai",
                    3,
                    45,
                    isLimited: true,
                    abilityType: typeof(Abilities.SecondEdition.LieutenantSaiAbility)
                );
            }
        }
    }
}

namespace Abilities.SecondEdition
{
    public class LieutenantSaiAbility : GenericAbility
    {
        GenericShip abilityTarget;
        GenericAction abilityAction;
        public override void ActivateAbility()
        {
            HostShip.OnCoordinateTargetIsSelected += RegisterAbilityEvents;
            HostShip.Ai.OnGetActionPriority += BoostCoordinatePriority;
        }

        public override void DeactivateAbility()
        {
            HostShip.OnCoordinateTargetIsSelected -= RegisterAbilityEvents;
            HostShip.Ai.OnGetActionPriority -= BoostCoordinatePriority;
        }

        // Bonus di priorità AI per Coordinate quando questa nave è pilotata da
        // Lieutenant Sai. Iscritto tramite l'hook generico
        // CustomizedAi.OnGetActionPriority (vedi CustomizedAi.cs:
        // CallGetActionPriority viene invocato per OGNI azione valutata su OGNI
        // nave, in AggressorAiPlayer.PerformActionFromList, subito dopo
        // action.GetActionPriority()) — CoordinateAction resta generica, non sa
        // nulla di questa logica: è Sai ad "alzare la mano" quando l'azione
        // valutata è un Coordinate.
        //
        // Motivazione tattica: l'abilità di Sai concede un'azione gratuita anche
        // a Sai stesso se la nave coordinata esegue un'azione presente anche
        // sulla sua action bar (vedi RegisterAbility/AbilityTakeFreeAction più
        // sotto) — un Coordinate riuscito con Sai vale potenzialmente due azioni
        // invece di una.
        // Valore di primo passaggio (40, come il bonus analogo già calibrato su
        // Reinforce in questa sessione): porta la priorità tipica di Coordinate
        // da 30 a 30+40=70, in linea con il massimo tipico di Boost osservato in
        // questo codebase — da validare in game, non ancora testato.
        private const int SAI_COORDINATE_BONUS = 40;

        private void BoostCoordinatePriority(GenericAction action, ref int priority)
        {
            if (action is CoordinateAction)
            {
                priority += SAI_COORDINATE_BONUS;
            }
        }

        private void RegisterAbilityEvents(GenericShip targetShip)
        {
            abilityTarget = targetShip;
            targetShip.OnActionIsPerformed += RegisterAbility;
            targetShip.OnActionIsSkipped += DeregisterAbilityEvents;
        }

        private void DeregisterAbilityEvents(GenericShip ship)
        {
            abilityTarget.OnActionIsPerformed -= RegisterAbility;
            abilityTarget.OnActionIsSkipped -= DeregisterAbilityEvents;
            abilityTarget = null;
            abilityAction = null;
        }

        private void RegisterAbility(GenericAction action)
        {

            DeregisterAbilityEvents(abilityTarget);

            if (action == null || !HostShip.ActionBar.HasAction(action.GetType()))
            {
                return;
            }

            abilityAction = action;
            Triggers.RegisterTrigger(new Trigger()
            {
                Name = HostShip.PilotInfo.PilotName + "'s ability",
                TriggerType = TriggerTypes.OnActionIsPerformed,
                TriggerOwner = HostShip.Owner.PlayerNo,
                EventHandler = AbilityTakeFreeAction
            });
        }

        private void AbilityTakeFreeAction(object sender, EventArgs e)
        {
            GenericShip previousActiveShip = Selection.ThisShip;
            Selection.ChangeActiveShip(HostShip);

            HostShip.AskPerformFreeAction(
                abilityAction,
                delegate
                {
                    Selection.ChangeActiveShip(previousActiveShip);
                    Triggers.FinishTrigger();
                },
                HostShip.PilotInfo.PilotName,
                "After you perform a Coordinate action, if the ship you chose performed an action on your action bar, you may perform that action",
                HostShip
            );
        }
    }
}