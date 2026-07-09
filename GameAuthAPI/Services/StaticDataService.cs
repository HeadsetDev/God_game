using GameAuthAPI.Models;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace GameAuthAPI.Services
{
    public class StaticDataService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<StaticDataService> _logger;
        private readonly string _staticDataPath;
        private readonly JsonSerializerOptions _jsonOptions;

        public StaticDataService(IDistributedCache cache, ILogger<StaticDataService> logger, IWebHostEnvironment env)
        {
            _cache = cache;
            _logger = logger;
            _staticDataPath = Path.Combine(env.ContentRootPath, "Data", "Static");

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true, // <-- ИГНОРИРУЕМ РЕГИСТР
                WriteIndented = true
            };
        }

        private async Task<List<T>> LoadFromFolderAsync<T>(string folderName, string cacheKey, TimeSpan? expiration = null) where T : class
        {
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                try
                {
                    return JsonSerializer.Deserialize<List<T>>(cached, _jsonOptions) ?? new List<T>();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Ошибка десериализации кэша для {CacheKey}, загружаем из файлов.", cacheKey);
                }
            }

            var result = new List<T>();
            var folderPath = Path.Combine(_staticDataPath, folderName);

            if (!Directory.Exists(folderPath))
            {
                _logger.LogWarning("Папка {FolderPath} не найдена. Возвращаем пустой список.", folderPath);
                return result;
            }

            var files = Directory.GetFiles(folderPath, "*.json");
            if (!files.Any())
            {
                _logger.LogWarning("В папке {FolderPath} нет JSON-файлов.", folderPath);
                return result;
            }

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        _logger.LogWarning("Файл {File} пустой, пропускаем.", file);
                        continue;
                    }

                    var batch = JsonSerializer.Deserialize<List<T>>(json, _jsonOptions);
                    if (batch != null)
                        result.AddRange(batch);
                    else
                        _logger.LogWarning("Файл {File} содержит невалидный JSON или пустой массив.", file);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка чтения файла {File}", file);
                }
            }

            if (result.Any())
            {
                var options = new DistributedCacheEntryOptions();
                if (expiration.HasValue)
                    options.AbsoluteExpirationRelativeToNow = expiration;
                else
                    options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60);

                var serialized = JsonSerializer.Serialize(result, _jsonOptions);
                await _cache.SetStringAsync(cacheKey, serialized, options);
            }

            return result;
        }

        public async Task<List<Item>> GetItemsAsync()
            => await LoadFromFolderAsync<Item>("Items", "static_items", TimeSpan.FromMinutes(60));

        public async Task<List<Mob>> GetMobsAsync()
            => await LoadFromFolderAsync<Mob>("Mobs", "static_mobs", TimeSpan.FromMinutes(60));

        public async Task<List<Quest>> GetQuestsAsync()
            => await LoadFromFolderAsync<Quest>("Quests", "static_quests", TimeSpan.FromMinutes(60));

        public async Task<List<Skill>> GetSkillsAsync()
            => await LoadFromFolderAsync<Skill>("Skills", "static_skills", TimeSpan.FromMinutes(60));

        public async Task<List<Achievement>> GetAchievementsAsync()
            => await LoadFromFolderAsync<Achievement>("Achievements", "static_achievements", TimeSpan.FromMinutes(60));

        public async Task<List<CraftRecipe>> GetCraftRecipesAsync()
            => await LoadFromFolderAsync<CraftRecipe>("CraftRecipes", "static_craft_recipes", TimeSpan.FromMinutes(60));

        public async Task<Item?> GetItemByIdAsync(int id)
        {
            var items = await GetItemsAsync();
            return items.FirstOrDefault(i => i.Id == id);
        }

        public async Task<Quest?> GetQuestByIdAsync(int id)
        {
            var quests = await GetQuestsAsync();
            return quests.FirstOrDefault(q => q.Id == id);
        }

        public async Task<CraftRecipe?> GetCraftRecipeByIdAsync(int id)
        {
            var recipes = await GetCraftRecipesAsync();
            return recipes.FirstOrDefault(r => r.Id == id);
        }

        public async Task<List<CraftRecipe>> GetAvailableCraftRecipesAsync(Player player)
        {
            var all = await GetCraftRecipesAsync();
            return all.Where(r => IsRecipeAvailable(player, r)).ToList();
        }

        public bool IsRecipeAvailable(Player player, CraftRecipe recipe)
        {
            if (recipe.Requirements == null || !recipe.Requirements.Any())
                return true;

            foreach (var req in recipe.Requirements)
            {
                switch (req.Type.ToLower())
                {
                    case "level":
                        if (player.Level < req.Value)
                            return false;
                        break;

                    case "craftskill":
                        if (player.CraftSkillLevel < req.Value)
                            return false;
                        break;

                    case "rank":
                        if (!string.IsNullOrEmpty(req.AdditionalData) && player.Rank != req.AdditionalData)
                            return false;
                        break;

                    case "achievement":
                        if (req.AdditionalData != null)
                        {
                            if (!player.Achievements.Contains(req.Value))
                                return false;
                        }
                        break;

                    default:
                        return false;
                }
            }

            return true;
        }
    }
}