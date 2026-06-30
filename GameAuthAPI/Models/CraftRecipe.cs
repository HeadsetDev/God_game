namespace GameAuthAPI.Models
{
    public class CraftRecipe
    {
        public int Id { get; set; }
        public string ResultItem { get; set; } = string.Empty;
        public Dictionary<string, int> RequiredResources { get; set; } = new();
    }
}