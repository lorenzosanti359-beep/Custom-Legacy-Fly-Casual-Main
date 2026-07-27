using System;
using System.Collections;
using BoardTools;
using UnityEngine;
using System.Collections.Generic;
using Actions;
using ActionsList;
using Arcs;
using Movement;
using Ship;
using Upgrade;
using System.Linq;

namespace Ship.SecondEdition.CustomN1Starfighter
{
    public class CustomN1Starfighter : GenericShip
    {
        public CustomN1Starfighter() : base()
        {
            ShipInfo = new ShipCardInfo
            (
                "Custom N-1 Starfighter",
                BaseSize.Small,
                Faction.Scum,
                new ShipArcsInfo(ArcType.Front, 2), 2, 3, 2,
                new ShipActionsInfo(
                    new ActionInfo(typeof(FocusAction)),
                    new ActionInfo(typeof(TargetLockAction)),
                    new ActionInfo(typeof(BarrelRollAction)),
                    new ActionInfo(typeof(BoostAction))
                ),
                new ShipUpgradesInfo(
                    UpgradeType.Sensor,
                    UpgradeType.Torpedo,
                    UpgradeType.Astromech,
                    UpgradeType.Modification,
                    UpgradeType.Title
                ),
                abilityText: "<b>Black Market Modifications:</b> You may equip 1 or upgrade."
            );

            ShipInfo.ActionIcons.AddLinkedAction(new LinkedActionInfo(typeof(BarrelRollAction), typeof(EvadeAction), ActionColor.Red));
            ShipInfo.ActionIcons.AddLinkedAction(new LinkedActionInfo(typeof(BoostAction), typeof(EvadeAction), ActionColor.Red));

            IconicPilots = new Dictionary<Faction, System.Type> {
                    { Faction.Scum, typeof(TheMandalorian) }
                };

            ModelInfo = new ShipModelInfo(
                "Naboo Royal N-1 Starfighter",
                "Yellow",
                new Vector3(-3.75f, 8f, 5.55f),
                1.1f
            );

            DialInfo = new ShipDialInfo(
                new ManeuverInfo(ManeuverSpeed.Speed1, ManeuverDirection.Left, ManeuverBearing.Bank, MovementComplexity.Normal),
                new ManeuverInfo(ManeuverSpeed.Speed1, ManeuverDirection.Forward, ManeuverBearing.Straight, MovementComplexity.Complex),
                new ManeuverInfo(ManeuverSpeed.Speed1, ManeuverDirection.Right, ManeuverBearing.Bank, MovementComplexity.Normal),

                new ManeuverInfo(ManeuverSpeed.Speed2, ManeuverDirection.Left, ManeuverBearing.Turn, MovementComplexity.Normal),
                new ManeuverInfo(ManeuverSpeed.Speed2, ManeuverDirection.Left, ManeuverBearing.Bank, MovementComplexity.Easy),
                new ManeuverInfo(ManeuverSpeed.Speed2, ManeuverDirection.Forward, ManeuverBearing.Straight, MovementComplexity.Easy),
                new ManeuverInfo(ManeuverSpeed.Speed2, ManeuverDirection.Right, ManeuverBearing.Bank, MovementComplexity.Easy),
                new ManeuverInfo(ManeuverSpeed.Speed2, ManeuverDirection.Right, ManeuverBearing.Turn, MovementComplexity.Normal),

                new ManeuverInfo(ManeuverSpeed.Speed3, ManeuverDirection.Left, ManeuverBearing.Turn, MovementComplexity.Normal),
                new ManeuverInfo(ManeuverSpeed.Speed3, ManeuverDirection.Left, ManeuverBearing.Bank, MovementComplexity.Easy),
                new ManeuverInfo(ManeuverSpeed.Speed3, ManeuverDirection.Forward, ManeuverBearing.Straight, MovementComplexity.Easy),
                new ManeuverInfo(ManeuverSpeed.Speed3, ManeuverDirection.Right, ManeuverBearing.Bank, MovementComplexity.Easy),
                new ManeuverInfo(ManeuverSpeed.Speed3, ManeuverDirection.Right, ManeuverBearing.Turn, MovementComplexity.Normal),
                new ManeuverInfo(ManeuverSpeed.Speed3, ManeuverDirection.Left, ManeuverBearing.KoiogranTurn, MovementComplexity.Complex),
                new ManeuverInfo(ManeuverSpeed.Speed3, ManeuverDirection.Right, ManeuverBearing.KoiogranTurn, MovementComplexity.Complex),

                new ManeuverInfo(ManeuverSpeed.Speed4, ManeuverDirection.Forward, ManeuverBearing.Straight, MovementComplexity.Normal),

                new ManeuverInfo(ManeuverSpeed.Speed5, ManeuverDirection.Forward, ManeuverBearing.Straight, MovementComplexity.Normal)
            );

            SoundInfo = new ShipSoundInfo(
                new List<string>()
                {
                    "XWing-Fly1",
                    "XWing-Fly2",
                    "XWing-Fly3"
                },
                "XWing-Laser", 2
            );

            ShipAbilities.Add(new Abilities.SecondEdition.BlackMarketModificationsAbility());

            ShipIconLetter = '<';
        }
    }
}
namespace Abilities.SecondEdition
{
    public class BlackMarketModificationsAbility : GenericAbility
    {
        private readonly List<UpgradeType> HardpointSlotTypes = new List<UpgradeType>
        {
            UpgradeType.Illicit,
            UpgradeType.Modification
        };

        public override void ActivateAbilityForSquadBuilder()
        {
            foreach (UpgradeType upgradeType in HardpointSlotTypes)
            {
                //HostShip.ShipInfo.UpgradeIcons.Upgrades.Add(upgradeType);
                HostShip.UpgradeBar.AddSlot(upgradeType);
            };

            HostShip.OnPreInstallUpgrade += OnPreInstallUpgrade;
            HostShip.OnRemovePreInstallUpgrade += OnRemovePreInstallUpgrade;
        }
        public override void ActivateAbility()
        {
        }

        public override void DeactivateAbility()
        {
        }

        public override void DeactivateAbilityForSquadBuilder() { }

        private void OnPreInstallUpgrade(GenericUpgrade upgrade)
        {
            if (HardpointSlotTypes.Contains(upgrade.UpgradeInfo.UpgradeTypes.First()))
            {
                HardpointSlotTypes
                    .Where(slot => slot != upgrade.UpgradeInfo.UpgradeTypes.First())
                    .ToList()
                    .ForEach(slot => HostShip.UpgradeBar.RemoveSlot(slot));
            }
        }

        private void OnRemovePreInstallUpgrade(GenericUpgrade upgrade)
        {
            if (HardpointSlotTypes.Contains(upgrade.UpgradeInfo.UpgradeTypes.First()))
            {
                HardpointSlotTypes
                    .Where(slot => slot != upgrade.UpgradeInfo.UpgradeTypes.First())
                    .ToList()
                    .ForEach(slot => HostShip.UpgradeBar.AddSlot(slot));
            }
        }
    }
}