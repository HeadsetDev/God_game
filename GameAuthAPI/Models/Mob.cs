namespace GameAuthAPI.Models
{
    public class Mob : IDamageable
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Health { get; set; }
        public int Damage { get; set; }
        public int Defense { get; set; } = 5;
        public int ExperienceReward { get; set; } = 10;
        public int Level { get; set; } = 1;

        // Внешний ключ для локации
        public int SpawnLocationId { get; set; }

        // Навигационное свойство для локации
        public Location SpawnLocation { get; set; } = null!;
    }
}