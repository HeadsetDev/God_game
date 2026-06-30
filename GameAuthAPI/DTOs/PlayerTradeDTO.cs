namespace GameAuthAPI.DTOs
{
    public class PlayerTradeDto
    {
        public int Id { get; set; }
        public int Player1Id { get; set; }
        public int Player2Id { get; set; }
        public List<int> Player1ItemIds { get; set; } = new();
        public List<int> Player2ItemIds { get; set; } = new();
        public bool IsConfirmedByPlayer1 { get; set; }
        public bool IsConfirmedByPlayer2 { get; set; }
        public bool IsCompleted { get; set; }
    }
}