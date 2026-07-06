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

        // ===================== ОСНОВНЫЕ МЕТОДЫ =====================

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetPlayers()
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

            return Ok(ApiResponse<List<PlayerDto>>.Ok(players ?? new List<PlayerDto>()));
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetPlayer(int id)
        {
            var cacheKey = $"player_{id}";
            var player = await _cache.GetAsync<PlayerDto>(cacheKey);

            if (player == null)
            {
                var dbPlayer = await _context.Players.FindAsync(id);
                if (dbPlayer == null)
                    return NotFound(ApiResponse<object>.Fail("Игрок не найден."));

                player = _mapper.Map<PlayerDto>(dbPlayer);
                await _cache.SetAsync(cacheKey, player, TimeSpan.FromMinutes(5));
            }

            return Ok(ApiResponse<PlayerDto>.Ok(player));
        }

        // ===================== УРОВЕНЬ =====================

        [HttpPut("{id}/level")]
        [Authorize]
        public async Task<IActionResult> UpdatePlayerLevel(int id, [FromBody] int newLevel)
        {
            if (newLevel < 0)
                return BadRequest(ApiResponse<object>.Fail("Уровень не может быть отрицательным."));

            var player = await _context.Players.FindAsync(id);
            if (player == null)
                return NotFound(ApiResponse<object>.Fail("Игрок не найден."));

            player.Level = newLevel;
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync($"player_{id}");
            await _cache.RemoveAsync("players_all");
            await _cache.RemoveAsync($"player_stats_{id}");

            return Ok(ApiResponse<PlayerDto>.Ok(_mapper.Map<PlayerDto>(player), "Уровень обновлён."));
        }

        // ===================== ИНВЕНТАРЬ =====================

        [HttpGet("{id}/inventory")]
        [Authorize]
        public async Task<IActionResult> GetPlayerInventory(int id)
        {
            var cacheKey = $"player_inventory_{id}";
            var inventory = await _cache.GetAsync<List<PlayerItemDto>>(cacheKey);

            if (inventory == null)
            {
                var player = await _context.Players
                    .Include(p => p.PlayerItems)
                        .ThenInclude(pi => pi.Item)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (player == null)
                    return NotFound(ApiResponse<object>.Fail("Игрок не найден."));

                inventory = player.PlayerItems.Select(pi => _mapper.Map<PlayerItemDto>(pi)).ToList();
                await _cache.SetAsync(cacheKey, inventory, TimeSpan.FromMinutes(5));
            }

            return Ok(ApiResponse<List<PlayerItemDto>>.Ok(inventory ?? new List<PlayerItemDto>()));
        }

        [HttpPost("{id}/add-item")]
        [Authorize]
        public async Task<IActionResult> AddItemToPlayer(int id, [FromBody] int itemId)
        {
            var player = await _context.Players.FindAsync(id);
            if (player == null)
                return NotFound(ApiResponse<object>.Fail("Игрок не найден."));

            var item = await _context.Items.FindAsync(itemId);
            if (item == null)
                return NotFound(ApiResponse<object>.Fail("Предмет не найден."));

            var existing = await _context.PlayerItems
                .FirstOrDefaultAsync(pi => pi.PlayerId == id && pi.ItemId == itemId);

            if (existing != null)
            {
                existing.Quantity++;
                _context.PlayerItems.Update(existing);
            }
            else
            {
                _context.PlayerItems.Add(new PlayerItem
                {
                    PlayerId = id,
                    ItemId = itemId,
                    Quantity = 1
                });
            }

            await _context.SaveChangesAsync();

            await _cache.RemoveAsync($"player_{id}");
            await _cache.RemoveAsync($"player_inventory_{id}");
            await _cache.RemoveAsync($"player_stats_{id}");

            return Ok(ApiResponse<object>.Ok(null, "Предмет добавлен в инвентарь."));
        }

        // ===================== ЭКИПИРОВКА =====================

        [HttpPut("{id}/equip/{itemId}")]
        [Authorize]
        public async Task<IActionResult> EquipItem(int id, int itemId)
        {
            var playerItem = await _context.PlayerItems
                .Include(pi => pi.Item)
                .FirstOrDefaultAsync(pi => pi.PlayerId == id && pi.ItemId == itemId);

            if (playerItem == null)
                return NotFound(ApiResponse<object>.Fail("Предмет не найден у игрока."));

            var item = playerItem.Item;
            if (item == null)
                return NotFound(ApiResponse<object>.Fail("Предмет не найден."));

            // Снимаем все предметы того же слота
            var equippedItems = await _context.PlayerItems
                .Where(pi => pi.PlayerId == id && pi.IsEquipped && pi.Item.Slot == item.Slot)
                .ToListAsync();

            foreach (var equipped in equippedItems)
                equipped.IsEquipped = false;

            playerItem.IsEquipped = true;
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync($"player_{id}");
            await _cache.RemoveAsync($"player_inventory_{id}");
            await _cache.RemoveAsync($"player_stats_{id}");

            return Ok(ApiResponse<object>.Ok(null, "Предмет экипирован."));
        }

        [HttpPut("{id}/unequip/{itemId}")]
        [Authorize]
        public async Task<IActionResult> UnequipItem(int id, int itemId)
        {
            var playerItem = await _context.PlayerItems
                .FirstOrDefaultAsync(pi => pi.PlayerId == id && pi.ItemId == itemId);

            if (playerItem == null)
                return NotFound(ApiResponse<object>.Fail("Предмет не найден у игрока."));

            playerItem.IsEquipped = false;
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync($"player_{id}");
            await _cache.RemoveAsync($"player_inventory_{id}");
            await _cache.RemoveAsync($"player_stats_{id}");

            return Ok(ApiResponse<object>.Ok(null, "Предмет снят."));
        }

        // ===================== СТАТИСТИКА =====================

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
                    return NotFound(ApiResponse<object>.Fail("Игрок не найден."));

                stats = player.CalculateTotalStats();
                await _cache.SetAsync(cacheKey, stats, TimeSpan.FromMinutes(5));
            }

            return Ok(ApiResponse<Dictionary<string, double>>.Ok(stats ?? new Dictionary<string, double>()));
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
                    return NotFound(ApiResponse<object>.Fail("Игрок не найден."));

                stats = new
                {
                    player.PvP_Wins,
                    player.PvP_Losses,
                    player.PvP_Kills,
                    player.PvP_Deaths
                };

                await _cache.SetAsync(cacheKey, stats, TimeSpan.FromMinutes(5));
            }

            return Ok(ApiResponse<object>.Ok(stats));
        }
    }
}