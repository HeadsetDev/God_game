namespace GameAuthAPI.Models
{
    public class PlayerSkill
    {
        public int PlayerId { get; set; }
        public Player Player { get; set; }

        public int SkillId { get; set; }
        public Skill Skill { get; set; }

        public bool IsLearned { get; set; } = true;
    }
}