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
        public class BatuuRecruit : ASF01BWingMark2
        {
            public BatuuRecruit() : base()
            {
                PilotInfo = new PilotCardInfo(
                    "Batuu Recruit",
                    2,
                    43
                );
            }
        }
    }
}