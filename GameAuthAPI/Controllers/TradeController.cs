namespace GameAuthAPI.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using GameAuthAPI.Data;
    using GameAuthAPI.Models;
    using System.Threading.Tasks;
    using System.Linq;
    using Microsoft.EntityFrameworkCore;

    [Route("api/[controller]")]
    [ApiController]
    public class PlayerTradeController : ControllerBase
    {
        private readonly GameDbContext _context;

        public PlayerTradeController(GameDbContext context)
        {
            _context = context;
        }

        // Инициировать сделку
        [HttpPost("start-trade")]
        public async Task<IActionResult> StartTrade(int player1Id, int player2Id)
        {
            var player1 = await _context.Players.FindAsync(player1Id);
            var player2 = await _context.Players.FindAsync(player2Id);

            if (player1 == null || player2 == null)
            {
                return NotFound("Игроки не найдены.");
            }

            var trade = new PlayerTrade
            {
                Player1Id = player1Id,
                Player2Id = player2Id,
                IsConfirmedByPlayer1 = false,
                IsConfirmedByPlayer2 = false,
                IsCompleted = false
            };

            _context.PlayerTrades.Add(trade);
            await _context.SaveChangesAsync();

            return Ok(trade);
        }

        // Выбрать предметы для обмена
        [HttpPost("add-items-to-trade")]
        public async Task<IActionResult> AddItemsToTrade(int tradeId, int playerId, List<int> itemIds)
        {
            var trade = await _context.PlayerTrades.Include(t => t.Player1Items).Include(t => t.Player2Items)
                                                     .FirstOrDefaultAsync(t => t.Id == tradeId);

            if (trade == null)
            {
                return NotFound("Сделка не найдена.");
            }

            var player = await _context.Players.Include(p => p.PlayerItems)
                                                .FirstOrDefaultAsync(p => p.Id == playerId);

            if (player == null)
            {
                return NotFound("Игрок не найден.");
            }

            var itemsToAdd = player.PlayerItems.Where(pi => itemIds.Contains(pi.ItemId)).ToList();

            if (playerId == trade.Player1Id)
            {
                trade.Player1Items.AddRange(itemsToAdd);
            }
            else if (playerId == trade.Player2Id)
            {
                trade.Player2Items.AddRange(itemsToAdd);
            }
            else
            {
                return BadRequest("Игрок не является частью этой сделки.");
            }

            // Обновляем сделку
            await _context.SaveChangesAsync();
            return Ok(trade);
        }

        // Подтвердить сделку
        [HttpPost("confirm-trade")]
        public async Task<IActionResult> ConfirmTrade(int tradeId, int playerId)
        {
            var trade = await _context.PlayerTrades
                                       .Include(t => t.Player1Items)
                                       .Include(t => t.Player2Items)
                                       .FirstOrDefaultAsync(t => t.Id == tradeId);

            if (trade == null)
            {
                return NotFound("Сделка не найдена.");
            }

            if (playerId == trade.Player1Id)
            {
                trade.IsConfirmedByPlayer1 = true;
            }
            else if (playerId == trade.Player2Id)
            {
                trade.IsConfirmedByPlayer2 = true;
            }
            else
            {
                return BadRequest("Игрок не является частью этой сделки.");
            }

            // Проверяем, завершена ли сделка
            if (trade.IsConfirmedByPlayer1 && trade.IsConfirmedByPlayer2)
            {
                // Передаем предметы без каскадного удаления
                foreach (var item in trade.Player1Items)
                {
                    item.PlayerId = trade.Player2Id; // Передаем предмет игроку 2
                    _context.PlayerItems.Update(item); // Явно обновляем объект в контексте
                }

                foreach (var item in trade.Player2Items)
                {
                    item.PlayerId = trade.Player1Id; // Передаем предмет игроку 1
                    _context.PlayerItems.Update(item); // Явно обновляем объект в контексте
                }

                trade.IsCompleted = true;
            }

            await _context.SaveChangesAsync();
            return Ok(trade);
        }

    }
}
