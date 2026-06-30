namespace GameAuthAPI.Models
{
    public class QuestParticipant
    {
        public int QuestId { get; set; }
        public Quest Quest { get; set; }

        public int PlayerId { get; set; }
        public Player Player { get; set; }
    }
}