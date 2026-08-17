using BoardTools;
using Ship;
using SubPhases;
using System;
using System.Collections.Generic;
using Upgrade;
using System.Linq;

namespace Ship
{
    namespace SecondEdition.NimbusClassVWing
    {
        public class Scrambler : NimbusClassVWing
        {
            public Scrambler() : base()
            {
                PilotInfo = new PilotCardInfo(
                    "\"Scrambler\"",
                    3,
                    30,
                    isLimited: true,
                    abilityType: typeof(Abilities.SecondEdition.ScramblerAbility),
                    extraUpgradeIcon: UpgradeType.Talent
                );
            }
        }
    }
}

namespace Abilities.SecondEdition
{
    public class ScramblerAbility : GenericAbility
    {
        public override void ActivateAbility()
        {
            AddDiceModification(
                HostShip.PilotInfo.PilotName,
                CanBeUsed,
                GetAiPriority,
                DiceModificationType.Reroll,
                2,
                isGlobal: true,
                payAbilityCost: SpendTargetLockOnAttacker
            );
        }

        private bool CanBeUsed()
        {
            if (!Tools.IsFriendly(Combat.Defender, HostShip)) return false;
            DistanceInfo distInfo = new DistanceInfo(HostShip, Combat.Attacker);
            if (distInfo.Range < 1 || distInfo.Range > 3) return false;
            return (Combat.AttackStep == CombatStep.Defence && ActionsHolder.HasTargetLockOn(HostShip, Combat.Attacker));
        }

        private int GetAiPriority()
        {
            return 85;
        }

        private void SpendTargetLockOnAttacker(Action<bool> callback)
        {
            if (ActionsHolder.HasTargetLockOn(HostShip, Combat.Attacker))
            {
                SpendTargetLock(delegate { callback(true); });
            }
            else
            {
                Messages.ShowError("Error: The attacker has no Target Lock to spend");
                callback(false);
            }
        }

        private void SpendTargetLock(Action callBack)
        {
            List<char> letters = ActionsHolder.GetTargetLocksLetterPairs(HostShip, Combat.Attacker);
            HostShip.Tokens.SpendToken(typeof(Tokens.BlueTargetLockToken), callBack, letters.First());
        }

        public override void DeactivateAbility()
        {
            RemoveDiceModification();
        }
    }
}