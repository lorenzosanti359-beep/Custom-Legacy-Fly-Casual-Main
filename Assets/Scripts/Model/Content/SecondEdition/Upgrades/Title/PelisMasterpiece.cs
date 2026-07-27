using Upgrade;
using System.Collections.Generic;
using Actions;
using ActionsList;
using Tokens;
using System.Linq;
using SubPhases;
using Abilities.SecondEdition;
using Ship;
using Abilities;
using Content;
using Arcs;
using BoardTools;

namespace UpgradesList.SecondEdition
{
    public class PelisMasterpiece : GenericUpgrade
    {
        public PelisMasterpiece() : base()
        {
            UpgradeInfo = new UpgradeCardInfo
            (
                "Peli's Masterpiece",
                UpgradeType.Title,
                cost: 7,
                isLimited: true,
                addSlot: new UpgradeSlot(UpgradeType.TacticalRelay),
                restrictions: new UpgradeCardRestrictions
                (
                    new FactionRestriction(Faction.Scum),
                    new ShipRestriction(typeof(Ship.SecondEdition.CustomN1Starfighter.CustomN1Starfighter))
                ),
                charges: 2,
                addAction: new ActionInfo(typeof(SlamAction)),
                abilityType: typeof(Abilities.SecondEdition.PelisMasterpieceAbility)
                
            );
            NameCanonical = "pelismasterpiece";
        }        
    }
}
namespace Abilities.SecondEdition
{
    public class PelisMasterpieceAbility : GenericAbility
    {
        public override void ActivateAbility()
        {
            HostShip.OnTryAddAction += RestrictSlam;
            HostShip.OnSlam += LoseCharge;
            HostShip.AfterGotNumberOfAttackDice += CheckForExtraDie;
        }

        public override void DeactivateAbility()
        {
            HostShip.OnTryAddAction -= RestrictSlam;
            HostShip.OnSlam -= LoseCharge;
            HostShip.AfterGotNumberOfAttackDice -= CheckForExtraDie;
        }

        private void RestrictSlam(GenericShip ship, GenericAction action, ref bool canBeUsed)
        {
            if (action is SlamAction)
            {
                if (canBeUsed) canBeUsed = HostUpgrade.State.Charges > 0;
            }
        }
        private void LoseCharge()
        {
            if (HostUpgrade.State.Charges > 0)
            {
                HostUpgrade.State.LoseCharge();
            }
        }
        private void CheckForExtraDie(ref int diceAmount)
        {
            if ((Combat.AttackStep == CombatStep.Attack
                && Combat.Attacker == HostShip
                && Combat.ChosenWeapon.WeaponType == WeaponTypes.PrimaryWeapon
                && Combat.Attacker.SectorsInfo.IsShipInSector(Combat.Defender, ArcType.Bullseye)))
            {

                    Messages.ShowInfo("Target is in bullseye arc, Peli's Masterpiece rolls +1 attack die");
                    diceAmount++;
            }
        }
        /*public override void ActivateAbilityForSquadBuilder()
        {
            HostShip.OnUpgradeEquipTagCheck += AllowChild;
        }

        public override void DeactivateAbilityForSquadBuilder()
        {
            HostShip.OnUpgradeEquipTagCheck -= AllowChild;
        }

        private void AllowChild(Tags tag, ref bool result)
        {
        }*/
    }
}