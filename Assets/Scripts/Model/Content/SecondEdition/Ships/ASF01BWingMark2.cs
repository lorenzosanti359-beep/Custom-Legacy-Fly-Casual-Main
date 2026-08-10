using System.Collections;
using System.Collections.Generic;
using Movement;
using ActionsList;
using Upgrade;
using Actions;
using Arcs;
using System;
using BoardTools;
using UnityEngine;
using Ship;
using System.Linq;

namespace Ship.SecondEdition.ASF01BWingMark2
{
        public class ASF01BWingMark2 : GenericShip
        {
            public ASF01BWingMark2() : base()
            {
                
                ShipInfo = new ShipCardInfo
            (
                "A/SF-01 B-wing Mark II",
                BaseSize.Small,
                Faction.Resistance,
                new ShipArcsInfo(ArcType.Front, 3), 1, 4, 5,
                new ShipActionsInfo(
                    new ActionInfo(typeof(FocusAction)),
                    new ActionInfo(typeof(TargetLockAction)),
                    new ActionInfo(typeof(BarrelRollAction))
                ),
                new ShipUpgradesInfo(
                    UpgradeType.Modification,
                    UpgradeType.Sensor,
                    UpgradeType.Tech,
                    UpgradeType.Cannon,
                    UpgradeType.Cannon,
                    UpgradeType.Torpedo,
                    UpgradeType.Configuration
                )
            );

                ShipInfo.ActionIcons.AddLinkedAction(new LinkedActionInfo(typeof(FocusAction), typeof(BarrelRollAction)));
                ShipInfo.ActionIcons.AddLinkedAction(new LinkedActionInfo(typeof(BarrelRollAction), typeof(TargetLockAction)));

                //DefaultUpgrades.Add(typeof(UpgradesList.SecondEdition.StabilizedSFoilsOpen));


                DialInfo = new ShipDialInfo(
                  new ManeuverInfo(ManeuverSpeed.Speed1, ManeuverDirection.Left, ManeuverBearing.Turn, MovementComplexity.Complex),
                  new ManeuverInfo(ManeuverSpeed.Speed1, ManeuverDirection.Left, ManeuverBearing.Bank, MovementComplexity.Easy),
                  new ManeuverInfo(ManeuverSpeed.Speed1, ManeuverDirection.Forward, ManeuverBearing.Straight, MovementComplexity.Easy),
                  new ManeuverInfo(ManeuverSpeed.Speed1, ManeuverDirection.Right, ManeuverBearing.Bank, MovementComplexity.Easy),
                  new ManeuverInfo(ManeuverSpeed.Speed1, ManeuverDirection.Right, ManeuverBearing.Turn, MovementComplexity.Complex),

                  new ManeuverInfo(ManeuverSpeed.Speed2, ManeuverDirection.Left, ManeuverBearing.Turn, MovementComplexity.Normal),
                  new ManeuverInfo(ManeuverSpeed.Speed2, ManeuverDirection.Left, ManeuverBearing.Bank, MovementComplexity.Normal),
                  new ManeuverInfo(ManeuverSpeed.Speed2, ManeuverDirection.Forward, ManeuverBearing.Straight, MovementComplexity.Easy),
                  new ManeuverInfo(ManeuverSpeed.Speed2, ManeuverDirection.Right, ManeuverBearing.Bank, MovementComplexity.Normal),
                  new ManeuverInfo(ManeuverSpeed.Speed2, ManeuverDirection.Right, ManeuverBearing.Turn, MovementComplexity.Normal),
                  new ManeuverInfo(ManeuverSpeed.Speed3, ManeuverDirection.Forward, ManeuverBearing.KoiogranTurn, MovementComplexity.Complex),

                  new ManeuverInfo(ManeuverSpeed.Speed3, ManeuverDirection.Left, ManeuverBearing.Bank, MovementComplexity.Complex),
                  new ManeuverInfo(ManeuverSpeed.Speed3, ManeuverDirection.Right, ManeuverBearing.Bank, MovementComplexity.Complex),

                  new ManeuverInfo(ManeuverSpeed.Speed4, ManeuverDirection.Forward, ManeuverBearing.Straight, MovementComplexity.Normal),
                 new ManeuverInfo(ManeuverSpeed.Speed1, ManeuverDirection.Left, ManeuverBearing.TallonRoll, MovementComplexity.Complex),
                    new ManeuverInfo(ManeuverSpeed.Speed1, ManeuverDirection.Right, ManeuverBearing.TallonRoll, MovementComplexity.Complex),
                new ManeuverInfo(ManeuverSpeed.Speed3, ManeuverDirection.Forward, ManeuverBearing.Straight, MovementComplexity.Easy)

              );
             ModelInfo = new ShipModelInfo(
                    "B-wing",
                    "Teal",
                    new Vector3(-3.33f, 6.4f, 5.55f),
                    2f
                );
            IconicPilots = new Dictionary<Faction, System.Type> {
                { Faction.Resistance, typeof(BatuuRecruit) }
            };
            SoundInfo = new ShipSoundInfo(
               new List<string>()
               {
                    "XWing-Fly1",
                    "XWing-Fly2",
                    "XWing-Fly3"
               },
               "XWing-Laser", 2
                );
                ShipIconLetter = 'n';

               // ManeuversImageUrl = "https://vignette.wikia.nocookie.net/xwing-miniatures-second-edition/images/f/ff/Maneuver_b-wing.png";
            }
        }
}
