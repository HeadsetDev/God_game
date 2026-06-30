using System.Collections.Generic;

namespace GameAuthAPI.Models
{
    public class GroupQuest
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int RequiredPlayers { get; set; }
        public int Reward { get; set; }

        // Навигационные свойства
        public List<QuestParticipant> QuestParticipants { get; set; } = new List<QuestParticipant>();
    }
}