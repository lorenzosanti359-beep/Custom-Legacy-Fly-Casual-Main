using SubPhases;
using System.Collections;
using System.Collections.Generic;
using Tokens;
using UnityEngine;

namespace ActionsList
{

    public class ReinforceAction : GenericAction
    {
        private Direction aiBetterSide = Direction.None;

        // Priorità base indipendente dalla minaccia attuale: garantisce che Reinforce
        // resti un'opzione ragionevole anche in preparazione, non solo come reazione
        // a chi sta già puntando la nave in questo momento. Aumentata da 25 a 35.
        private const int REINFORCE_BASE_PRIORITY = 35;

        // Bonus per ogni nave nemica che sta puntando questa nave da quel lato (fore o
        // aft), secondo ActionsHolder.CountEnemiesTargeting — la metrica originale,
        // ripristinata dopo aver considerato e scartato un conteggio puramente
        // posizionale (navi nell'arco a prescindere da gittata/linea di vista):
        // CountEnemiesTargeting resta la misura di minaccia più pertinente per
        // decidere dove serve davvero Reinforce. Aumentato da 30 a 40: con 2 navi
        // nemiche che puntano dallo stesso lato la priorità sale a 35+80=115, sopra
        // il massimo tipico osservato per Boost in questo codebase (~70 nel caso
        // migliore, CalculateBoostPositionPriority in NavigationSubSystem.cs).
        private const int REINFORCE_PER_ENEMY_BONUS = 40;

        public ReinforceAction()
        {
            Name = DiceModificationName = "Reinforce";
            ImageUrl = "https://raw.githubusercontent.com/guidokessels/xwing-data/master/images/reference-cards/ReinforceAction.png";
        }

        public override void ActionTake()
        {
            ReinforceSideSubphase decisionSubphase = (ReinforceSideSubphase)Phases.StartTemporarySubPhaseNew(
                Name,
                typeof(ReinforceSideSubphase),
                Phases.CurrentSubPhase.CallBack
            );

            decisionSubphase.DescriptionShort = "Reinforce: Select a side";
            decisionSubphase.RequiredPlayer = Selection.ThisShip.Owner.PlayerNo;

            decisionSubphase.AddDecision(
                "Fore side",
                delegate { Selection.ThisShip.Tokens.AssignToken(typeof(ReinforceForeToken), DecisionSubPhase.ConfirmDecision); },
                isCentered: true
            );

            decisionSubphase.AddDecision(
                "Aft side",
                delegate { Selection.ThisShip.Tokens.AssignToken(typeof(ReinforceAftToken), DecisionSubPhase.ConfirmDecision); },
                isCentered: true
            );

            decisionSubphase.DefaultDecisionName = (aiBetterSide == Direction.Top) ? "Fore side" : "Aft side";

            decisionSubphase.Start();
        }

        public override int GetActionPriority()
        {
            int resultFore = REINFORCE_BASE_PRIORITY + REINFORCE_PER_ENEMY_BONUS * ActionsHolder.CountEnemiesTargeting(Selection.ThisShip, 1);
            int resultAft  = REINFORCE_BASE_PRIORITY + REINFORCE_PER_ENEMY_BONUS * ActionsHolder.CountEnemiesTargeting(Selection.ThisShip, -1);

            aiBetterSide = (resultFore >= resultAft) ? Direction.Top : Direction.Bottom;

            return Mathf.Max(resultFore, resultAft);
        }

        private class ReinforceSideSubphase : DecisionSubPhase { }

    }

}