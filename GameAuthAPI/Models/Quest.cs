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
        /// Является ли квест групповым.
        /// </summary>
        public bool IsGroupQuest { get; set; }

        /// <summary>
        /// Количество игроков для группового квеста.
        /// </summary>
        public int RequiredPlayers { get; set; }

        // Навигационные свойства
        public List<QuestParticipant> QuestParticipants { get; set; } = new List<QuestParticipant>();

        /// <summary>
        /// Условия выполнения квеста (десериализованные).
        /// </summary>
        [NotMapped]
        public Dictionary<string, int> Conditions
        {
            get
            {
                if (string.IsNullOrEmpty(ConditionsJson))
                    return new Dictionary<string, int>();

                try
                {
                    return JsonSerializer.Deserialize<Dictionary<string, int>>(ConditionsJson) ?? new Dictionary<string, int>();
                }
                catch
                {
                    return new Dictionary<string, int>();
                }
            }
            set => ConditionsJson = JsonSerializer.Serialize(value);
        }
    }
}