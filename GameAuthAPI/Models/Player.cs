using System.ComponentModel.DataAnnotations;
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

        // Внешний ключ для текущей локации
        public int CurrentLocationId { get; set; }

        // Навигационное свойство для текущей локации
        public Location CurrentLocation { get; set; } = null!;

        public List<PlayerItem> PlayerItems { get; set; } = new();
        public List<Quest> Quests { get; set; } = new();
        public int PlayerKills { get; set; }

        // Словарь для хранения ресурсов игрока
        public Dictionary<string, int> Resources { get; set; } = new();

        // Навигационное свойство для гильдий
        public List<PlayerGuild> PlayerGuilds { get; set; } = new List<PlayerGuild>();

        private readonly PasswordService _passwordService;
        private readonly ILogger<Player> _logger;

        public Player(string name, string password, PasswordService passwordService, ILogger<Player> logger)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Имя пользователя не может быть пустым.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Пароль не может быть пустым.", nameof(password));
            }

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

        // Конструктор для Entity Framework
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
                        {
                            totalStats[stat.Key] += stat.Value;
                        }
                        else
                        {
                            totalStats[stat.Key] = stat.Value;
                        }
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
    }
}