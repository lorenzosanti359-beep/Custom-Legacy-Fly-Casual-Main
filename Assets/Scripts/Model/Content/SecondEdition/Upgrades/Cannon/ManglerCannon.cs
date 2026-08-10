using Tokens;
using Upgrade;
using Ship;
using System.Collections.Generic;
using Arcs;

namespace UpgradesList.SecondEdition
{
    public class ManglerCannon : GenericSpecialWeapon
    {
        public ManglerCannon() : base()
        {
            UpgradeInfo = new UpgradeCardInfo(
                "\"Mangler\" Cannon",
                UpgradeType.Cannon,
                cost: 5,
                weaponInfo: new SpecialWeaponInfo(
                    attackValue: 3,
                    minRange: 2,
                    maxRange: 3,
                    arc: ArcType.Front,
                    requiresToken: typeof(BlueTargetLockToken)
                ),
                abilityType: typeof(Abilities.SecondEdition.ManglerCannonDamageAbility)
            );

            NameCanonical = "manglerCannon";
        }
    }
}


namespace Abilities.SecondEdition
{
    //Attack (Lock):  If this attack hits, the defender suffers 1 crit damage and gains 1 strain. 
    //Then cancel all hit / crit results.
    public class ManglerCannonDamageAbility : GenericAbility
    {
        public override void ActivateAbility()
        {
            HostShip.OnShotHitAsAttacker += RegisterWeaponEffect;
        }

        public override void DeactivateAbility()
        {
            HostShip.OnShotHitAsAttacker -= RegisterWeaponEffect;
        }

        protected void RegisterWeaponEffect()
        {
            if (Combat.ChosenWeapon == HostUpgrade)
            {
                Triggers.RegisterTrigger(new Trigger()
                {
                    Name = "Mangler weapon effect",
                    TriggerType = TriggerTypes.OnShotHit,
                    TriggerOwner = Combat.Attacker.Owner.PlayerNo,
                    EventHandler = WeaponEffect
                });
            }
        }

        protected void WeaponEffect(object sender, System.EventArgs e)
        {
            Combat.DiceRollAttack.CancelAllResults();
            Combat.DiceRollAttack.RemoveAllFailures();

            DamageSourceEventArgs weaponDamage = new DamageSourceEventArgs()
            {
                Source = HostShip,
                DamageType = DamageTypes.ShipAttack
            };

            Combat.Defender.Damage.TryResolveDamage(0, weaponDamage, AssignTokens, 1);                     
        }

        protected void AssignTokens()
        {
            Combat.Defender.Tokens.AssignToken(typeof(StrainToken), Triggers.FinishTrigger);
        }
    }

}