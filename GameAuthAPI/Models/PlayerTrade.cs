using System.Collections.Generic;

namespace GameAuthAPI.Models
{
    public class PlayerTrade
    {
        public int Id { get; set; }
        public int Player1Id { get; set; }
        public Player? Player1 { get; set; }
        public int Player2Id { get; set; }
        public Player? Player2 { get; set; }

        public List<PlayerItem> Player1Items { get; set; } = new List<PlayerItem>();
        public List<PlayerItem> Player2Items { get; set; } = new List<PlayerItem>();

        public bool IsConfirmedByPlayer1 { get; set; } = false;
        public bool IsConfirmedByPlayer2 { get; set; } = false;
        public bool IsCompleted { get; set; } = false;
    }
}