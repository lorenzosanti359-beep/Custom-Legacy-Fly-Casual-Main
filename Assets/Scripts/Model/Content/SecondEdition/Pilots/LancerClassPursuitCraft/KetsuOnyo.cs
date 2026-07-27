using Upgrade;
using Content;
using System.Collections.Generic;

namespace Ship
{
    namespace SecondEdition.LancerClassPursuitCraft
    {
        public class KetsuOnyo : LancerClassPursuitCraft
        {
            public KetsuOnyo() : base()
            {
                PilotInfo = new PilotCardInfo(
                    "Ketsu Onyo",
                    5,
                    66,
                    isLimited: true,
                    abilityType: typeof(Abilities.FirstEdition.KetsuOnyoPilotAbility),
                    tags: new List<Tags>
                    {
                        Tags.Mandalorian,
                        Tags.BountyHunter
                    },
                    extraUpgradeIcon: UpgradeType.Talent
                );
            }
        }
    }
}