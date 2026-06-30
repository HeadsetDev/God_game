namespace GameAuthAPI.Models
{
    public class Location
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // Список связанных локаций
        public List<Location> ConnectedLocations { get; set; } = new();
    }
}