using System;
using System.Linq;
using BoardTools;
using Ship;
using SubPhases;
using Tokens;
using Upgrade;
using Arcs;
using BoardTools;
using Content;
using System.Collections.Generic;
using UnityEngine;

namespace Ship
{
    namespace SecondEdition.ASF01BWingMark2
    {
        public class EnananSupa : ASF01BWingMark2
        {
            public EnananSupa() : base()
            {
                PilotInfo = new PilotCardInfo(
                    "Enanan Supa",
                    4,
                    48,
                    isLimited: true,
                    abilityText: "........",
                    abilityType: typeof(Abilities.SecondEdition.EnananSupaAbility),
                    extraUpgradeIcon: UpgradeType.Talent
                );
            }
        }
    }
}

namespace Abilities.SecondEdition
{
    public class EnananSupaAbility : GenericAbility
    {
        private GenericShip PreviousCurrentShip { get; set; }
        private int NumberOfShipsToUseAbility { get; set; }

        public override void ActivateAbility()
        {
            HostShip.OnCombatActivation += CheckEnananSupaAbility;
        }

        public override void DeactivateAbility()
        {
            HostShip.OnCombatActivation -= CheckEnananSupaAbility;
        }

        private void CheckEnananSupaAbility(GenericShip ship)
        {
            int counted = HasEnoughTargets();
            if (counted > 0 )
            {
                Messages.ShowInfo($"Ability triggers {counted}");
                NumberOfShipsToUseAbility = counted;
                RegisterAbilityTrigger(TriggerTypes.OnCombatActivation, AskToUseOwnAbility);
            }
        }
        private int HasEnoughTargets()
        {
            int count = 0;

            foreach (GenericShip TurretShip in HostShip.Owner.Ships.Values)
            {
                var turretArcs = TurretShip.ArcsInfo.Arcs.Where(arc => arc is ArcSingleTurret || arc is ArcDualTurretA || arc is ArcDualTurretB);
                if (turretArcs.Any(arc => new ShotInfoArc(TurretShip, HostShip, arc).InArc))
                {
                    count++;
                }
            }
            foreach (GenericShip TurretShip in HostShip.Owner.EnemyShips.Values)
            {
                var turretArcs = TurretShip.ArcsInfo.Arcs.Where(arc => arc is ArcSingleTurret || arc is ArcDualTurretA || arc is ArcDualTurretB);
                if (turretArcs.Any(arc => new ShotInfoArc(TurretShip, HostShip, arc).InArc))
                {
                    count++;
                }
            }
            return count;
        }
        private void AskToUseOwnAbility(object sender, EventArgs e)
        {
            AskToUseAbility(
                HostShip.PilotInfo.PilotName,
                NeverUseByDefault,
                StartMultiSelectionSubphase,
                descriptionLong: "Do you want to select ships to give strain?",
                imageHolder: HostShip
            );
        }
        private void StartMultiSelectionSubphase(object sender, EventArgs e)
        {
            DecisionSubPhase.ConfirmDecisionNoCallback();
            MultiSelectionSubphase subphase = Phases.StartTemporarySubPhaseNew<MultiSelectionSubphase>("Enanan Supa", Phases.CurrentSubPhase.CallBack);

            subphase.RequiredPlayer = HostShip.Owner.PlayerNo;

            subphase.Filter = FilterSelection;
            subphase.GetAiPriority = GetAiPriority;
            subphase.MaxToSelect = NumberOfShipsToUseAbility;
            subphase.WhenDone = GetStrain;

            subphase.DescriptionShort = "Enanan Supa";
            subphase.DescriptionLong = "Enemy ship in front half may get strain token";
            subphase.ImageSource = HostShip;

            subphase.Start();
        }
        private int GetAiPriority(GenericShip ship)
        {
            // Never use ability
            return 0;
        }
        private void GetStrain(Action callback)
        {
            //int forceToSpend = 0;
            foreach (GenericShip ship in Selection.MultiSelectedShips)
            {
                //Roster.ToggleManeuverVisibility(ship, true);
                ship.Tokens.AssignToken(typeof(StrainToken), delegate { });
                //forceToSpend++;
                //Messages.ShowInfo(string.Format("{0}: Dial of {1} is flipped faceup", HostUpgrade.UpgradeInfo.Name, ship.PilotInfo.PilotName));
            }
            //HostShip.State.SpendForce(forceToSpend, callback);
            callback();
        }
        private bool FilterSelection(GenericShip ship)
        {
            if (Tools.IsSameTeam(ship, HostShip)) return false;

            if (!HostShip.SectorsInfo.IsShipInSector(ship, Arcs.ArcType.FullFront)) return false;

            return true;
        }

    }
}