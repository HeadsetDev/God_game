namespace GameAuthAPI.DTOs
{
    /// <summary>
    /// DTO для квеста.
    /// </summary>
    public class QuestDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Reward { get; set; }
        public Dictionary<string, int> Conditions { get; set; } = new();
        public bool IsCompleted { get; set; }
    }
}