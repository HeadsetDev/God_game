using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Data;
using GameAuthAPI.DTOs;
using GameAuthAPI.Models;
using GameAuthAPI.Services;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuestsController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly IMapper _mapper;
        private readonly RedisCacheService _cache;

        public QuestsController(GameDbContext context, IMapper mapper, RedisCacheService cache)
        {
            _context = context;
            _mapper = mapper;
            _cache = cache;
        }

        // ===================== ОСНОВНЫЕ МЕТОДЫ =====================

        [HttpGet]
        public async Task<IActionResult> GetQuests()
        {
            const string cacheKey = "quests_all";
            var quests = await _cache.GetAsync<List<QuestDto>>(cacheKey);

            if (quests == null)
            {
                quests = await _context.Quests
                    .Select(q => _mapper.Map<QuestDto>(q))
                    .ToListAsync();
                await _cache.SetAsync(cacheKey, quests, TimeSpan.FromMinutes(10));
            }

            return Ok(ApiResponse<List<QuestDto>>.Ok(quests ?? new List<QuestDto>()));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetQuest(int id)
        {
            var cacheKey = $"quest_{id}";
            var quest = await _cache.GetAsync<QuestDto>(cacheKey);

            if (quest == null)
            {
                var dbQuest = await _context.Quests.FindAsync(id);
                if (dbQuest == null)
                    return NotFound(ApiResponse<object>.Fail("Квест не найден."));

                quest = _mapper.Map<QuestDto>(dbQuest);
                await _cache.SetAsync(cacheKey, quest, TimeSpan.FromMinutes(10));
            }

            return Ok(ApiResponse<QuestDto>.Ok(quest));
        }

        [HttpGet("player/{playerId}")]
        [Authorize]
        public async Task<IActionResult> GetPlayerQuests(int playerId)
        {
            var cacheKey = $"player_quests_{playerId}";
            var quests = await _cache.GetAsync<List<QuestDto>>(cacheKey);

            if (quests == null)
            {
                var player = await _context.Players
                    .Include(p => p.Quests)
                    .FirstOrDefaultAsync(p => p.Id == playerId);

                if (player == null)
                    return NotFound(ApiResponse<object>.Fail("Игрок не найден."));

                quests = player.Quests.Select(q => _mapper.Map<QuestDto>(q)).ToList();
                await _cache.SetAsync(cacheKey, quests, TimeSpan.FromMinutes(5));
            }

            return Ok(ApiResponse<List<QuestDto>>.Ok(quests ?? new List<QuestDto>()));
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CreateQuest([FromBody] QuestDto questDto)
        {
            if (questDto == null)
                return BadRequest(ApiResponse<object>.Fail("Данные для создания квеста не предоставлены."));

            var quest = _mapper.Map<Quest>(questDto);
            _context.Quests.Add(quest);
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync("quests_all");
            var createdQuestDto = _mapper.Map<QuestDto>(quest);

            return CreatedAtAction(nameof(GetQuest), new { id = createdQuestDto.Id },
                ApiResponse<QuestDto>.Ok(createdQuestDto, "Квест создан."));
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UpdateQuest(int id, [FromBody] QuestDto questDto)
        {
            if (id != questDto.Id)
                return BadRequest(ApiResponse<object>.Fail("ID квеста не совпадает."));

            var quest = await _context.Quests.FindAsync(id);
            if (quest == null)
                return NotFound(ApiResponse<object>.Fail("Квест не найден."));

            _mapper.Map(questDto, quest);
            _context.Entry(quest).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                await _cache.RemoveAsync($"quest_{id}");
                await _cache.RemoveAsync("quests_all");
                return Ok(ApiResponse<object>.Ok(null, "Квест обновлён."));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Quests.Any(e => e.Id == id))
                    return NotFound(ApiResponse<object>.Fail("Квест не найден."));
                throw;
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteQuest(int id)
        {
            var quest = await _context.Quests.FindAsync(id);
            if (quest == null)
                return NotFound(ApiResponse<object>.Fail("Квест не найден."));

            _context.Quests.Remove(quest);
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync($"quest_{id}");
            await _cache.RemoveAsync("quests_all");

            return Ok(ApiResponse<object>.Ok(null, "Квест удалён."));
        }

        // ===================== ГРУППОВЫЕ КВЕСТЫ =====================

        [HttpPost("start-group-quest")]
        [Authorize]
        public async Task<IActionResult> StartGroupQuest([FromBody] StartGroupQuestDto dto)
        {
            var quest = await _context.Quests.FindAsync(dto.QuestId);
            if (quest == null)
                return NotFound(ApiResponse<object>.Fail("Квест не найден."));

            if (!quest.IsGroupQuest)
                return BadRequest(ApiResponse<object>.Fail("Это не групповой квест."));

            if (dto.PlayerIds.Count < quest.RequiredPlayers)
                return BadRequest(ApiResponse<object>.Fail($"Нужно минимум {quest.RequiredPlayers} игроков."));

            var existing = await _context.QuestParticipants
                .AnyAsync(qp => qp.QuestId == quest.Id);
            if (existing)
                return BadRequest(ApiResponse<object>.Fail("Квест уже запущен."));

            foreach (var playerId in dto.PlayerIds)
            {
                _context.QuestParticipants.Add(new QuestParticipant
                {
                    QuestId = quest.Id,
                    PlayerId = playerId
                });
            }

            await _context.SaveChangesAsync();
            await _cache.RemoveAsync($"quest_{quest.Id}");
            await _cache.RemoveAsync("quests_all");

            return Ok(ApiResponse<object>.Ok(null, $"Групповой квест {quest.Name} запущен."));
        }

        [HttpPost("complete-group-quest")]
        [Authorize]
        public async Task<IActionResult> CompleteGroupQuest([FromBody] CompleteGroupQuestDto dto)
        {
            var quest = await _context.Quests
                .Include(q => q.QuestParticipants)
                .FirstOrDefaultAsync(q => q.Id == dto.QuestId);

            if (quest == null)
                return NotFound(ApiResponse<object>.Fail("Квест не найден."));

            if (!quest.IsGroupQuest || !quest.QuestParticipants.Any())
                return BadRequest(ApiResponse<object>.Fail("Нет участников для квеста."));

            var allCompleted = true;
            var participants = new List<Player>();

            foreach (var participant in quest.QuestParticipants)
            {
                var player = await _context.Players.FindAsync(participant.PlayerId);
                if (player == null) continue;

                if (!CheckQuestConditions(player, quest))
                {
                    allCompleted = false;
                    break;
                }
                participants.Add(player);
            }

            if (!allCompleted)
                return BadRequest(ApiResponse<object>.Fail("Не все участники выполнили условия."));

            foreach (var player in participants)
            {
                player.Coins += quest.Reward;
                if (!player.Quests.Contains(quest))
                    player.Quests.Add(quest);
            }

            quest.IsCompleted = true;
            await _context.SaveChangesAsync();

            foreach (var player in participants)
                await _cache.RemoveAsync($"player_quests_{player.Id}");

            await _cache.RemoveAsync($"quest_{quest.Id}");
            await _cache.RemoveAsync("quests_all");

            return Ok(ApiResponse<object>.Ok(null,
                $"Квест {quest.Name} выполнен! Все получили {quest.Reward} монет."));
        }

        private bool CheckQuestConditions(Player player, Quest quest)
        {
            foreach (var condition in quest.Conditions)
            {
                if (condition.Key == "MonstersKilled" && player.PlayerKills < condition.Value)
                    return false;
            }
            return true;
        }
    }
}