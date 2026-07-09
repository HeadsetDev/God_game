using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Data;
using GameAuthAPI.Models;
using GameAuthAPI.DTOs;
using GameAuthAPI.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly PasswordService _passwordService;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthController> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IMemoryCache _cache;
        private readonly SecurityLogger _securityLogger;
        private readonly EncryptionService _encryptionService;

        public AuthController(
            GameDbContext context,
            PasswordService passwordService,
            IConfiguration config,
            ILogger<AuthController> logger,
            ILoggerFactory loggerFactory,
            IMemoryCache cache,
            SecurityLogger securityLogger,
            EncryptionService encryptionService)
        {
            _context = context;
            _passwordService = passwordService;
            _config = config;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _cache = cache;
            _securityLogger = securityLogger;
            _encryptionService = encryptionService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterUserDto registerUserDto)
        {
            if (registerUserDto == null)
                return BadRequest(ApiResponse<object>.Fail("Данные для регистрации не предоставлены."));

            if (!IsPasswordStrong(registerUserDto.Password))
                return BadRequest(ApiResponse<object>.Fail("Пароль должен содержать минимум 8 символов, буквы и цифры."));

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (await _context.Players.AnyAsync(u => u.Name == registerUserDto.Username))
                {
                    _securityLogger.LogSuspiciousActivity(
                        "Попытка регистрации с существующим именем",
                        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        registerUserDto.Username
                    );
                    return BadRequest(ApiResponse<object>.Fail("Пользователь уже существует."));
                }

                var defaultLocation = await _context.Locations.FirstOrDefaultAsync();
                if (defaultLocation == null)
                {
                    defaultLocation = new Location { Name = "Стартовая локация", Description = "Место, где появляются новые игроки." };
                    _context.Locations.Add(defaultLocation);
                    await _context.SaveChangesAsync();
                }

                var playerLogger = _loggerFactory.CreateLogger<Player>();
                var newPlayer = new Player(
                    registerUserDto.Username,
                    registerUserDto.Password,
                    _passwordService,
                    playerLogger
                )
                {
                    Role = registerUserDto.Role,
                    CurrentLocationId = defaultLocation.Id
                };

                _context.Players.Add(newPlayer);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _securityLogger.LogSecurityEvent(
                    "USER_REGISTERED",
                    $"Новый пользователь {registerUserDto.Username} зарегистрирован",
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    registerUserDto.Username
                );

                return Ok(ApiResponse<object>.Ok(null, "Пользователь зарегистрирован."));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Ошибка при регистрации");
                return StatusCode(500, ApiResponse<object>.Fail("Произошла ошибка при регистрации."));
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginUserDto loginUserDto)
        {
            if (loginUserDto == null)
                return BadRequest(ApiResponse<object>.Fail("Данные для входа не предоставлены."));

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var key = $"failed_login_{ip}_{loginUserDto.Username}";

            var attempts = _cache.Get<int>(key);
            if (attempts >= 5)
            {
                _securityLogger.LogSuspiciousActivity(
                    "Блокировка IP после 5 неудачных попыток входа",
                    ip,
                    loginUserDto.Username
                );
                return StatusCode(403, ApiResponse<object>.Fail("Слишком много попыток входа. Попробуйте позже."));
            }

            try
            {
                var player = await _context.Players.FirstOrDefaultAsync(u => u.Name == loginUserDto.Username);
                if (player == null || !player.CheckPassword(loginUserDto.Password))
                {
                    _cache.Set(key, attempts + 1, TimeSpan.FromMinutes(15));
                    _securityLogger.LogSuspiciousActivity(
                        "Неудачная попытка входа",
                        ip,
                        loginUserDto.Username
                    );
                    return Unauthorized(ApiResponse<object>.Fail("Неверные данные."));
                }

                _cache.Remove(key);
                var token = GenerateJwtToken(player.Name, player.Role);

                _securityLogger.LogSecurityEvent(
                    "USER_LOGIN",
                    $"Пользователь {player.Name} успешно вошел",
                    ip,
                    player.Name
                );

                return Ok(ApiResponse<object>.Ok(new { token }, "Вход выполнен."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при входе");
                return StatusCode(500, ApiResponse<object>.Fail("Произошла ошибка при входе."));
            }
        }

        private string GenerateJwtToken(string username, string role)
        {
            var jwtSettings = _config.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("Секретный ключ не найден.");

            var player = _context.Players.FirstOrDefault(p => p.Name == username);
            if (player == null)
                throw new InvalidOperationException("Пользователь не найден.");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, player.Id.ToString()),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role),
                new Claim("SessionId", Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private bool IsPasswordStrong(string password)
        {
            return password.Length >= 8 &&
                   password.Any(char.IsUpper) &&
                   password.Any(char.IsLower) &&
                   password.Any(char.IsDigit);
        }
    }
}