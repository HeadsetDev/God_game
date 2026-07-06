namespace GameAuthAPI.Models
{
    public class CraftRecipe
    {
        public int Id { get; set; }
        public string ResultItem { get; set; } = string.Empty;
        public Dictionary<string, int> RequiredResources { get; set; } = new();
        public List<CraftRequirement> Requirements { get; set; } = new();
    }

    public class CraftRequirement
    {
        public string Type { get; set; } = "Level"; // Level, CraftSkill, Achievement, Rank
        public int Value { get; set; }
        public string? AdditionalData { get; set; } // для ачивок — название, для звания — название
    }
}