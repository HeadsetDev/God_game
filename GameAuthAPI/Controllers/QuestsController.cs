using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Data;
using GameAuthAPI.DTOs;
using GameAuthAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuestsController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _cache;

        public QuestsController(GameDbContext context, IMapper mapper, IMemoryCache cache)
        {
            _context = context;
            _mapper = mapper;
            _cache = cache;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<QuestDto>>> GetQuests()
        {
            if (!_cache.TryGetValue("Quests", out IEnumerable<QuestDto>? quests))
            {
                quests = await _context.Quests
                    .Select(q => _mapper.Map<QuestDto>(q))
                    .ToListAsync();

                _cache.Set("Quests", quests ?? new List<QuestDto>(), TimeSpan.FromMinutes(10));
            }

            return Ok(quests ?? new List<QuestDto>());
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<QuestDto>> GetQuest(int id)
        {
            var quest = await _context.Quests.FindAsync(id);
            if (quest == null)
            {
                return NotFound();
            }

            return Ok(_mapper.Map<QuestDto>(quest));
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

        [HttpPost("start-group-quest")]
        [Authorize]
        public async Task<IActionResult> StartGroupQuest([FromBody] StartGroupQuestDto startGroupQuestDto)
        {
            var quest = await _context.Quests.FindAsync(startGroupQuestDto.QuestId);
            if (quest == null)
            {
                return NotFound("Quest not found.");
            }

            // Проверка, что квест является групповым
            if (!quest.IsGroupQuest)
            {
                return BadRequest("This quest is not a group quest.");
            }

            // Проверка, что группа собрана
            var participants = startGroupQuestDto.PlayerIds.Count;
            if (participants < quest.RequiredPlayers)
            {
                return BadRequest("Not enough players to start the quest.");
            }

            // Добавляем участников квеста
            foreach (var playerId in startGroupQuestDto.PlayerIds)
            {
                _context.QuestParticipants.Add(new QuestParticipant
                {
                    QuestId = quest.Id,
                    PlayerId = playerId
                });
            }

            await _context.SaveChangesAsync();

            return Ok($"Group quest {quest.Name} started with {participants} players.");
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

            return NoContent();
        }
    }
}