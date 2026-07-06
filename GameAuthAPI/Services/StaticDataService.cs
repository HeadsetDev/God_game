using System.Text.Json;
using GameAuthAPI.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace GameAuthAPI.Services
{
    public class StaticDataService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<StaticDataService> _logger;
        private readonly string _staticDataPath;

        public StaticDataService(IDistributedCache cache, ILogger<StaticDataService> logger, IWebHostEnvironment env)
        {
            _cache = cache;
            _logger = logger;
            _staticDataPath = Path.Combine(env.ContentRootPath, "Data", "Static");
        }

        // ===================== ЗАГРУЗКА ИЗ ФАЙЛОВ =====================

        private async Task<T> LoadFromFileAsync<T>(string fileName, string cacheKey, TimeSpan? cacheExpiration = null)
        {
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                return JsonSerializer.Deserialize<T>(cached) ?? Activator.CreateInstance<T>();
            }

            var filePath = Path.Combine(_staticDataPath, fileName);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning($"Файл {filePath} не найден. Возвращаем пустые данные.");
                return Activator.CreateInstance<T>();
            }

            var json = await File.ReadAllTextAsync(filePath);
            var data = JsonSerializer.Deserialize<T>(json);

            if (data != null)
            {
                var options = new DistributedCacheEntryOptions();
                if (cacheExpiration.HasValue)
                    options.AbsoluteExpirationRelativeToNow = cacheExpiration;
                else
                    options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);

                await _cache.SetStringAsync(cacheKey, json, options);
            }

            return data ?? Activator.CreateInstance<T>();
        }

        // ===================== ПОЛУЧЕНИЕ ДАННЫХ =====================

        public async Task<List<Item>> GetItemsAsync()
            => await LoadFromFileAsync<List<Item>>("items.json", "static_items", TimeSpan.FromMinutes(60));

        public async Task<List<Mob>> GetMobsAsync()
            => await LoadFromFileAsync<List<Mob>>("mobs.json", "static_mobs", TimeSpan.FromMinutes(60));

        public async Task<List<Quest>> GetQuestsAsync()
            => await LoadFromFileAsync<List<Quest>>("quests.json", "static_quests", TimeSpan.FromMinutes(60));

        public async Task<List<Skill>> GetSkillsAsync()
            => await LoadFromFileAsync<List<Skill>>("skills.json", "static_skills", TimeSpan.FromMinutes(60));

        public async Task<List<CraftRecipe>> GetCraftRecipesAsync()
            => await LoadFromFileAsync<List<CraftRecipe>>("craft_recipes.json", "static_craft_recipes", TimeSpan.FromMinutes(60));

        public async Task<List<Achievement>> GetAchievementsAsync()
            => await LoadFromFileAsync<List<Achievement>>("achievements.json", "static_achievements", TimeSpan.FromMinutes(60));

        // ===================== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ =====================

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