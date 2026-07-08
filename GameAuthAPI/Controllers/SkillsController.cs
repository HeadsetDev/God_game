using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GameAuthAPI.Data;
using GameAuthAPI.Models;
using GameAuthAPI.DTOs;
using GameAuthAPI.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SkillsController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly RedisCacheService _cache;

        public SkillsController(GameDbContext context, RedisCacheService cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllSkills()
        {
            const string cacheKey = "skills_all";
            var skills = await _cache.GetAsync<List<Skill>>(cacheKey);

            if (skills == null)
            {
                skills = await _context.Skills.ToListAsync();
                await _cache.SetAsync(cacheKey, skills, TimeSpan.FromMinutes(30));
            }

            return Ok(ApiResponse<List<Skill>>.Ok(skills ?? new List<Skill>()));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSkill(int id)
        {
            var cacheKey = $"skill_{id}";
            var skill = await _cache.GetAsync<Skill>(cacheKey);

            if (skill == null)
            {
                skill = await _context.Skills.FindAsync(id);
                if (skill == null)
                    return NotFound(ApiResponse<object>.Fail("Навык не найден."));

                await _cache.SetAsync(cacheKey, skill, TimeSpan.FromMinutes(30));
            }

            return Ok(ApiResponse<Skill>.Ok(skill));
        }

        // Публичный просмотр навыков игрока — без ограничения владения.
        [HttpGet("player/{playerId}")]
        [Authorize]
        public async Task<IActionResult> GetPlayerSkills(int playerId)
        {
            var cacheKey = $"player_skills_{playerId}";
            var skills = await _cache.GetAsync<List<Skill>>(cacheKey);

            if (skills == null)
            {
                var player = await _context.Players
                    .Include(p => p.PlayerSkills)
                        .ThenInclude(ps => ps.Skill)
                    .FirstOrDefaultAsync(p => p.Id == playerId);

                if (player == null)
                    return NotFound(ApiResponse<object>.Fail("Игрок не найден."));

                skills = player.PlayerSkills.Select(ps => ps.Skill).ToList();
                await _cache.SetAsync(cacheKey, skills, TimeSpan.FromMinutes(5));
            }

            return Ok(ApiResponse<List<Skill>>.Ok(skills ?? new List<Skill>()));
        }

        [HttpPost("learn")]
        [Authorize]
        public async Task<IActionResult> LearnSkill([FromBody] LearnSkillDto dto)
        {
            if (dto == null)
                return BadRequest(ApiResponse<object>.Fail("Данные не предоставлены."));

            var authError = EnsureOwnPlayerId(dto.PlayerId);
            if (authError != null)
                return authError;

            var player = await _context.Players.FindAsync(dto.PlayerId);
            if (player == null)
                return NotFound(ApiResponse<object>.Fail("Игрок не найден."));

            var skill = await _context.Skills.FindAsync(dto.SkillId);
            if (skill == null)
                return NotFound(ApiResponse<object>.Fail("Навык не найден."));

            if (player.Level < skill.RequiredLevel)
                return BadRequest(ApiResponse<object>.Fail($"Требуется уровень {skill.RequiredLevel}."));

            var existing = await _context.PlayerSkills
                .FirstOrDefaultAsync(ps => ps.PlayerId == dto.PlayerId && ps.SkillId == dto.SkillId);

            if (existing != null)
                return BadRequest(ApiResponse<object>.Fail("Навык уже изучен."));

            _context.PlayerSkills.Add(new PlayerSkill
            {
                PlayerId = dto.PlayerId,
                SkillId = dto.SkillId
            });

            await _context.SaveChangesAsync();

            await _cache.RemoveAsync($"player_skills_{dto.PlayerId}");
            await _cache.RemoveAsync($"player_skill_{dto.PlayerId}_{dto.SkillId}");

            return Ok(ApiResponse<object>.Ok(null, "Навык успешно изучен."));
        }

        [HttpPost("use/{playerId}/{skillId}")]
        [Authorize]
        public async Task<IActionResult> UseSkill(int playerId, int skillId)
        {
            var authError = EnsureOwnPlayerId(playerId);
            if (authError != null)
                return authError;

            var playerSkill = await _context.PlayerSkills
                .Include(ps => ps.Skill)
                .FirstOrDefaultAsync(ps => ps.PlayerId == playerId && ps.SkillId == skillId);

            if (playerSkill == null)
                return NotFound(ApiResponse<object>.Fail("Навык не найден у игрока."));

            // Здесь будет логика применения навыка в бою (через BattleHub)
            return Ok(ApiResponse<object>.Ok(
                new
                {
                    SkillName = playerSkill.Skill.Name,
                    Damage = playerSkill.Skill.Damage,
                    ManaCost = playerSkill.Skill.ManaCost
                },
                $"Навык {playerSkill.Skill.Name} использован."
            ));
        }

        [HttpDelete("forget/{playerId}/{skillId}")]
        [Authorize]
        public async Task<IActionResult> ForgetSkill(int playerId, int skillId)
        {
            var authError = EnsureOwnPlayerId(playerId);
            if (authError != null)
                return authError;

            var playerSkill = await _context.PlayerSkills
                .FirstOrDefaultAsync(ps => ps.PlayerId == playerId && ps.SkillId == skillId);

            if (playerSkill == null)
                return NotFound(ApiResponse<object>.Fail("Навык не найден у игрока."));

            _context.PlayerSkills.Remove(playerSkill);
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync($"player_skills_{playerId}");
            await _cache.RemoveAsync($"player_skill_{playerId}_{skillId}");

            return Ok(ApiResponse<object>.Ok(null, "Навык забыт."));
        }

        // Не даёт выполнить действие от имени чужого playerId,
        // даже если он подставлен в теле запроса или URL.
        private IActionResult? EnsureOwnPlayerId(int requestedPlayerId)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null || !int.TryParse(claim.Value, out var authenticatedPlayerId))
                return Unauthorized(ApiResponse<object>.Fail("Идентификатор пользователя не найден в токене."));

            if (authenticatedPlayerId != requestedPlayerId)
                return Forbid();

            return null;
        }
    }

    public class LearnSkillDto
    {
        public int PlayerId { get; set; }
        public int SkillId { get; set; }
    }
}