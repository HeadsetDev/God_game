using System;

namespace GameAuthAPI.Models
{
    public class Duel
    {
        public int Id { get; set; }
        public int ChallengerId { get; set; }
        public Player Challenger { get; set; } = null!;

        public int OpponentId { get; set; }
        public Player Opponent { get; set; } = null!;

        public DuelStatus Status { get; set; } = DuelStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }

        public int? WinnerId { get; set; }
    }

    public enum DuelStatus
    {
        Pending,    // Ожидает принятия
        Active,     // Идёт бой
        Finished,   // Завершён
        Cancelled   // Отменён
    }
}