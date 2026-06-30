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
    public class PlayersController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _cache;

        public PlayersController(GameDbContext context, IMapper mapper, IMemoryCache cache)
        {
            _context = context;
            _mapper = mapper;
            _cache = cache;
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<IEnumerable<PlayerDto>>> GetPlayers()
        {
            if (!_cache.TryGetValue("Players", out IEnumerable<PlayerDto>? players))
            {
                players = await _context.Players
                    .Select(p => _mapper.Map<PlayerDto>(p))
                    .ToListAsync();

                _cache.Set("Players", players ?? new List<PlayerDto>(), TimeSpan.FromMinutes(10));
            }

            return Ok(players ?? new List<PlayerDto>());
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<PlayerDto>> GetPlayer(int id)
        {
            var player = await _context.Players.FindAsync(id);
            if (player == null)
            {
                return NotFound("Игрок не найден.");
            }

            return Ok(_mapper.Map<PlayerDto>(player));
        }

        [HttpPut("{id}/level")]
        [Authorize]
        public async Task<IActionResult> UpdatePlayerLevel(int id, [FromBody] int newLevel)
        {
            if (newLevel < 0)
            {
                return BadRequest("Уровень не может быть отрицательным.");
            }

            var player = await _context.Players.FindAsync(id);
            if (player == null)
            {
                return NotFound("Игрок не найден.");
            }

            player.Level = newLevel;
            await _context.SaveChangesAsync();

            return Ok(_mapper.Map<PlayerDto>(player));
        }

        [HttpPost("{id}/add-item")]
        [Authorize]
        public async Task<IActionResult> AddItemToPlayer(int id, [FromBody] int itemId)
        {
            var player = await _context.Players.FindAsync(id);
            if (player == null)
            {
                return NotFound("Игрок не найден.");
            }

            var item = await _context.Items.FindAsync(itemId);
            if (item == null)
            {
                return NotFound("Предмет не найден.");
            }

            var playerItem = new PlayerItem
            {
                PlayerId = id,
                ItemId = itemId,
                Quantity = 1
            };

            _context.PlayerItems.Add(playerItem);
            await _context.SaveChangesAsync();

            return Ok(_mapper.Map<PlayerDto>(player));
        }

        [HttpGet("{id}/stats")]
        [Authorize]
        public async Task<IActionResult> GetPlayerStats(int id)
        {
            var player = await _context.Players
                .Include(p => p.PlayerItems)
                .ThenInclude(pi => pi.Item)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (player == null)
            {
                return NotFound("Игрок не найден.");
            }

            var totalStats = player.CalculateTotalStats();
            return Ok(totalStats);
        }
    }
}