using System;
using System.Linq;
using BoardTools;
using Ship;
using SubPhases;
using Tokens;
using Upgrade;

namespace Ship
{
    namespace SecondEdition.ASF01BWingMark2
    {
        public class BlackSquadronVeteran : ASF01BWingMark2
        {
            public BlackSquadronVeteran() : base()
            {
                PilotInfo = new PilotCardInfo(
                    "Black Squadron Veteran",
                    3,
                    45,
                    extraUpgradeIcon: UpgradeType.Talent
                );
            }
        }
    }
}