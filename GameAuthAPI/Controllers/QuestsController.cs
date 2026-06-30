using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Data;
using GameAuthAPI.DTOs;
using GameAuthAPI.Models;
using GameAuthAPI.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<QuestDto>>> GetQuests()
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

            return Ok(quests ?? new List<QuestDto>());
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<QuestDto>> GetQuest(int id)
        {
            var cacheKey = $"quest_{id}";
            var quest = await _cache.GetAsync<QuestDto>(cacheKey);

            if (quest == null)
            {
                var dbQuest = await _context.Quests.FindAsync(id);
                if (dbQuest == null)
                {
                    return NotFound();
                }

                quest = _mapper.Map<QuestDto>(dbQuest);
                await _cache.SetAsync(cacheKey, quest, TimeSpan.FromMinutes(10));
            }

            return Ok(quest);
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
                {
                    return NotFound("Игрок не найден.");
                }

                quests = player.Quests.Select(q => _mapper.Map<QuestDto>(q)).ToList();
                await _cache.SetAsync(cacheKey, quests, TimeSpan.FromMinutes(5));
            }

            return Ok(quests);
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<QuestDto>> CreateQuest([FromBody] QuestDto questDto)
        {
            if (questDto == null)
            {
                return BadRequest("Данные для создания квеста не предоставлены.");
            }

            var quest = _mapper.Map<Quest>(questDto);
            _context.Quests.Add(quest);
            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync("quests_all");

            var createdQuestDto = _mapper.Map<QuestDto>(quest);
            return CreatedAtAction(nameof(GetQuest), new { id = createdQuestDto.Id }, createdQuestDto);
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateQuest(int id, [FromBody] QuestDto questDto)
        {
            if (id != questDto.Id)
            {
                return BadRequest("ID квеста не совпадает.");
            }

            var quest = await _context.Quests.FindAsync(id);
            if (quest == null)
            {
                return NotFound();
            }

            _mapper.Map(questDto, quest);
            _context.Entry(quest).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();

                // Инвалидируем кэш
                await _cache.RemoveAsync($"quest_{id}");
                await _cache.RemoveAsync("quests_all");
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Quests.Any(e => e.Id == id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteQuest(int id)
        {
            var quest = await _context.Quests.FindAsync(id);
            if (quest == null)
            {
                return NotFound();
            }

            _context.Quests.Remove(quest);
            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync($"quest_{id}");
            await _cache.RemoveAsync("quests_all");

            return NoContent();
        }

        // ===================== ГРУППОВЫЕ КВЕСТЫ =====================

        [HttpPost("start-group-quest")]
        [Authorize]
        public async Task<IActionResult> StartGroupQuest([FromBody] StartGroupQuestDto startGroupQuestDto)
        {
            var quest = await _context.Quests.FindAsync(startGroupQuestDto.QuestId);
            if (quest == null)
            {
                return NotFound("Quest not found.");
            }

            if (!quest.IsGroupQuest)
            {
                return BadRequest("This quest is not a group quest.");
            }

            var participants = startGroupQuestDto.PlayerIds.Count;
            if (participants < quest.RequiredPlayers)
            {
                return BadRequest("Not enough players to start the quest.");
            }

            var existingParticipants = await _context.QuestParticipants
                .Where(qp => qp.QuestId == quest.Id)
                .ToListAsync();

            if (existingParticipants.Any())
            {
                return BadRequest("This quest already has participants.");
            }

            foreach (var playerId in startGroupQuestDto.PlayerIds)
            {
                _context.QuestParticipants.Add(new QuestParticipant
                {
                    QuestId = quest.Id,
                    PlayerId = playerId
                });
            }

            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync($"quest_{quest.Id}");
            await _cache.RemoveAsync("quests_all");

            return Ok($"Group quest {quest.Name} started with {participants} players.");
        }

        [HttpPost("complete-group-quest")]
        [Authorize]
        public async Task<IActionResult> CompleteGroupQuest([FromBody] CompleteGroupQuestDto dto)
        {
            var quest = await _context.Quests
                .Include(q => q.QuestParticipants)
                .FirstOrDefaultAsync(q => q.Id == dto.QuestId);

            if (quest == null)
            {
                return NotFound("Квест не найден.");
            }

            if (!quest.IsGroupQuest)
            {
                return BadRequest("Это не групповой квест.");
            }

            if (quest.QuestParticipants == null || !quest.QuestParticipants.Any())
            {
                return BadRequest("Нет участников для этого квеста.");
            }

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
            {
                return BadRequest("Не все участники выполнили условия квеста.");
            }

            foreach (var player in participants)
            {
                player.Coins += quest.Reward;
                if (!player.Quests.Contains(quest))
                {
                    player.Quests.Add(quest);
                }
            }

            quest.IsCompleted = true;
            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync($"quest_{quest.Id}");
            await _cache.RemoveAsync("quests_all");
            foreach (var player in participants)
            {
                await _cache.RemoveAsync($"player_quests_{player.Id}");
            }

            return Ok($"Групповой квест {quest.Name} выполнен! Все участники получили {quest.Reward} монет.");
        }

        private bool CheckQuestConditions(Player player, Quest quest)
        {
            foreach (var condition in quest.Conditions)
            {
                if (condition.Key == "MonstersKilled" && player.PlayerKills < condition.Value)
                {
                    return false;
                }
            }
            return true;
        }
    }
}