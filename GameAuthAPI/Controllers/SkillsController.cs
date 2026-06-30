using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GameAuthAPI.Data;
using GameAuthAPI.Models;
using GameAuthAPI.Services;
using Microsoft.AspNetCore.Authorization;

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

            return Ok(skills);
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
                {
                    return NotFound("Навык не найден.");
                }

                await _cache.SetAsync(cacheKey, skill, TimeSpan.FromMinutes(30));
            }

            return Ok(skill);
        }

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
                {
                    return NotFound("Игрок не найден.");
                }

                skills = player.PlayerSkills.Select(ps => ps.Skill).ToList();
                await _cache.SetAsync(cacheKey, skills, TimeSpan.FromMinutes(5));
            }

            return Ok(skills);
        }

        [HttpPost("learn")]
        [Authorize]
        public async Task<IActionResult> LearnSkill([FromBody] LearnSkillDto dto)
        {
            var player = await _context.Players.FindAsync(dto.PlayerId);
            if (player == null)
            {
                return NotFound("Игрок не найден.");
            }

            var skill = await _context.Skills.FindAsync(dto.SkillId);
            if (skill == null)
            {
                return NotFound("Навык не найден.");
            }

            if (player.Level < skill.RequiredLevel)
            {
                return BadRequest("Уровень игрока недостаточен для изучения этого навыка.");
            }

            var existing = await _context.PlayerSkills
                .FirstOrDefaultAsync(ps => ps.PlayerId == dto.PlayerId && ps.SkillId == dto.SkillId);

            if (existing != null)
            {
                return BadRequest("Навык уже изучен.");
            }

            _context.PlayerSkills.Add(new PlayerSkill
            {
                PlayerId = dto.PlayerId,
                SkillId = dto.SkillId
            });

            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync($"player_skills_{dto.PlayerId}");

            return Ok("Навык успешно изучен.");
        }

        [HttpPost("use/{playerId}/{skillId}")]
        [Authorize]
        public async Task<IActionResult> UseSkill(int playerId, int skillId)
        {
            var cacheKey = $"player_skill_{playerId}_{skillId}";
            var playerSkill = await _cache.GetAsync<PlayerSkill>(cacheKey);

            if (playerSkill == null)
            {
                playerSkill = await _context.PlayerSkills
                    .Include(ps => ps.Skill)
                    .FirstOrDefaultAsync(ps => ps.PlayerId == playerId && ps.SkillId == skillId);

                if (playerSkill == null)
                {
                    return NotFound("Навык не найден у игрока.");
                }

                await _cache.SetAsync(cacheKey, playerSkill, TimeSpan.FromMinutes(1));
            }

            return Ok($"Игрок {playerId} использовал навык {playerSkill.Skill.Name}!");
        }

        [HttpDelete("forget/{playerId}/{skillId}")]
        [Authorize]
        public async Task<IActionResult> ForgetSkill(int playerId, int skillId)
        {
            var playerSkill = await _context.PlayerSkills
                .FirstOrDefaultAsync(ps => ps.PlayerId == playerId && ps.SkillId == skillId);

            if (playerSkill == null)
            {
                return NotFound("Навык не найден у игрока.");
            }

            _context.PlayerSkills.Remove(playerSkill);
            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync($"player_skills_{playerId}");
            await _cache.RemoveAsync($"player_skill_{playerId}_{skillId}");

            return Ok("Навык забыт.");
        }
    }

    public class LearnSkillDto
    {
        public int PlayerId { get; set; }
        public int SkillId { get; set; }
    }
}