using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameAuthAPI.Services;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;

namespace GameAuthAPI.Models
{
    public class Player
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Имя пользователя обязательно.")]
        [MinLength(3, ErrorMessage = "Имя пользователя должно содержать не менее 3 символов.")]
        [RegularExpression(@"^[a-zA-Z]+$", ErrorMessage = "Имя должно состоять только из букв.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Хэш пароля обязателен.")]
        public string PasswordHash { get; set; } = string.Empty;

        public string Role { get; set; } = "Player";
        public int Level { get; set; }
        public int Coins { get; set; }
        public int Crystals { get; set; }
        public int Experience { get; set; }
        public int ExperienceToNextLevel { get; set; } = 100;

        public int CurrentLocationId { get; set; }
        public Location CurrentLocation { get; set; } = null!;

        public List<PlayerItem> PlayerItems { get; set; } = new();
        public List<Quest> Quests { get; set; } = new();
        public int PlayerKills { get; set; }

        // ========== НОВЫЕ ПОЛЯ ДЛЯ КРАФТА, РАНГА И ДОСТИЖЕНИЙ ==========
        public int CraftSkillLevel { get; set; } = 0;
        public string Rank { get; set; } = "Новичок";
        public string AchievementsJson { get; set; } = "[]";

        [NotMapped]
        public List<int> Achievements
        {
            get => string.IsNullOrEmpty(AchievementsJson)
                ? new List<int>()
                : JsonSerializer.Deserialize<List<int>>(AchievementsJson) ?? new List<int>();
            set => AchievementsJson = JsonSerializer.Serialize(value);
        }

        // ========== ШИФРОВАННЫЕ ПОЛЯ (опционально) ==========
        public string? EmailEncrypted { get; set; }
        public string? PhoneEncrypted { get; set; }
        public string? AddressEncrypted { get; set; }

        [NotMapped]
        public string? Email
        {
            get => DecryptField(EmailEncrypted);
            set => EmailEncrypted = EncryptField(value);
        }

        [NotMapped]
        public string? Phone
        {
            get => DecryptField(PhoneEncrypted);
            set => PhoneEncrypted = EncryptField(value);
        }

        [NotMapped]
        public string? Address
        {
            get => DecryptField(AddressEncrypted);
            set => AddressEncrypted = EncryptField(value);
        }

        [NotMapped]
        public Dictionary<string, int> Resources { get; set; } = new();

        public List<PlayerGuild> PlayerGuilds { get; set; } = new List<PlayerGuild>();
        public List<PlayerSkill> PlayerSkills { get; set; } = new();

        // PvP статистика
        public int PvP_Wins { get; set; } = 0;
        public int PvP_Losses { get; set; } = 0;
        public int PvP_Kills { get; set; } = 0;
        public int PvP_Deaths { get; set; } = 0;

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

        private string? EncryptField(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            return GetEncryptionService().Encrypt(value);
        }

        private string? DecryptField(string? encrypted)
        {
            if (string.IsNullOrEmpty(encrypted))
                return null;
            return GetEncryptionService().TryDecrypt(encrypted, out var result) ? result : null;
        }

        private readonly PasswordService _passwordService;
        private readonly ILogger<Player> _logger;

        public Player(string name, string password, PasswordService passwordService, ILogger<Player> logger)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Имя пользователя не может быть пустым.", nameof(name));
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Пароль не может быть пустым.", nameof(password));

            _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (!ValidateName(name))
            {
                _logger.LogError("Некорректное имя пользователя: {Name}", name);
                throw new ArgumentException("Имя должно содержать хотя бы 2 символа и состоять только из букв.", nameof(name));
            }

            Name = name;
            PasswordHash = _passwordService.HashPassword(password);
        }

        public Player()
        {
            _passwordService = new PasswordService();
            _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Player>();
        }

        private bool ValidateName(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && name.Length >= 2 && name.All(char.IsLetter);
        }

        public bool CheckPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                _logger.LogWarning("Попытка проверки пустого пароля для пользователя: {Name}", Name);
                return false;
            }
            return _passwordService.CheckPassword(PasswordHash, password);
        }

        public Dictionary<string, double> CalculateTotalStats()
        {
            var totalStats = new Dictionary<string, double>();
            foreach (var playerItem in PlayerItems)
            {
                if (playerItem.IsEquipped && playerItem.Item != null)
                {
                    foreach (var stat in playerItem.Item.Stats)
                    {
                        if (totalStats.ContainsKey(stat.Key))
                            totalStats[stat.Key] += stat.Value;
                        else
                            totalStats[stat.Key] = stat.Value;
                    }
                }
            }
            return totalStats;
        }

        public void CheckQuests()
        {
            foreach (var quest in Quests)
            {
                if (!quest.IsCompleted)
                {
                    bool isCompleted = true;
                    foreach (var condition in quest.Conditions)
                    {
                        if (condition.Key == "MonstersKilled" && PlayerKills < condition.Value)
                        {
                            isCompleted = false;
                            break;
                        }
                    }
                    if (isCompleted)
                    {
                        quest.IsCompleted = true;
                        Coins += quest.Reward;
                    }
                }
            }
        }

        public void AddExperience(int amount)
        {
            Experience += amount;
            while (Experience >= ExperienceToNextLevel)
            {
                Experience -= ExperienceToNextLevel;
                Level++;
                ExperienceToNextLevel = (int)(ExperienceToNextLevel * 1.5);
            }
        }
    }
}