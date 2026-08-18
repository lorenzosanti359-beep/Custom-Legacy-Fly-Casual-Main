using Arcs;
using BoardTools;
using Ship;
using Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Upgrade;


namespace Ship
{
    namespace SecondEdition.CustomN1Starfighter
    {
        public class TheMandalorian : CustomN1Starfighter
        {
            public TheMandalorian() : base()
            {
                PilotInfo = new PilotCardInfo(
                    "The Mandalorian",
                    5,
                    40,
                    isLimited: true,
                    abilityText: "While you defend or perform a primary attack....",
                    abilityType: typeof(Abilities.SecondEdition.TheMandalorianNabooN1StarfighterAbility),
                    tags: new List<Tags>
                    {
                        Tags.Mandalorian,
                        Tags.BountyHunter
                    },
                    extraUpgradeIcon: UpgradeType.Talent,
                    factionOverride: Faction.Scum
                );

                PilotNameCanonical = "themandalorianmirror-customn1starfighter";
            }
        }
    }
}

namespace Abilities.SecondEdition
{
    public class TheMandalorianNabooN1StarfighterAbility : GenericAbility
    {
        public override void ActivateAbility()
        {
            AddDiceModification(
                HostShip.PilotInfo.PilotName,
                IsAvailable,
                GetAiPriority,
                DiceModificationType.Add,
                1,
                sideCanBeChangedTo: DieSide.Focus);
        }

        private bool IsAvailable()
        {
            return IsInFrontSectorOf2Ships();
       
        }

        private bool IsInFrontSectorOf2Ships()
        {
            int count = 0;

            foreach (GenericShip enemyShip in HostShip.Owner.EnemyShips.Values)
            {
                int rangeInFrontSector = HostShip.SectorsInfo.RangeToShipBySector(enemyShip, ArcType.Front);
                if (Combat.Defender.ShipId == HostShip.ShipId || (Combat.Attacker.ShipId == HostShip.ShipId && Combat.ChosenWeapon.WeaponType == Ship.WeaponTypes.PrimaryWeapon))
                {
                    if (rangeInFrontSector <= 2) //rangeInFrontSector >= 1 && 
                    {
                        count++;
                        if (count == 2) return true;
                    }
                }
            }

            return false;
        }

        private int GetAiPriority()
        {
            return 100; // Free change limited by side if 1
        }

        public override void DeactivateAbility()
        {
            RemoveDiceModification();
        }
    }
}
