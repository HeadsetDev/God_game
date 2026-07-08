using System.Collections.Generic;

namespace GameAuthAPI.Models
{
    public class Skill
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = "Active"; // Active, Passive, Ultimate
        public int Damage { get; set; }
        public int ManaCost { get; set; }
        public int Cooldown { get; set; } // в секундах
        public int RequiredLevel { get; set; }
        public string Effect { get; set; } = string.Empty; // JSON с доп. эффектами

        // ========== НОВЫЕ ПОЛЯ ==========
        public DamageType DamageType { get; set; } = DamageType.Physical;
        public StatusEffectType? StatusEffect { get; set; }
        public int StatusChance { get; set; } = 50; // шанс наложения эффекта (0-100)
        public StanceType? RequiredStance { get; set; } // null = доступен всегда

        // Навигационное свойство
        public List<PlayerSkill> PlayerSkills { get; set; } = new();
    }
}