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
        private readonly StaticDataService _staticDataService;

        public QuestsController(GameDbContext context, IMapper mapper, StaticDataService staticDataService)
        {
            _context = context;
            _mapper = mapper;
            _staticDataService = staticDataService;
        }

        // ===================== ПОЛУЧЕНИЕ ВСЕХ КВЕСТОВ ИЗ JSON =====================

        [HttpGet]
        public async Task<IActionResult> GetQuests()
        {
            var quests = await _staticDataService.GetQuestsAsync();
            var questDtos = quests?.Select(q => _mapper.Map<QuestDto>(q)).ToList() ?? new List<QuestDto>();
            return Ok(ApiResponse<List<QuestDto>>.Ok(questDtos));
        }

        // ===================== ПОЛУЧЕНИЕ КВЕСТА ПО ID =====================

        [HttpGet("{id}")]
        public async Task<IActionResult> GetQuest(int id)
        {
            var quests = await _staticDataService.GetQuestsAsync();
            var quest = quests?.FirstOrDefault(q => q.Id == id);
            if (quest == null)
                return NotFound(ApiResponse<object>.Fail("Квест не найден."));

            var questDto = _mapper.Map<QuestDto>(quest);
            return Ok(ApiResponse<QuestDto>.Ok(questDto));
        }

        // ===================== ПОЛУЧЕНИЕ КВЕСТОВ ИГРОКА (ИЗ БД) =====================

        [HttpGet("player/{playerId}")]
        [Authorize]
        public async Task<IActionResult> GetPlayerQuests(int playerId)
        {
            var player = await _context.Players
                .Include(p => p.Quests)
                .FirstOrDefaultAsync(p => p.Id == playerId);

            if (player == null)
                return NotFound(ApiResponse<object>.Fail("Игрок не найден."));

            var quests = player.Quests.Select(q => _mapper.Map<QuestDto>(q)).ToList();
            return Ok(ApiResponse<List<QuestDto>>.Ok(quests));
        }

        // ===================== СОЗДАНИЕ КВЕСТА (ДЛЯ АДМИНА, В БД) =====================

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CreateQuest([FromBody] QuestDto questDto)
        {
            if (questDto == null)
                return BadRequest(ApiResponse<object>.Fail("Данные для создания квеста не предоставлены."));

            var quest = _mapper.Map<Quest>(questDto);
            _context.Quests.Add(quest);
            await _context.SaveChangesAsync();

            var createdQuestDto = _mapper.Map<QuestDto>(quest);
            return CreatedAtAction(nameof(GetQuest), new { id = createdQuestDto.Id },
                ApiResponse<QuestDto>.Ok(createdQuestDto, "Квест создан."));
        }

        // ===================== ОБНОВЛЕНИЕ КВЕСТА (ДЛЯ АДМИНА) =====================

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
                return Ok(ApiResponse<object>.Ok(null, "Квест обновлён."));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Quests.Any(e => e.Id == id))
                    return NotFound(ApiResponse<object>.Fail("Квест не найден."));
                throw;
            }
        }

        // ===================== УДАЛЕНИЕ КВЕСТА =====================

        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteQuest(int id)
        {
            var quest = await _context.Quests.FindAsync(id);
            if (quest == null)
                return NotFound(ApiResponse<object>.Fail("Квест не найден."));

            _context.Quests.Remove(quest);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.Ok(null, "Квест удалён."));
        }

        // ===================== ГРУППОВЫЕ КВЕСТЫ =====================

        [HttpPost("start-group-quest")]
        [Authorize]
        public async Task<IActionResult> StartGroupQuest([FromBody] StartGroupQuestDto dto)
        {
            var quests = await _staticDataService.GetQuestsAsync();
            var quest = quests?.FirstOrDefault(q => q.Id == dto.QuestId);
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
            return Ok(ApiResponse<object>.Ok(null, $"Групповой квест {quest.Name} запущен."));
        }

        [HttpPost("complete-group-quest")]
        [Authorize]
        public async Task<IActionResult> CompleteGroupQuest([FromBody] CompleteGroupQuestDto dto)
        {
            var quests = await _staticDataService.GetQuestsAsync();
            var quest = quests?.FirstOrDefault(q => q.Id == dto.QuestId);
            if (quest == null)
                return NotFound(ApiResponse<object>.Fail("Квест не найден."));

            if (!quest.IsGroupQuest)
                return BadRequest(ApiResponse<object>.Fail("Это не групповой квест."));

            var participants = await _context.QuestParticipants
                .Where(qp => qp.QuestId == quest.Id)
                .ToListAsync();

            if (!participants.Any())
                return BadRequest(ApiResponse<object>.Fail("Нет участников для этого квеста."));

            var allCompleted = true;
            var players = new List<Player>();

            foreach (var p in participants)
            {
                var player = await _context.Players.FindAsync(p.PlayerId);
                if (player == null) continue;

                if (!CheckQuestConditions(player, quest))
                {
                    allCompleted = false;
                    break;
                }
                players.Add(player);
            }

            if (!allCompleted)
                return BadRequest(ApiResponse<object>.Fail("Не все участники выполнили условия."));

            foreach (var player in players)
            {
                player.Coins += quest.Reward;
            }

            _context.QuestParticipants.RemoveRange(participants);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.Ok(null, $"Квест {quest.Name} выполнен! Все получили {quest.Reward} монет."));
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