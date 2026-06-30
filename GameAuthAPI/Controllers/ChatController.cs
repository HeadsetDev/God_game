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

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly IMapper _mapper;
        private readonly RedisCacheService _cache;

        public ChatController(GameDbContext context, IMapper mapper, RedisCacheService cache)
        {
            _context = context;
            _mapper = mapper;
            _cache = cache;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ChatMessageDto>>> GetMessages(
            [FromQuery] ChatMessageType? type = null,
            [FromQuery] int? receiverId = null,
            [FromQuery] int limit = 50)
        {
            var cacheKey = $"chat_messages_{type}_{receiverId}_{limit}";
            var messages = await _cache.GetAsync<List<ChatMessageDto>>(cacheKey);

            if (messages == null)
            {
                var query = _context.ChatMessages
                    .Include(cm => cm.Sender)
                    .Include(cm => cm.Receiver)
                    .AsQueryable();

                if (type.HasValue)
                {
                    query = query.Where(cm => cm.Type == type.Value);
                }

                if (receiverId.HasValue)
                {
                    query = query.Where(cm => cm.ReceiverId == receiverId.Value);
                }

                messages = await query
                    .OrderByDescending(cm => cm.Timestamp)
                    .Take(limit)
                    .Select(cm => _mapper.Map<ChatMessageDto>(cm))
                    .ToListAsync();

                // Переворачиваем для хронологического порядка
                messages.Reverse();

                await _cache.SetAsync(cacheKey, messages, TimeSpan.FromMinutes(5));
            }

            return Ok(messages ?? new List<ChatMessageDto>());
        }

        [HttpPost]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ChatMessageDto>> SendMessage([FromBody] ChatMessageDto chatMessageDto)
        {
            if (chatMessageDto == null)
            {
                return BadRequest("Данные сообщения не предоставлены.");
            }

            var chatMessage = _mapper.Map<ChatMessage>(chatMessageDto);
            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync();

            var createdMessageDto = _mapper.Map<ChatMessageDto>(chatMessage);

            // Инвалидируем кэш
            await _cache.RemoveAsync($"chat_messages_{null}_{null}_50");
            await _cache.RemoveAsync($"chat_messages_Global_{null}_50");
            if (chatMessageDto.ReceiverId.HasValue)
            {
                await _cache.RemoveAsync($"chat_messages_Private_{chatMessageDto.ReceiverId}_50");
                await _cache.RemoveAsync($"chat_messages_Private_{chatMessageDto.SenderId}_50");
            }

            return CreatedAtAction(nameof(GetMessages), new { id = createdMessageDto.Id }, createdMessageDto);
        }

        [HttpGet("guild/{guildId}")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<ChatMessageDto>>> GetGuildMessages(
            int guildId,
            [FromQuery] int limit = 50)
        {
            var cacheKey = $"chat_guild_{guildId}_{limit}";
            var messages = await _cache.GetAsync<List<ChatMessageDto>>(cacheKey);

            if (messages == null)
            {
                // Для гильдии используем отдельную логику или фильтр по типу
                var query = _context.ChatMessages
                    .Include(cm => cm.Sender)
                    .Include(cm => cm.Receiver)
                    .Where(cm => cm.Type == ChatMessageType.Guild)
                    .AsQueryable();

                messages = await query
                    .OrderByDescending(cm => cm.Timestamp)
                    .Take(limit)
                    .Select(cm => _mapper.Map<ChatMessageDto>(cm))
                    .ToListAsync();

                messages.Reverse();
                await _cache.SetAsync(cacheKey, messages, TimeSpan.FromMinutes(5));
            }

            return Ok(messages ?? new List<ChatMessageDto>());
        }
    }
}