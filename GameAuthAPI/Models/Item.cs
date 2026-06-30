using System.Collections.Generic;

namespace GameAuthAPI.Models
{
    public class Item
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ItemRarity Rarity { get; set; }
        public ItemType Type { get; set; }
        public EquipmentSlot Slot { get; set; } 
        public bool IsPartOfSet { get; set; }
        public bool IsQuestItem { get; set; }
        public bool CanBeUpgraded { get; set; }
        public int UpgradeLevel { get; set; }
        public int MaxUpgradeLevel { get; set; }
        public Dictionary<string, double> Stats { get; set; } = new();
        public List<Achievement> Achievements { get; set; } = new();
        public int PlayerKills { get; set; }
        public int BossKills { get; set; }
        public List<ItemStats> AchievementBonuses { get; set; } = new();
    }
}