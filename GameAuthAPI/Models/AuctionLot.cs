namespace GameAuthAPI.Models
{
    public class AuctionLot
    {
        public int Id { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal StartingPrice { get; set; }
        public string Seller { get; set; } = string.Empty;
        public DateTime EndTime { get; set; }
    }
}