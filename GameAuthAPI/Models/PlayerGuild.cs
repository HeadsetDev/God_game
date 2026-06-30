namespace GameAuthAPI.Models
{
    public class PlayerGuild
    {
        public int PlayerId { get; set; }
        public Player Player { get; set; }

        public int GuildId { get; set; }
        public Guild Guild { get; set; }

        public string Role { get; set; } // Leader, Member и т.д.
    }
}