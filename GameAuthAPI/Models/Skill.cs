using System.ComponentModel.DataAnnotations;

namespace GameAuthAPI.Models
{
    public class Skill
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Type { get; set; } = "Active"; // Active, Passive, Ultimate

        public int Damage { get; set; }

        public int ManaCost { get; set; }

        public int Cooldown { get; set; } // в секундах

        public int RequiredLevel { get; set; }

        public string Effect { get; set; } = string.Empty; // JSON с эффектами

        // Навигационное свойство для связи с игроками
        public List<PlayerSkill> PlayerSkills { get; set; } = new();
    }
}