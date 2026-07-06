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
    public class ItemsController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly IMapper _mapper;
        private readonly RedisCacheService _cache;

        public ItemsController(GameDbContext context, IMapper mapper, RedisCacheService cache)
        {
            _context = context;
            _mapper = mapper;
            _cache = cache;
        }

        [HttpGet]
        public async Task<IActionResult> GetItems()
        {
            const string cacheKey = "items_all";
            var items = await _cache.GetAsync<List<ItemDto>>(cacheKey);

            if (items == null)
            {
                items = await _context.Items
                    .Select(i => _mapper.Map<ItemDto>(i))
                    .ToListAsync();
                await _cache.SetAsync(cacheKey, items, TimeSpan.FromMinutes(10));
            }

            return Ok(ApiResponse<List<ItemDto>>.Ok(items ?? new List<ItemDto>()));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetItem(int id)
        {
            var cacheKey = $"item_{id}";
            var item = await _cache.GetAsync<ItemDto>(cacheKey);

            if (item == null)
            {
                var dbItem = await _context.Items.FindAsync(id);
                if (dbItem == null)
                    return NotFound(ApiResponse<object>.Fail("Предмет не найден."));

                item = _mapper.Map<ItemDto>(dbItem);
                await _cache.SetAsync(cacheKey, item, TimeSpan.FromMinutes(10));
            }

            return Ok(ApiResponse<ItemDto>.Ok(item));
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CreateItem([FromBody] CreateItemDto createItemDto)
        {
            if (createItemDto == null)
                return BadRequest(ApiResponse<object>.Fail("Данные для создания предмета не предоставлены."));

            var item = _mapper.Map<Item>(createItemDto);
            _context.Items.Add(item);
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync("items_all");
            var itemDto = _mapper.Map<ItemDto>(item);

            return CreatedAtAction(nameof(GetItem), new { id = itemDto.Id },
                ApiResponse<ItemDto>.Ok(itemDto, "Предмет создан."));
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UpdateItem(int id, [FromBody] ItemDto itemDto)
        {
            if (id != itemDto.Id)
                return BadRequest(ApiResponse<object>.Fail("ID предмета не совпадает."));

            var item = await _context.Items.FindAsync(id);
            if (item == null)
                return NotFound(ApiResponse<object>.Fail("Предмет не найден."));

            _mapper.Map(itemDto, item);
            _context.Entry(item).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                await _cache.RemoveAsync($"item_{id}");
                await _cache.RemoveAsync("items_all");
                return Ok(ApiResponse<object>.Ok(null, "Предмет обновлён."));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Items.Any(e => e.Id == id))
                    return NotFound(ApiResponse<object>.Fail("Предмет не найден."));
                throw;
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteItem(int id)
        {
            var item = await _context.Items.FindAsync(id);
            if (item == null)
                return NotFound(ApiResponse<object>.Fail("Предмет не найден."));

            _context.Items.Remove(item);
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync($"item_{id}");
            await _cache.RemoveAsync("items_all");

            return Ok(ApiResponse<object>.Ok(null, "Предмет удалён."));
        }
    }
}