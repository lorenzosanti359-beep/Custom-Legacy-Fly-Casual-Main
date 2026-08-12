using Abilities.SecondEdition;
using Upgrade;
using System.Collections.Generic;
using Content;

namespace Ship
{
    namespace SecondEdition.EWing
    {
        public class LukeSkywalker : EWing
        {
            public LukeSkywalker() : base()
            {
                PilotInfo = new PilotCardInfo(
                    "Luke Skywalker",
                    6,
                    100,
                    isLimited: true,
                    abilityType: typeof(LukeSkywalkerAbility),
                    tags: new List<Tags>
                    {
                        Tags.LightSide
                    },
                    force: 3,
                    extraUpgradeIcon: UpgradeType.ForcePower
                );
                PilotNameCanonical = "lukeskywalker-ewing";
                ModelInfo.SkinName = "Luke Skywalker";
            }
        }
    }
}
