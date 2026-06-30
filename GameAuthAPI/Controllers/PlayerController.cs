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
using Microsoft.Extensions.Caching.Memory;

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlayersController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly IMapper _mapper;
        private readonly RedisCacheService _cache;

        public PlayersController(GameDbContext context, IMapper mapper, RedisCacheService cache)
        {
            _context = context;
            _mapper = mapper;
            _cache = cache;
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<IEnumerable<PlayerDto>>> GetPlayers()
        {
            const string cacheKey = "players_all";

            var players = await _cache.GetAsync<List<PlayerDto>>(cacheKey);

            if (players == null)
            {
                players = await _context.Players
                    .Select(p => _mapper.Map<PlayerDto>(p))
                    .ToListAsync();

                await _cache.SetAsync(cacheKey, players, TimeSpan.FromMinutes(5));
            }

            return Ok(players ?? new List<PlayerDto>());
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<PlayerDto>> GetPlayer(int id)
        {
            var cacheKey = $"player_{id}";
            var player = await _cache.GetAsync<PlayerDto>(cacheKey);

            if (player == null)
            {
                var dbPlayer = await _context.Players.FindAsync(id);
                if (dbPlayer == null)
                {
                    return NotFound("Игрок не найден.");
                }

                player = _mapper.Map<PlayerDto>(dbPlayer);
                await _cache.SetAsync(cacheKey, player, TimeSpan.FromMinutes(5));
            }

            return Ok(player);
        }

        [HttpGet("{id}/stats")]
        [Authorize]
        public async Task<IActionResult> GetPlayerStats(int id)
        {
            var cacheKey = $"player_stats_{id}";
            var stats = await _cache.GetAsync<Dictionary<string, double>>(cacheKey);

            if (stats == null)
            {
                var player = await _context.Players
                    .Include(p => p.PlayerItems)
                        .ThenInclude(pi => pi.Item)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (player == null)
                {
                    return NotFound("Игрок не найден.");
                }

                stats = player.CalculateTotalStats();
                await _cache.SetAsync(cacheKey, stats, TimeSpan.FromMinutes(5));
            }

            return Ok(stats);
        }

        [HttpGet("{id}/inventory")]
        [Authorize]
        public async Task<IActionResult> GetPlayerInventory(int id)
        {
            var cacheKey = $"player_inventory_{id}";
            var inventory = await _cache.GetAsync<List<PlayerItem>>(cacheKey);

            if (inventory == null)
            {
                var player = await _context.Players
                    .Include(p => p.PlayerItems)
                        .ThenInclude(pi => pi.Item)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (player == null)
                {
                    return NotFound("Игрок не найден.");
                }

                inventory = player.PlayerItems;
                await _cache.SetAsync(cacheKey, inventory, TimeSpan.FromMinutes(5));
            }

            return Ok(inventory);
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

            // Инвалидируем кэш
            await _cache.RemoveAsync($"player_{id}");
            await _cache.RemoveAsync($"player_stats_{id}");
            await _cache.RemoveAsync("players_all");

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

            // Инвалидируем кэш
            await _cache.RemoveAsync($"player_{id}");
            await _cache.RemoveAsync($"player_inventory_{id}");
            await _cache.RemoveAsync($"player_stats_{id}");

            return Ok(_mapper.Map<PlayerDto>(player));
        }

        [HttpPut("{id}/equip/{itemId}")]
        [Authorize]
        public async Task<IActionResult> EquipItem(int id, int itemId)
        {
            var playerItem = await _context.PlayerItems
                .FirstOrDefaultAsync(pi => pi.PlayerId == id && pi.ItemId == itemId);

            if (playerItem == null)
            {
                return NotFound("Предмет не найден у игрока.");
            }

            // Снимаем все предметы того же типа (слота)
            var item = await _context.Items.FindAsync(itemId);
            if (item == null)
            {
                return NotFound("Предмет не найден.");
            }

            var equippedItems = await _context.PlayerItems
                .Where(pi => pi.PlayerId == id && pi.IsEquipped && pi.Item.Slot == item.Slot)
                .ToListAsync();

            foreach (var equipped in equippedItems)
            {
                equipped.IsEquipped = false;
            }

            playerItem.IsEquipped = true;
            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync($"player_{id}");
            await _cache.RemoveAsync($"player_inventory_{id}");
            await _cache.RemoveAsync($"player_stats_{id}");

            return Ok("Предмет экипирован.");
        }

        [HttpPut("{id}/unequip/{itemId}")]
        [Authorize]
        public async Task<IActionResult> UnequipItem(int id, int itemId)
        {
            var playerItem = await _context.PlayerItems
                .FirstOrDefaultAsync(pi => pi.PlayerId == id && pi.ItemId == itemId);

            if (playerItem == null)
            {
                return NotFound("Предмет не найден у игрока.");
            }

            playerItem.IsEquipped = false;
            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync($"player_{id}");
            await _cache.RemoveAsync($"player_inventory_{id}");
            await _cache.RemoveAsync($"player_stats_{id}");

            return Ok("Предмет снят.");
        }

        [HttpGet("{id}/pvp-stats")]
        [Authorize]
        public async Task<IActionResult> GetPvPStats(int id)
        {
            var cacheKey = $"player_pvp_{id}";
            var stats = await _cache.GetAsync<object>(cacheKey);

            if (stats == null)
            {
                var player = await _context.Players.FindAsync(id);
                if (player == null)
                {
                    return NotFound("Игрок не найден.");
                }

                stats = new
                {
                    player.PvP_Wins,
                    player.PvP_Losses,
                    player.PvP_Kills,
                    player.PvP_Deaths
                };

                await _cache.SetAsync(cacheKey, stats, TimeSpan.FromMinutes(5));
            }

            return Ok(stats);
        }
    }
}