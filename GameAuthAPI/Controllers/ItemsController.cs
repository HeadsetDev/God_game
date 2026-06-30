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
    public class ItemsController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _cache;

        public ItemsController(GameDbContext context, IMapper mapper, IMemoryCache cache)
        {
            _context = context;
            _mapper = mapper;
            _cache = cache;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ItemDto>>> GetItems()
        {
            if (!_cache.TryGetValue("Items", out IEnumerable<ItemDto>? items))
            {
                items = await _context.Items
                    .Select(i => _mapper.Map<ItemDto>(i))
                    .ToListAsync();

                _cache.Set("Items", items ?? new List<ItemDto>(), TimeSpan.FromMinutes(10));
            }

            return Ok(items ?? new List<ItemDto>());
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ItemDto>> GetItem(int id)
        {
            var item = await _context.Items.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            return Ok(_mapper.Map<ItemDto>(item));
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ItemDto>> CreateItem([FromBody] CreateItemDto createItemDto)
        {
            if (createItemDto == null)
            {
                return BadRequest("Данные для создания предмета не предоставлены.");
            }

            var item = _mapper.Map<Item>(createItemDto);
            _context.Items.Add(item);
            await _context.SaveChangesAsync();

            var itemDto = _mapper.Map<ItemDto>(item);
            return CreatedAtAction(nameof(GetItem), new { id = itemDto.Id }, itemDto);
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateItem(int id, [FromBody] ItemDto itemDto)
        {
            if (id != itemDto.Id)
            {
                return BadRequest("ID предмета не совпадает.");
            }

            var item = await _context.Items.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, item);
            _context.Entry(item).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Items.Any(e => e.Id == id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }
    }
}