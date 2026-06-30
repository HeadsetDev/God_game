using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Data;
using GameAuthAPI.Models;
using GameAuthAPI.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using GameAuthAPI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;

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

        public AuthController(
            GameDbContext context,
            PasswordService passwordService,
            IConfiguration config,
            ILogger<AuthController> logger,
            ILoggerFactory loggerFactory)
        {
            _context = context;
            _passwordService = passwordService;
            _config = config;
            _logger = logger;
            _loggerFactory = loggerFactory;
        }

        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Register([FromBody] RegisterUserDto registerUserDto)
        {
            if (registerUserDto == null)
            {
                _logger.LogWarning("Данные для регистрации не предоставлены.");
                return BadRequest("Данные для регистрации не предоставлены.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (await _context.Players.AnyAsync(u => u.Name == registerUserDto.Username))
                {
                    _logger.LogWarning("Попытка регистрации уже существующего пользователя: {Username}", registerUserDto.Username);
                    return BadRequest("Пользователь уже существует.");
                }

                var playerLogger = _loggerFactory.CreateLogger<Player>();
                var newPlayer = new Player(
                    registerUserDto.Username,
                    registerUserDto.Password,
                    _passwordService,
                    playerLogger
                )
                {
                    Role = registerUserDto.Role
                };

                _context.Players.Add(newPlayer);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Пользователь успешно зарегистрирован: {Username}", registerUserDto.Username);
                return Ok("Пользователь зарегистрирован.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Ошибка при регистрации пользователя: {Username}", registerUserDto.Username);
                return StatusCode(500, "Произошла ошибка при регистрации.");
            }
        }

        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Login([FromBody] LoginUserDto loginUserDto)
        {
            if (loginUserDto == null)
            {
                _logger.LogWarning("Данные для входа не предоставлены.");
                return BadRequest("Данные для входа не предоставлены.");
            }

            try
            {
                var player = await _context.Players.FirstOrDefaultAsync(u => u.Name == loginUserDto.Username);
                if (player == null || !player.CheckPassword(loginUserDto.Password))
                {
                    _logger.LogWarning("Неудачная попытка входа для пользователя: {Username}", loginUserDto.Username);
                    return Unauthorized("Неверные данные.");
                }

                var token = GenerateJwtToken(player.Name, player.Role);
                _logger.LogInformation("Пользователь успешно вошел: {Username}", loginUserDto.Username);
                return Ok(new { token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при входе пользователя: {Username}", loginUserDto.Username);
                return StatusCode(500, "Произошла ошибка при входе.");
            }
        }

        private string GenerateJwtToken(string username, string role)
        {
            var jwtSettings = _config.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("Секретный ключ не найден в конфигурации.");

            // Получаем идентификатор пользователя из базы данных
            var player = _context.Players.FirstOrDefault(p => p.Name == username);
            if (player == null)
            {
                throw new InvalidOperationException("Пользователь не найден.");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, player.Id.ToString()), // Используем Id пользователя
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}