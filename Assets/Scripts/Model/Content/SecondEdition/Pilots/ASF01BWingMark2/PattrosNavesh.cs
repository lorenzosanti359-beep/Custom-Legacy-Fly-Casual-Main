using System;
using System.Linq;
using BoardTools;
using Ship;
using SubPhases;
using Tokens;
using Upgrade;
using Abilities.SecondEdition;
using Actions;
using ActionsList;
using Content;
using System.Collections.Generic;
using UnityEngine;

namespace Ship
{
    namespace SecondEdition.ASF01BWingMark2
    {
        public class PattrosNavesh : ASF01BWingMark2
        {
            public PattrosNavesh() : base()
            {
                PilotInfo = new PilotCardInfo(
                    "Pattros Navesh",
                    4,
                    51,
                    isLimited: true,
                    abilityText: "......",
                    abilityType: typeof(Abilities.SecondEdition.PattrosNaveshAbility),
                    extraUpgradeIcon: UpgradeType.Talent
                );
            }
        }
    }
}
namespace Abilities.SecondEdition
{
    public class PattrosNaveshAbility : GenericAbility
    {
        public override void ActivateAbility()
        {
            AddDiceModification(
                HostName,
                IsDiceModificationAvailable,
                GetDiceModificationPriority,
                DiceModificationType.Reroll,
                GetNumberOfFriendlyShipsAtRange2
            );
        }

        private bool IsDiceModificationAvailable()
        {
            bool result = false;
            if ((Combat.AttackStep == CombatStep.Attack))
            {
                if (GetNumberOfFriendlyShipsAtRange2() > 0 && (Combat.CurrentDiceRoll.Blanks + Combat.CurrentDiceRoll.Focuses) > 0) result = true;
            }
            return result;

        }

        private int GetNumberOfFriendlyShipsAtRange2()
        {
            //return BoardTools.Board.GetShipsAtRange(HostShip, new UnityEngine.Vector2(0, 2), Team.Type.Friendly).Count;
            int count = 0;
            List<GenericShip> friendlyShipsAtRange = Board.GetShipsAtRange(HostShip, new Vector2(0, 2), Team.Type.Friendly);

            foreach (GenericShip ship in friendlyShipsAtRange)
            {
                if (ship.IsStressed || ship.IsStrained)
                {
                    count++;
                }
            }
            return count;
        }

        private int GetDiceModificationPriority()
        {
            return 90;
        }

        public override void DeactivateAbility()
        {
            RemoveDiceModification();
        }
    }
}