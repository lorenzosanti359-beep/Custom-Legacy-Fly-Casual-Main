using Content;
using Ship;
using SubPhases;
using System;
using System.Collections.Generic;
using Tokens;
using Upgrade;
using ActionsList;
using BoardTools;
using Movement;

namespace Ship.SecondEdition.DroidTriFighter
{
    public class DIS067 : DroidTriFighter
    {
        public DIS067()
        {
            PilotInfo = new PilotCardInfo(
                "DIS-067",
                4,
                38,
                limited: 2,
                extraUpgradeIcon: UpgradeType.Talent,
                abilityType: typeof(Abilities.SecondEdition.DIS067Ability),
                tags: new List<Tags>
                {
                    Tags.Droid
                }
            );
        }
    }
}

namespace Abilities.SecondEdition
{
    public class DIS067Ability : GenericAbility
    {
        public override void ActivateAbility()
        {
            HostShip.OnGetAvailableBarrelRollTemplates += ChangeBarrelRollTemplates;
        }

        public override void DeactivateAbility()
        {
            HostShip.OnGetAvailableBarrelRollTemplates -= ChangeBarrelRollTemplates;
        }

        private void ChangeBarrelRollTemplates(List<ManeuverTemplate> availableTemplates, GenericAction action)
        {
            availableTemplates.Add(new ManeuverTemplate(ManeuverBearing.Turn, ManeuverDirection.Left, ManeuverSpeed.Speed1));
            availableTemplates.Add(new ManeuverTemplate(ManeuverBearing.Turn, ManeuverDirection.Right, ManeuverSpeed.Speed1));
            availableTemplates.RemoveAll(n => n.Name == "Straight 1");
        }
    }
}
