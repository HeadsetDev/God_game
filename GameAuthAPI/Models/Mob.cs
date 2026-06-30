namespace GameAuthAPI.Models
{
    public class Mob
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Health { get; set; }
        public int Damage { get; set; }

        // Внешний ключ для локации
        public int SpawnLocationId { get; set; }

        // Навигационное свойство для локации
        public Location SpawnLocation { get; set; } = null!;
    }
}