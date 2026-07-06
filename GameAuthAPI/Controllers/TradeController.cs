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
    public class TradeController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly IMapper _mapper;
        private readonly RedisCacheService _cache;

        public TradeController(GameDbContext context, IMapper mapper, RedisCacheService cache)
        {
            _context = context;
            _mapper = mapper;
            _cache = cache;
        }

        // ===================== ИНИЦИАЦИЯ СДЕЛКИ =====================

        [HttpPost("initiate")]
        [Authorize]
        public async Task<IActionResult> InitiateTrade([FromBody] TradeRequestDto tradeRequest)
        {
            if (tradeRequest == null)
                return BadRequest(ApiResponse<object>.Fail("Данные для инициирования сделки не предоставлены."));

            if (tradeRequest.Player1Id == tradeRequest.Player2Id)
                return BadRequest(ApiResponse<object>.Fail("Нельзя торговать с самим собой."));

            var player1 = await _context.Players.FindAsync(tradeRequest.Player1Id);
            var player2 = await _context.Players.FindAsync(tradeRequest.Player2Id);

            if (player1 == null || player2 == null)
                return NotFound(ApiResponse<object>.Fail("Один из игроков не найден."));

            // Проверяем, нет ли уже активной сделки между этими игроками
            var existingTrade = await _context.PlayerTrades
                .FirstOrDefaultAsync(t =>
                    (t.Player1Id == tradeRequest.Player1Id && t.Player2Id == tradeRequest.Player2Id ||
                     t.Player1Id == tradeRequest.Player2Id && t.Player2Id == tradeRequest.Player1Id) &&
                    !t.IsCompleted);

            if (existingTrade != null)
                return BadRequest(ApiResponse<object>.Fail("У вас уже есть активная сделка с этим игроком."));

            var trade = new PlayerTrade
            {
                Player1Id = tradeRequest.Player1Id,
                Player2Id = tradeRequest.Player2Id,
                IsConfirmedByPlayer1 = false,
                IsConfirmedByPlayer2 = false,
                IsCompleted = false
            };

            _context.PlayerTrades.Add(trade);
            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync($"trade_{trade.Id}");
            await _cache.RemoveAsync($"player_trades_{tradeRequest.Player1Id}");
            await _cache.RemoveAsync($"player_trades_{tradeRequest.Player2Id}");

            var tradeDto = _mapper.Map<PlayerTradeDto>(trade);
            return Ok(ApiResponse<PlayerTradeDto>.Ok(tradeDto, "Сделка инициирована."));
        }

        // ===================== ПОДТВЕРЖДЕНИЕ СДЕЛКИ =====================

        [HttpPost("confirm")]
        [Authorize]
        public async Task<IActionResult> ConfirmTrade([FromBody] ConfirmTradeDto confirmTrade)
        {
            if (confirmTrade == null)
                return BadRequest(ApiResponse<object>.Fail("Данные для подтверждения не предоставлены."));

            var trade = await _context.PlayerTrades
                .Include(t => t.Player1Items)
                .Include(t => t.Player2Items)
                .FirstOrDefaultAsync(t => t.Id == confirmTrade.TradeId);

            if (trade == null)
                return NotFound(ApiResponse<object>.Fail("Сделка не найдена."));

            if (trade.IsCompleted)
                return BadRequest(ApiResponse<object>.Fail("Сделка уже завершена."));

            if (confirmTrade.PlayerId == trade.Player1Id)
                trade.IsConfirmedByPlayer1 = true;
            else if (confirmTrade.PlayerId == trade.Player2Id)
                trade.IsConfirmedByPlayer2 = true;
            else
                return BadRequest(ApiResponse<object>.Fail("Игрок не является участником сделки."));

            // Если оба подтвердили — выполняем обмен
            if (trade.IsConfirmedByPlayer1 && trade.IsConfirmedByPlayer2)
            {
                // Перемещаем предметы
                foreach (var item in trade.Player1Items)
                {
                    item.PlayerId = trade.Player2Id;
                    _context.PlayerItems.Update(item);
                }

                foreach (var item in trade.Player2Items)
                {
                    item.PlayerId = trade.Player1Id;
                    _context.PlayerItems.Update(item);
                }

                trade.IsCompleted = true;
                await _context.SaveChangesAsync();

                // Инвалидируем кэш
                await _cache.RemoveAsync($"trade_{trade.Id}");
                await _cache.RemoveAsync($"player_trades_{trade.Player1Id}");
                await _cache.RemoveAsync($"player_trades_{trade.Player2Id}");
                await _cache.RemoveAsync($"player_inventory_{trade.Player1Id}");
                await _cache.RemoveAsync($"player_inventory_{trade.Player2Id}");

                return Ok(ApiResponse<object>.Ok(null, "Сделка успешно завершена! Предметы обменяны."));
            }

            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync($"trade_{trade.Id}");
            await _cache.RemoveAsync($"player_trades_{trade.Player1Id}");
            await _cache.RemoveAsync($"player_trades_{trade.Player2Id}");

            return Ok(ApiResponse<object>.Ok(null, "Подтверждение принято. Ожидайте подтверждения от второго игрока."));
        }

        // ===================== ДОБАВЛЕНИЕ ПРЕДМЕТА В СДЕЛКУ =====================

        [HttpPost("add-item")]
        [Authorize]
        public async Task<IActionResult> AddItemToTrade([FromBody] AddItemToTradeDto dto)
        {
            if (dto == null)
                return BadRequest(ApiResponse<object>.Fail("Данные не предоставлены."));

            var trade = await _context.PlayerTrades
                .Include(t => t.Player1Items)
                .Include(t => t.Player2Items)
                .FirstOrDefaultAsync(t => t.Id == dto.TradeId);

            if (trade == null)
                return NotFound(ApiResponse<object>.Fail("Сделка не найдена."));

            if (trade.IsCompleted)
                return BadRequest(ApiResponse<object>.Fail("Сделка уже завершена."));

            // Проверяем, что игрок является участником
            if (dto.PlayerId != trade.Player1Id && dto.PlayerId != trade.Player2Id)
                return BadRequest(ApiResponse<object>.Fail("Игрок не является участником сделки."));

            // Проверяем, что предмет принадлежит игроку
            var playerItem = await _context.PlayerItems
                .Include(pi => pi.Item)
                .FirstOrDefaultAsync(pi => pi.PlayerId == dto.PlayerId && pi.ItemId == dto.ItemId);

            if (playerItem == null)
                return NotFound(ApiResponse<object>.Fail("Предмет не найден у игрока."));

            // Добавляем предмет в сделку (в список соответствующего игрока)
            if (dto.PlayerId == trade.Player1Id)
            {
                if (trade.Player1Items.Any(i => i.ItemId == dto.ItemId))
                    return BadRequest(ApiResponse<object>.Fail("Предмет уже добавлен в сделку."));
                trade.Player1Items.Add(playerItem);
            }
            else
            {
                if (trade.Player2Items.Any(i => i.ItemId == dto.ItemId))
                    return BadRequest(ApiResponse<object>.Fail("Предмет уже добавлен в сделку."));
                trade.Player2Items.Add(playerItem);
            }

            // Сбрасываем подтверждения, т.к. состав сделки изменился
            trade.IsConfirmedByPlayer1 = false;
            trade.IsConfirmedByPlayer2 = false;

            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync($"trade_{trade.Id}");
            await _cache.RemoveAsync($"player_trades_{trade.Player1Id}");
            await _cache.RemoveAsync($"player_trades_{trade.Player2Id}");

            return Ok(ApiResponse<object>.Ok(null, "Предмет добавлен в сделку."));
        }

        // ===================== УДАЛЕНИЕ ПРЕДМЕТА ИЗ СДЕЛКИ =====================

        [HttpPost("remove-item")]
        [Authorize]
        public async Task<IActionResult> RemoveItemFromTrade([FromBody] RemoveItemFromTradeDto dto)
        {
            if (dto == null)
                return BadRequest(ApiResponse<object>.Fail("Данные не предоставлены."));

            var trade = await _context.PlayerTrades
                .Include(t => t.Player1Items)
                .Include(t => t.Player2Items)
                .FirstOrDefaultAsync(t => t.Id == dto.TradeId);

            if (trade == null)
                return NotFound(ApiResponse<object>.Fail("Сделка не найдена."));

            if (trade.IsCompleted)
                return BadRequest(ApiResponse<object>.Fail("Сделка уже завершена."));

            // Определяем, из какого списка удалять
            var itemList = dto.PlayerId == trade.Player1Id ? trade.Player1Items : trade.Player2Items;
            var itemToRemove = itemList.FirstOrDefault(i => i.ItemId == dto.ItemId);

            if (itemToRemove == null)
                return NotFound(ApiResponse<object>.Fail("Предмет не найден в сделке."));

            itemList.Remove(itemToRemove);

            // Сбрасываем подтверждения
            trade.IsConfirmedByPlayer1 = false;
            trade.IsConfirmedByPlayer2 = false;

            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync($"trade_{trade.Id}");
            await _cache.RemoveAsync($"player_trades_{trade.Player1Id}");
            await _cache.RemoveAsync($"player_trades_{trade.Player2Id}");

            return Ok(ApiResponse<object>.Ok(null, "Предмет удалён из сделки."));
        }

        // ===================== ОТМЕНА СДЕЛКИ =====================

        [HttpPost("cancel")]
        [Authorize]
        public async Task<IActionResult> CancelTrade([FromBody] CancelTradeDto dto)
        {
            if (dto == null)
                return BadRequest(ApiResponse<object>.Fail("Данные не предоставлены."));

            var trade = await _context.PlayerTrades
                .Include(t => t.Player1Items)
                .Include(t => t.Player2Items)
                .FirstOrDefaultAsync(t => t.Id == dto.TradeId);

            if (trade == null)
                return NotFound(ApiResponse<object>.Fail("Сделка не найдена."));

            if (trade.IsCompleted)
                return BadRequest(ApiResponse<object>.Fail("Сделка уже завершена."));

            if (dto.PlayerId != trade.Player1Id && dto.PlayerId != trade.Player2Id)
                return BadRequest(ApiResponse<object>.Fail("Игрок не является участником сделки."));

            // Удаляем сделку
            _context.PlayerTrades.Remove(trade);
            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync($"trade_{trade.Id}");
            await _cache.RemoveAsync($"player_trades_{trade.Player1Id}");
            await _cache.RemoveAsync($"player_trades_{trade.Player2Id}");

            return Ok(ApiResponse<object>.Ok(null, "Сделка отменена."));
        }

        // ===================== ПОЛУЧЕНИЕ ИНФОРМАЦИИ О СДЕЛКЕ =====================

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetTrade(int id)
        {
            var cacheKey = $"trade_{id}";
            var trade = await _cache.GetAsync<PlayerTradeDto>(cacheKey);

            if (trade == null)
            {
                var dbTrade = await _context.PlayerTrades
                    .Include(t => t.Player1Items)
                        .ThenInclude(pi => pi.Item)
                    .Include(t => t.Player2Items)
                        .ThenInclude(pi => pi.Item)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (dbTrade == null)
                    return NotFound(ApiResponse<object>.Fail("Сделка не найдена."));

                trade = _mapper.Map<PlayerTradeDto>(dbTrade);
                await _cache.SetAsync(cacheKey, trade, TimeSpan.FromMinutes(5));
            }

            return Ok(ApiResponse<PlayerTradeDto>.Ok(trade));
        }

        // ===================== ПОЛУЧЕНИЕ ВСЕХ СДЕЛОК ИГРОКА =====================

        [HttpGet("player/{playerId}")]
        [Authorize]
        public async Task<IActionResult> GetPlayerTrades(int playerId)
        {
            var cacheKey = $"player_trades_{playerId}";
            var trades = await _cache.GetAsync<List<PlayerTradeDto>>(cacheKey);

            if (trades == null)
            {
                var dbTrades = await _context.PlayerTrades
                    .Where(t => t.Player1Id == playerId || t.Player2Id == playerId)
                    .Include(t => t.Player1Items)
                    .Include(t => t.Player2Items)
                    .OrderByDescending(t => t.Id)
                    .ToListAsync();

                trades = dbTrades.Select(t => _mapper.Map<PlayerTradeDto>(t)).ToList();
                await _cache.SetAsync(cacheKey, trades, TimeSpan.FromMinutes(5));
            }

            return Ok(ApiResponse<List<PlayerTradeDto>>.Ok(trades ?? new List<PlayerTradeDto>()));
        }
    }

    // ===================== DTO ДЛЯ ДОБАВЛЕНИЯ/УДАЛЕНИЯ ПРЕДМЕТОВ =====================

    public class AddItemToTradeDto
    {
        public int TradeId { get; set; }
        public int PlayerId { get; set; }
        public int ItemId { get; set; }
    }

    public class RemoveItemFromTradeDto
    {
        public int TradeId { get; set; }
        public int PlayerId { get; set; }
        public int ItemId { get; set; }
    }

    public class CancelTradeDto
    {
        public int TradeId { get; set; }
        public int PlayerId { get; set; }
    }
}