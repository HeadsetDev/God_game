namespace GameAuthAPI.Models
{
    public class NPC
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Dialogue { get; set; } = string.Empty;

        // Внешний ключ для локации
        public int SpawnLocationId { get; set; }

        // Навигационное свойство для локации
        public Location SpawnLocation { get; set; } = null!;
    }
}