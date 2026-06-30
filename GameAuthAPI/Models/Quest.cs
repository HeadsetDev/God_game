using System.Collections.Generic;
using System.Text.Json;
using System.ComponentModel.DataAnnotations.Schema;
namespace GameAuthAPI.Models
{
    /// <summary>
    /// Модель квеста.
    /// </summary>
    public class Quest
    {
        public int Id { get; set; }

        /// <summary>
        /// Название квеста.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Описание квеста.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Награда за выполнение квеста.
        /// </summary>
        public int Reward { get; set; }

        /// <summary>
        /// Условия выполнения квеста (сериализованные в JSON).
        /// </summary>
        public string ConditionsJson { get; set; } = string.Empty;

        /// <summary>
        /// Статус выполнения квеста.
        /// </summary>
        public bool IsCompleted { get; set; } = false;

        /// <summary>
        /// Условия выполнения квеста (десериализованные).
        /// </summary>
        
        public bool IsGroupQuest { get; set; } // Новое поле: является ли квест групповым
        public int RequiredPlayers { get; set; } // Новое поле: количество игроков для группового квеста

        // Навигационные свойства
        public List<QuestParticipant> QuestParticipants { get; set; } = new List<QuestParticipant>();
        [NotMapped] // Указываем EF игнорировать это свойство
        public Dictionary<string, int> Conditions
        {
            get => JsonSerializer.Deserialize<Dictionary<string, int>>(ConditionsJson) ?? new Dictionary<string, int>();
            set => ConditionsJson = JsonSerializer.Serialize(value);
        }
    }
}