using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Services;
using GameAuthAPI.Models;
using GameAuthAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuctionController : ControllerBase
    {
        private readonly RabbitMQService _rabbitMQService;
        private readonly GameDbContext _context;
        private readonly RedisCacheService _cache;

        public AuctionController(RabbitMQService rabbitMQService, GameDbContext context, RedisCacheService cache)
        {
            _rabbitMQService = rabbitMQService;
            _context = context;
            _cache = cache;
        }

        [HttpPost("publish")]
        public async Task<IActionResult> PublishLot([FromBody] AuctionLot lot)
        {
            if (lot == null)
            {
                return BadRequest("Лот не может быть пустым.");
            }

            _rabbitMQService.PublishAuctionLot(lot);

            // Сохраняем лот в БД для истории
            _context.AuctionLots.Add(lot);
            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync("auction_lots_active");
            await _cache.RemoveAsync($"auction_lot_{lot.Id}");

            return Ok("Лот опубликован на аукционе.");
        }

        [HttpGet("receive")]
        public IActionResult ReceiveLot()
        {
            var lot = _rabbitMQService.ReceiveAuctionLot();

            if (lot == null)
            {
                return Ok("Нет доступных лотов на аукционе.");
            }

            return Ok(lot);
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveLots()
        {
            const string cacheKey = "auction_lots_active";
            var lots = await _cache.GetAsync<List<AuctionLot>>(cacheKey);

            if (lots == null)
            {
                lots = await _context.AuctionLots
                    .Where(l => l.EndTime > DateTime.UtcNow)
                    .ToListAsync();

                await _cache.SetAsync(cacheKey, lots, TimeSpan.FromMinutes(5));
            }

            return Ok(lots ?? new List<AuctionLot>());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetLot(int id)
        {
            var cacheKey = $"auction_lot_{id}";
            var lot = await _cache.GetAsync<AuctionLot>(cacheKey);

            if (lot == null)
            {
                lot = await _context.AuctionLots.FindAsync(id);
                if (lot == null)
                {
                    return NotFound("Лот не найден.");
                }

                await _cache.SetAsync(cacheKey, lot, TimeSpan.FromMinutes(5));
            }

            return Ok(lot);
        }

        [HttpPost("bid")]
        public async Task<IActionResult> PlaceBid([FromBody] AuctionBid bid)
        {
            var lot = await _context.AuctionLots.FindAsync(bid.LotId);
            if (lot == null)
            {
                return NotFound("Лот не найден.");
            }

            if (bid.Amount <= lot.StartingPrice)
            {
                return BadRequest("Ставка должна быть выше текущей цены.");
            }

            lot.StartingPrice = bid.Amount;
            lot.Seller = bid.BidderName;
            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync($"auction_lot_{lot.Id}");
            await _cache.RemoveAsync("auction_lots_active");

            return Ok($"Ставка {bid.Amount} принята для лота {lot.Id}");
        }
    }

    public class AuctionBid
    {
        public int LotId { get; set; }
        public decimal Amount { get; set; }
        public string BidderName { get; set; } = string.Empty;
    }
}