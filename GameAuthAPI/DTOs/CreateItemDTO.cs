namespace GameAuthAPI.DTOs
{
    public class CreateItemDto
    {
        public string Name { get; set; } = string.Empty;
        public string Rarity { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsPartOfSet { get; set; }
        public bool IsQuestItem { get; set; }
        public bool CanBeUpgraded { get; set; }
        public Dictionary<string, double> Stats { get; set; } = new();
    }
}