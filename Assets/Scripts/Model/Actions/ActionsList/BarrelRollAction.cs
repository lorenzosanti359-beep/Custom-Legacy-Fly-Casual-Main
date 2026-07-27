using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BoardTools;
using SubPhases;

namespace ActionsList
{
    public class BarrelRollAction : GenericAction
    {
        public bool IsThroughObstacle { get; set; }

        public ManeuverTemplate SelectedTemplate { get; set; }

        public BarrelRollAction()
        {
            Name = "Barrel Roll";
        }

        public override void ActionTake()
        {
            if (Selection.ThisShip.Owner.UsesHotacAiRules)
            {
                Phases.CurrentSubPhase.CallBack();
            }
            else
            {
                Phases.CurrentSubPhase.Pause();

                BarrelRollPlanningSubPhase subphase = Phases.StartTemporarySubPhaseNew<BarrelRollPlanningSubPhase>(
                    "Barrel Roll",
                    Phases.CurrentSubPhase.CallBack
                );
                subphase.HostAction = this;

                // La subphase determina da sé se il giocatore è AI (TheShip.Owner.PlayerType)
                // e in tal caso recupera il piano da TheShip.AiPlans.GetPlanByActionName("Barrel Roll"),
                // esattamente come BoostPlanningSubPhase.StartBoostPlanning(). Non serve più
                // passare qui alcuna preselezione.
                subphase.Start();
            }
        }

        public override int GetActionPriority()
        {
            // DEBUG: per forzare sempre il barrel roll nei test, decommentare la riga seguente
            // e aggiungere DebugForceBarrelRoll a DebugManager.
            // if (DebugManager.DebugForceBarrelRoll) return 9999;

            return AI.Aggressor.NavigationSubSystem.TryBarrelRollPossibilities(this);
        }

        public override void RevertActionOnFail(bool hasSecondChance = false)
        {
            SelectedTemplate = null;
            Phases.GoBack();
        }
    }
}