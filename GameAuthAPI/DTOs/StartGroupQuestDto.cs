namespace GameAuthAPI.DTOs
{
    public class StartGroupQuestDto
    {
        public int QuestId { get; set; }
        public List<int> PlayerIds { get; set; }
    }
}