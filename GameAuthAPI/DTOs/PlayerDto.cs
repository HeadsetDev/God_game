namespace GameAuthAPI.DTOs
{
    public class PlayerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
        public int Coins { get; set; }
        public List<PlayerItemDto> PlayerItems { get; set; } = new();
    }

}