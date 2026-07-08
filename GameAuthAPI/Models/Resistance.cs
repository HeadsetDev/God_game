using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameAuthAPI.Models
{
    public class Resistance
    {
        public int Id { get; set; }

        [NotMapped]
        public Dictionary<DamageType, int> DamageResistances { get; set; } = new();

        [NotMapped]
        public Dictionary<StatusEffectType, int> StatusResistances { get; set; } = new();

        public int PhysicalDefense { get; set; }
        public int MagicDefense { get; set; }
        public int DodgeChance { get; set; }

        [NotMapped]
        public int TotalPhysicalDefense => PhysicalDefense + (DamageResistances.ContainsKey(DamageType.Physical) ? DamageResistances[DamageType.Physical] / 2 : 0);

        [NotMapped]
        public int TotalMagicDefense => MagicDefense + (DamageResistances.ContainsKey(DamageType.Magic) ? DamageResistances[DamageType.Magic] / 2 : 0);
    }
}