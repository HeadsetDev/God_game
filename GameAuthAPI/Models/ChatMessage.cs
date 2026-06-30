using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameAuthAPI.Models
{
    /// <summary>
    /// Модель сообщения чата.
    /// </summary>
    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID отправителя.
        /// </summary>
        public int SenderId { get; set; }

        /// <summary>
        /// Отправитель сообщения.
        /// </summary>
        [ForeignKey("SenderId")]
        public Player Sender { get; set; }

        /// <summary>
        /// ID получателя (если это личное сообщение).
        /// </summary>
        public int? ReceiverId { get; set; }

        /// <summary>
        /// Получатель сообщения (если это личное сообщение).
        /// </summary>
        [ForeignKey("ReceiverId")]
        public Player Receiver { get; set; }

        /// <summary>
        /// Текст сообщения.
        /// </summary>
        [Required]
        public string Message { get; set; }

        /// <summary>
        /// Дата и время отправки сообщения.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Тип сообщения (личное или общее).
        /// </summary>
        public ChatMessageType Type { get; set; } = ChatMessageType.Global;
    }

    /// <summary>
    /// Тип сообщения чата.
    /// </summary>
    public enum ChatMessageType
    {
        Global, // Общее сообщение
        Private // Личное сообщение
    }
}