namespace GameAuthAPI.DTOs
{
    public class ItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Rarity { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsPartOfSet { get; set; }
        public bool IsQuestItem { get; set; }
        public bool CanBeUpgraded { get; set; }
        public int UpgradeLevel { get; set; }
        public int MaxUpgradeLevel { get; set; }
        public Dictionary<string, double> Stats { get; set; } = new();
        public List<AchievementDto> Achievements { get; set; } = new();
        public int PlayerKills { get; set; }
        public int BossKills { get; set; }
        public List<ItemStatsDto> AchievementBonuses { get; set; } = new();
    }

    public class AchievementDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
    }

    public class ItemStatsDto
    {
        public double Strength { get; set; }
        public double CriticalChance { get; set; }
    }
}