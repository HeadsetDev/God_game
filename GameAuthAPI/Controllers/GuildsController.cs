using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Data;
using GameAuthAPI.Models;
using GameAuthAPI.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization; // Добавьте эту строку

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GuildsController : ControllerBase
    {
        private readonly GameDbContext _context;

        public GuildsController(GameDbContext context)
        {
            _context = context;
        }

        [HttpPost("create")]
        [Authorize] // Убедитесь, что метод доступен только аутентифицированным пользователям
        public async Task<IActionResult> CreateGuild([FromBody] CreateGuildDto createGuildDto)
        {
            // Проверка, что данные для создания гильдии предоставлены
            if (createGuildDto == null)
            {
                return BadRequest("Данные для создания гильдии не предоставлены.");
            }

            // Получаем идентификатор пользователя из токена
            var playerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (playerIdClaim == null)
            {
                return Unauthorized("Идентификатор пользователя не найден в токене.");
            }

            if (!int.TryParse(playerIdClaim.Value, out var playerId))
            {
                return BadRequest("Некорректный идентификатор пользователя.");
            }

            // Проверяем, что пользователь существует
            var player = await _context.Players.FindAsync(playerId);
            if (player == null)
            {
                return NotFound("Пользователь не найден.");
            }

            // Создаем гильдию
            var guild = new Guild
            {
                Name = createGuildDto.Name,
                LeaderId = playerId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Guilds.Add(guild);
            await _context.SaveChangesAsync();

            // Добавляем создателя гильдии как лидера
            _context.PlayerGuilds.Add(new PlayerGuild
            {
                PlayerId = playerId,
                GuildId = guild.Id,
                Role = "Leader"
            });

            await _context.SaveChangesAsync();

            return Ok(new { guild.Id, guild.Name });
        }
    }
}