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
    public class AuctionController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly RabbitMQService _rabbitMQService;
        private readonly RedisCacheService _cache;
        private readonly IMapper _mapper;

        public AuctionController(
            GameDbContext context,
            RabbitMQService rabbitMQService,
            RedisCacheService cache,
            IMapper mapper)
        {
            _context = context;
            _rabbitMQService = rabbitMQService;
            _cache = cache;
            _mapper = mapper;
        }

        // ===================== ПУБЛИКАЦИЯ ЛОТА =====================

        [HttpPost("publish")]
        [Authorize]
        public async Task<IActionResult> PublishLot([FromBody] AuctionLot lot)
        {
            if (lot == null)
                return BadRequest(ApiResponse<object>.Fail("Лот не может быть пустым."));

            // Публикуем в RabbitMQ
            _rabbitMQService.PublishAuctionLot(lot);

            // Сохраняем в БД для истории
            lot.CreatedAt = DateTime.UtcNow;
            lot.IsActive = true;
            _context.AuctionLots.Add(lot);
            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync("auction_lots_active");
            await _cache.RemoveAsync($"auction_lot_{lot.Id}");

            return Ok(ApiResponse<object>.Ok(new { lot.Id, lot.ItemName }, "Лот опубликован на аукционе."));
        }

        // ===================== ПОЛУЧЕНИЕ ЛОТА ИЗ ОЧЕРЕДИ =====================

        [HttpGet("receive")]
        public IActionResult ReceiveLot()
        {
            var lot = _rabbitMQService.ReceiveAuctionLot();

            if (lot == null)
                return Ok(ApiResponse<object>.Ok(null, "Нет доступных лотов на аукционе."));

            return Ok(ApiResponse<AuctionLot>.Ok(lot, "Лот получен из очереди."));
        }

        // ===================== АКТИВНЫЕ ЛОТЫ =====================

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveLots()
        {
            const string cacheKey = "auction_lots_active";
            var lots = await _cache.GetAsync<List<AuctionLot>>(cacheKey);

            if (lots == null)
            {
                lots = await _context.AuctionLots
                    .Where(l => l.IsActive && l.EndTime > DateTime.UtcNow)
                    .OrderByDescending(l => l.CreatedAt)
                    .ToListAsync();

                await _cache.SetAsync(cacheKey, lots, TimeSpan.FromMinutes(5));
            }

            return Ok(ApiResponse<List<AuctionLot>>.Ok(lots ?? new List<AuctionLot>()));
        }

        // ===================== ПОЛУЧЕНИЕ ЛОТА ПО ID =====================

        [HttpGet("{id}")]
        public async Task<IActionResult> GetLot(int id)
        {
            var cacheKey = $"auction_lot_{id}";
            var lot = await _cache.GetAsync<AuctionLot>(cacheKey);

            if (lot == null)
            {
                lot = await _context.AuctionLots.FindAsync(id);
                if (lot == null)
                    return NotFound(ApiResponse<object>.Fail("Лот не найден."));

                await _cache.SetAsync(cacheKey, lot, TimeSpan.FromMinutes(5));
            }

            return Ok(ApiResponse<AuctionLot>.Ok(lot));
        }

        // ===================== РАЗМЕЩЕНИЕ СТАВКИ =====================

        [HttpPost("bid")]
        [Authorize]
        public async Task<IActionResult> PlaceBid([FromBody] AuctionBid bid)
        {
            if (bid == null || bid.LotId <= 0 || bid.Amount <= 0)
                return BadRequest(ApiResponse<object>.Fail("Некорректные данные ставки."));

            var lot = await _context.AuctionLots.FindAsync(bid.LotId);
            if (lot == null)
                return NotFound(ApiResponse<object>.Fail("Лот не найден."));

            if (!lot.IsActive || lot.EndTime <= DateTime.UtcNow)
                return BadRequest(ApiResponse<object>.Fail("Лот уже неактивен."));

            if (bid.Amount <= lot.StartingPrice)
                return BadRequest(ApiResponse<object>.Fail($"Ставка должна быть выше текущей цены ({lot.StartingPrice})."));

            // Обновляем лот
            lot.StartingPrice = bid.Amount;
            lot.Seller = bid.BidderName; // Здесь можно хранить имя последнего ставящего

            // Если нужно сохранить историю ставок, можно добавить отдельную таблицу, но пока просто обновляем лот
            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.RemoveAsync($"auction_lot_{lot.Id}");
            await _cache.RemoveAsync("auction_lots_active");

            return Ok(ApiResponse<object>.Ok(new { lot.Id, NewPrice = lot.StartingPrice }, $"Ставка {bid.Amount} принята для лота {lot.Id}."));
        }

        // ===================== ИСТОРИЯ ЛОТОВ (ДЛЯ АДМИНА) =====================

        [HttpGet("history")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetHistory([FromQuery] int limit = 50)
        {
            var lots = await _context.AuctionLots
                .OrderByDescending(l => l.CreatedAt)
                .Take(limit)
                .ToListAsync();

            return Ok(ApiResponse<List<AuctionLot>>.Ok(lots));
        }
    }

    // ===================== DTO ДЛЯ СТАВКИ =====================

    public class AuctionBid
    {
        public int LotId { get; set; }
        public decimal Amount { get; set; }
        public string BidderName { get; set; } = string.Empty;
    }
}