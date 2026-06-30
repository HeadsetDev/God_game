using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameAuthAPI.Services; // <-- ЭТА СТРОКА БЫЛА ПРОПУЩЕНА

namespace GameAuthAPI.Models
{
    public class EncryptedField
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string EncryptedValue { get; set; } = string.Empty;

        [NotMapped]
        public string? DecryptedValue
        {
            get => null;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    var config = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json")
                        .Build();
                    var encryptionService = new EncryptionService(config);
                    EncryptedValue = encryptionService.Encrypt(value);
                }
            }
        }

        public EncryptedField() { }

        public EncryptedField(string plainText, EncryptionService encryptionService)
        {
            EncryptedValue = encryptionService.Encrypt(plainText);
        }

        public string Decrypt(EncryptionService encryptionService)
        {
            return encryptionService.Decrypt(EncryptedValue);
        }
    }
}