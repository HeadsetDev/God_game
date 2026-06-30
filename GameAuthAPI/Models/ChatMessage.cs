using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameAuthAPI.Services;

namespace GameAuthAPI.Models
{
    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }

        public int SenderId { get; set; }

        [ForeignKey("SenderId")]
        public Player Sender { get; set; } = null!;

        public int? ReceiverId { get; set; }

        [ForeignKey("ReceiverId")]
        public Player? Receiver { get; set; }

        // ========== ØÈÔÐÎÂÀÍÍÎÅ ÑÎÎÁÙÅÍÈÅ ==========
        public string MessageEncrypted { get; set; } = string.Empty;

        [NotMapped]
        public string Message
        {
            get => DecryptMessage(MessageEncrypted);
            set => MessageEncrypted = EncryptMessage(value);
        }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public ChatMessageType Type { get; set; } = ChatMessageType.Global;

        private static EncryptionService? _encryptionService;

        private EncryptionService GetEncryptionService()
        {
            if (_encryptionService == null)
            {
                var config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json")
                    .Build();
                _encryptionService = new EncryptionService(config);
            }
            return _encryptionService;
        }

        private string EncryptMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return string.Empty;

            return GetEncryptionService().Encrypt(message);
        }

        private string DecryptMessage(string encrypted)
        {
            if (string.IsNullOrEmpty(encrypted))
                return string.Empty;

            return GetEncryptionService().TryDecrypt(encrypted, out var result) ? result : "[SHIFROVANO]";
        }
    }

    public enum ChatMessageType
    {
        Global,
        Private,
        Guild
    }
}