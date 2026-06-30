using System;
using System.Collections.Generic;

namespace GameAuthAPI.Models
{
    public class Guild
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int LeaderId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Навигационные свойства
        public List<PlayerGuild> PlayerGuilds { get; set; } = new List<PlayerGuild>();
    }
}