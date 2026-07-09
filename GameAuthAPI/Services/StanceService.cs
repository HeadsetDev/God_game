using GameAuthAPI.Models;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace GameAuthAPI.Services
{
    public class StanceService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<StanceService> _logger;
        private readonly string _staticDataPath;
        private readonly JsonSerializerOptions _jsonOptions;

        public StanceService(IDistributedCache cache, ILogger<StanceService> logger, IWebHostEnvironment env)
        {
            _cache = cache;
            _logger = logger;
            _staticDataPath = Path.Combine(env.ContentRootPath, "Data", "Static");
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        public async Task<StanceStats?> GetStanceStatsAsync(StanceType stance)
        {
            const string cacheKey = "static_stances";
            var cached = await _cache.GetStringAsync(cacheKey);
            StanceData? stanceData = null;

            if (!string.IsNullOrEmpty(cached))
            {
                try
                {
                    var all = JsonSerializer.Deserialize<StanceCollection>(cached, _jsonOptions);
                    stanceData = all?.Stances?.FirstOrDefault(s => s.StanceType == (int)stance);
                }
                catch
                {
                    // игнорируем
                }
            }

            if (stanceData == null)
            {
                var filePath = Path.Combine(_staticDataPath, "Stances", "stances.json");
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Файл стоек не найден: {FilePath}", filePath);
                    return new StanceStats();
                }

                try
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    var all = JsonSerializer.Deserialize<StanceCollection>(json, _jsonOptions);
                    stanceData = all?.Stances?.FirstOrDefault(s => s.StanceType == (int)stance);

                    if (all?.Stances != null)
                    {
                        var options = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60)
                        };
                        await _cache.SetStringAsync(cacheKey, json, options);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка загрузки стоек");
                    return new StanceStats();
                }
            }

            if (stanceData == null)
                return new StanceStats();

            return new StanceStats
            {
                StrengthBonus = stanceData.Bonuses?.StrengthBonus ?? 0,
                AgilityBonus = stanceData.Bonuses?.AgilityBonus ?? 0,
                IntelligenceBonus = stanceData.Bonuses?.IntelligenceBonus ?? 0,
                VitalityBonus = stanceData.Bonuses?.VitalityBonus ?? 0,
                WillpowerBonus = stanceData.Bonuses?.WillpowerBonus ?? 0,
                PerceptionBonus = stanceData.Bonuses?.PerceptionBonus ?? 0,
                HealthBonus = stanceData.Bonuses?.HealthBonus ?? 0,
                ManaBonus = stanceData.Bonuses?.ManaBonus ?? 0,
                DefenseBonus = stanceData.Bonuses?.DefenseBonus ?? 0,
                MagicResistBonus = stanceData.Bonuses?.MagicResistBonus ?? 0
            };
        }

        public async Task<List<int>> GetAvailableSkillsForStanceAsync(StanceType stance)
        {
            var stats = await GetStanceStatsAsync(stance);
            // Не совсем корректно, нужно отдельно хранить навыки.
            // Пока возвращаем пустой список – позже добавим.
            return new List<int>();
        }
    }

    public class StanceCollection
    {
        public List<StanceData>? Stances { get; set; }
    }

    public class StanceData
    {
        public int StanceType { get; set; }
        public string? Name { get; set; }
        public string? ClassName { get; set; }
        public StanceBonusData? Bonuses { get; set; }
        public List<int>? AvailableSkills { get; set; }
    }

    public class StanceBonusData
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
    }
}