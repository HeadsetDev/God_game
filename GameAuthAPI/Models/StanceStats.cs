namespace GameAuthAPI.Models
{
	public class StanceStats
	{
		public int StrengthBonus { get; set; }
		public int AgilityBonus { get; set; }
		public int IntelligenceBonus { get; set; }
		public int VitalityBonus { get; set; }
		public int WillpowerBonus { get; set; }
		public int PerceptionBonus { get; set; }

		public int HealthBonus { get; set; }
		public int ManaBonus { get; set; }
		public int DefenseBonus { get; set; }
		public int MagicResistBonus { get; set; }

		public StanceStats()
		{
			StrengthBonus = 0;
			AgilityBonus = 0;
			IntelligenceBonus = 0;
			VitalityBonus = 0;
			WillpowerBonus = 0;
			PerceptionBonus = 0;
			HealthBonus = 0;
			ManaBonus = 0;
			DefenseBonus = 0;
			MagicResistBonus = 0;
		}
	}
}