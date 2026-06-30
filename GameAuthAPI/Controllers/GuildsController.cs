using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GameAuthAPI.Data;
using GameAuthAPI.Models;
using GameAuthAPI.DTOs;
using GameAuthAPI.Services;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GuildsController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly RedisCacheService _cache;

        public GuildsController(GameDbContext context, RedisCacheService cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpPost("create")]
        [Authorize]
        public async Task<IActionResult> CreateGuild([FromBody] CreateGuildDto createGuildDto)
        {
            if (createGuildDto == null)
            {
                return BadRequest("Данные для создания гильдии не предоставлены.");
            }

            var playerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (playerIdClaim == null)
            {
                return Unauthorized("Идентификатор пользователя не найден в токене.");
            }

            if (!int.TryParse(playerIdClaim.Value, out var playerId))
            {
                return BadRequest("Некорректный идентификатор пользователя.");
            }

            var player = await _context.Players.FindAsync(playerId);
            if (player == null)
            {
                return NotFound("Пользователь не найден.");
            }

            var guild = new Guild
            {
                Name = createGuildDto.Name,
                LeaderId = playerId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Guilds.Add(guild);
            await _context.SaveChangesAsync();

            _context.PlayerGuilds.Add(new PlayerGuild
            {
                PlayerId = playerId,
                GuildId = guild.Id,
                Role = "Leader"
            });

            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync($"guild_{guild.Id}");
            await _cache.RemoveAsync($"player_guild_{playerId}");

            return Ok(new { guild.Id, guild.Name });
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetGuild(int id)
        {
            var cacheKey = $"guild_{id}";
            var guild = await _cache.GetAsync<Guild>(cacheKey);

            if (guild == null)
            {
                guild = await _context.Guilds
                    .Include(g => g.PlayerGuilds)
                        .ThenInclude(pg => pg.Player)
                    .FirstOrDefaultAsync(g => g.Id == id);

                if (guild == null)
                {
                    return NotFound("Гильдия не найдена.");
                }

                await _cache.SetAsync(cacheKey, guild, TimeSpan.FromMinutes(5));
            }

            return Ok(guild);
        }

        [HttpGet("player/{playerId}")]
        [Authorize]
        public async Task<IActionResult> GetPlayerGuild(int playerId)
        {
            var cacheKey = $"player_guild_{playerId}";
            var guild = await _cache.GetAsync<Guild>(cacheKey);

            if (guild == null)
            {
                var playerGuild = await _context.PlayerGuilds
                    .Include(pg => pg.Guild)
                    .FirstOrDefaultAsync(pg => pg.PlayerId == playerId);

                if (playerGuild == null)
                {
                    return NotFound("Игрок не состоит в гильдии.");
                }

                guild = playerGuild.Guild;
                await _cache.SetAsync(cacheKey, guild, TimeSpan.FromMinutes(5));
            }

            return Ok(guild);
        }
    }
}