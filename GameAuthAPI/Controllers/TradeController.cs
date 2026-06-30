using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Data;
using GameAuthAPI.DTOs;
using GameAuthAPI.Models;
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

        public TradeController(GameDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
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

            return Ok(_mapper.Map<PlayerTradeDto>(trade));
        }

        [HttpPost("confirm")]
        [Authorize]
        public async Task<IActionResult> ConfirmTrade([FromBody] ConfirmTradeDto confirmTrade)
        {
            var trade = await _context.PlayerTrades
                .Include(t => t.Player1Items)
                .Include(t => t.Player2Items)
                .FirstOrDefaultAsync(t => t.Id == confirmTrade.TradeId);

            if (trade == null)
            {
                return NotFound("Сделка не найдена.");
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
            return Ok(_mapper.Map<PlayerTradeDto>(trade));
        }
    }
}