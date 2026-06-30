namespace GameAuthAPI.Models
{
    public class PlayerItem
    {
        public int PlayerId { get; set; }
        public Player? Player { get; set; }
        public int ItemId { get; set; }
        public Item? Item { get; set; }
        public int Quantity { get; set; }
        public bool IsEquipped { get; set; } = false;
    }
}