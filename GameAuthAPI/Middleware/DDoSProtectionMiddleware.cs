using Microsoft.Extensions.Caching.Memory;

namespace GameAuthAPI.Middleware
{
    public class DDoSProtectionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private readonly ILogger<DDoSProtectionMiddleware> _logger;

        public DDoSProtectionMiddleware(
            RequestDelegate next,
            IMemoryCache cache,
            ILogger<DDoSProtectionMiddleware> logger)
        {
            _next = next;
            _cache = cache;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var key = $"ddos_{ip}";

            var requestCount = _cache.Get<int>(key);
            if (requestCount >= 100)
            {
                _logger.LogWarning("🚨 DDoS protection triggered for IP: {Ip}", ip);
                context.Response.StatusCode = 429;
                await context.Response.WriteAsync("Too Many Requests. Please try again later.");
                return;
            }

            _cache.Set(key, requestCount + 1, TimeSpan.FromMinutes(1));
            await _next(context);
        }
    }
}