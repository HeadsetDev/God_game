using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameAuthAPI.Services
{
    public class RedisCacheService
    {
        private readonly IDistributedCache _cache;

        public RedisCacheService(IDistributedCache cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// Получить объект из кэша по ключу
        /// </summary>
        public async Task<T?> GetAsync<T>(string key)
        {
            var data = await _cache.GetStringAsync(key);
            if (string.IsNullOrEmpty(data))
                return default;

            return JsonSerializer.Deserialize<T>(data);
        }

        /// <summary>
        /// Сохранить объект в кэш
        /// </summary>
        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            var options = new DistributedCacheEntryOptions();
            if (expiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiration;
            }
            else
            {
                options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            }

            var json = JsonSerializer.Serialize(value);
            await _cache.SetStringAsync(key, json, options);
        }

        /// <summary>
        /// Удалить объект из кэша
        /// </summary>
        public async Task RemoveAsync(string key)
        {
            await _cache.RemoveAsync(key);
        }

        /// <summary>
        /// Проверить, существует ли ключ в кэше
        /// </summary>
        public async Task<bool> ExistsAsync(string key)
        {
            var data = await _cache.GetStringAsync(key);
            return !string.IsNullOrEmpty(data);
        }

        /// <summary>
        /// Получить строковое значение из кэша
        /// </summary>
        public async Task<string?> GetStringAsync(string key)
        {
            return await _cache.GetStringAsync(key);
        }

        /// <summary>
        /// Сохранить строковое значение в кэш
        /// </summary>
        public async Task SetStringAsync(string key, string value, TimeSpan? expiration = null)
        {
            var options = new DistributedCacheEntryOptions();
            if (expiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiration;
            }
            else
            {
                options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            }

            await _cache.SetStringAsync(key, value, options);
        }
    }
}