namespace GameAuthAPI.DTOs
{
	public class PlayerItemDto
	{
		public int PlayerId { get; set; }
		public int ItemId { get; set; }
		public int Quantity { get; set; }
		public bool IsEquipped { get; set; } // Новое поле
	}
}