namespace GameAuthAPI.Services
{
    public class SecurityLogger
    {
        private readonly ILogger<SecurityLogger> _logger;

        public SecurityLogger(ILogger<SecurityLogger> logger)
        {
            _logger = logger;
        }

        public void LogSuspiciousActivity(string message, string ip, string? user = null)
        {
            _logger.LogWarning(
                "🚨 SUSPICIOUS ACTIVITY: {Message} | IP: {Ip} | User: {User} | Time: {Time}",
                message, ip, user ?? "Unknown", DateTime.UtcNow
            );
        }

        public void LogSecurityEvent(string eventName, string details, string ip, string? user = null)
        {
            _logger.LogInformation(
                "🔐 SECURITY EVENT: {EventName} | Details: {Details} | IP: {Ip} | User: {User}",
                eventName, details, ip, user ?? "Unknown"
            );
        }
    }
}