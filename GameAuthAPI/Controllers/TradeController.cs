using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Data;
using GameAuthAPI.DTOs;
using GameAuthAPI.Models;
using GameAuthAPI.Services;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
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

        [HttpPost("initiate")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> InitiateTrade([FromBody] TradeRequestDto tradeRequest)
        {
            if (tradeRequest == null)
            {
                return BadRequest("Данные для инициирования сделки не предоставлены.");
            }

            var player1 = await _context.Players.FindAsync(tradeRequest.Player1Id);
            var player2 = await _context.Players.FindAsync(tradeRequest.Player2Id);

            if (player1 == null || player2 == null)
            {
                return NotFound("Один из игроков не найден.");
            }

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

            return Ok(_mapper.Map<PlayerTradeDto>(trade));
        }

        [HttpPost("confirm")]
        [Authorize]
        public async Task<IActionResult> ConfirmTrade([FromBody] ConfirmTradeDto confirmTrade)
        {
            var cacheKey = $"trade_{confirmTrade.TradeId}";
            var trade = await _cache.GetAsync<PlayerTrade>(cacheKey);

            if (trade == null)
            {
                trade = await _context.PlayerTrades
                    .Include(t => t.Player1Items)
                    .Include(t => t.Player2Items)
                    .FirstOrDefaultAsync(t => t.Id == confirmTrade.TradeId);

                if (trade == null)
                {
                    return NotFound("Сделка не найдена.");
                }

                await _cache.SetAsync(cacheKey, trade, TimeSpan.FromMinutes(5));
            }

            if (confirmTrade.PlayerId == trade.Player1Id)
            {
                trade.IsConfirmedByPlayer1 = true;
            }
            else if (confirmTrade.PlayerId == trade.Player2Id)
            {
                trade.IsConfirmedByPlayer2 = true;
            }
            else
            {
                return BadRequest("Игрок не является участником сделки.");
            }

            if (trade.IsConfirmedByPlayer1 && trade.IsConfirmedByPlayer2)
            {
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
            }

            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync($"trade_{trade.Id}");
            await _cache.RemoveAsync($"player_trades_{trade.Player1Id}");
            await _cache.RemoveAsync($"player_trades_{trade.Player2Id}");

            return Ok(_mapper.Map<PlayerTradeDto>(trade));
        }

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
                    .Include(t => t.Player2Items)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (dbTrade == null)
                {
                    return NotFound("Сделка не найдена.");
                }

                trade = _mapper.Map<PlayerTradeDto>(dbTrade);
                await _cache.SetAsync(cacheKey, trade, TimeSpan.FromMinutes(5));
            }

            return Ok(trade);
        }

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
                    .ToListAsync();

                trades = dbTrades.Select(t => _mapper.Map<PlayerTradeDto>(t)).ToList();
                await _cache.SetAsync(cacheKey, trades, TimeSpan.FromMinutes(5));
            }

            return Ok(trades ?? new List<PlayerTradeDto>());
        }
    }
}