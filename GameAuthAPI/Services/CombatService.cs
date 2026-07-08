using GameAuthAPI.Models;

namespace GameAuthAPI.Services
{
    public class CombatService
    {
        private readonly Random _random = new();

        public int CalculateDamage(Player attacker, IDamageable target, bool isPvP, Skill? skill = null)
        {
            var damageType = skill?.DamageType ?? DamageType.Physical;
            var baseAttack = GetBaseAttack(attacker, skill);
            var resistance = GetResistance(target);
            var defense = GetDefense(target, damageType);

            var damageResist = resistance.DamageResistances.ContainsKey(damageType) ? resistance.DamageResistances[damageType] : 0;
            var damageReduction = 1.0 - (damageResist / 100.0);

            var damage = (int)((baseAttack - defense) * damageReduction);

            if (IsPhysicalDamage(damageType))
                damage = (int)(damage * (1 - resistance.TotalPhysicalDefense / (resistance.TotalPhysicalDefense + 100.0)));
            else
                damage = (int)(damage * (1 - resistance.TotalMagicDefense / (resistance.TotalMagicDefense + 100.0)));

            if (IsDodged(attacker, resistance))
                return 0;

            if (IsCritical(attacker))
                damage = (int)(damage * GetCriticalMultiplier(attacker));

            if (isPvP)
                damage = (int)(damage * 0.7);

            return damage > 0 ? damage : 1;
        }

        public bool ApplyStatusEffect(Player attacker, IDamageable target, StatusEffectType effect, int chance = 100)
        {
            var resistance = GetResistance(target);
            var resistChance = resistance.StatusResistances.ContainsKey(effect) ? resistance.StatusResistances[effect] : 0;
            var finalChance = chance - resistChance;
            return _random.NextDouble() * 100 < finalChance;
        }

        private int GetBaseAttack(Player attacker, Skill? skill)
        {
            var strength = attacker.TotalStrength;
            var agility = attacker.TotalAgility;
            var intelligence = attacker.TotalIntelligence;

            if (skill != null)
            {
                return skill.Damage + (int)(strength * 0.5) + (int)(agility * 0.3) + (int)(intelligence * 0.2);
            }

            return 5 + strength * 2 + agility * 1 + intelligence * 1;
        }

        private Resistance GetResistance(IDamageable target)
        {
            if (target is Player player)
                return player.TotalResistance;
            return new Resistance();
        }

        private int GetDefense(IDamageable target, DamageType damageType)
        {
            if (target is Player player)
            {
                return IsPhysicalDamage(damageType)
                    ? player.TotalResistance.TotalPhysicalDefense
                    : player.TotalResistance.TotalMagicDefense;
            }

            if (target is Mob mob)
                return mob.Defense;

            return 0;
        }

        private bool IsPhysicalDamage(DamageType damageType)
        {
            return damageType == DamageType.Physical ||
                   damageType == DamageType.Slashing ||
                   damageType == DamageType.Piercing ||
                   damageType == DamageType.Blunt ||
                   damageType == DamageType.Range ||
                   damageType == DamageType.Dagger;
        }

        private bool IsDodged(Player attacker, Resistance resistance)
        {
            return _random.NextDouble() * 100 < resistance.DodgeChance;
        }

        private bool IsCritical(Player player)
        {
            var critChance = 5 + player.TotalAgility * 0.5 + player.TotalPerception * 0.3;
            return _random.NextDouble() * 100 < critChance;
        }

        private double GetCriticalMultiplier(Player player)
        {
            return 1.5 + (player.TotalPerception * 0.5 + player.TotalStrength * 0.2) / 100;
        }
    }
}