using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Data;
using GameAuthAPI.DTOs;
using GameAuthAPI.Models;
using GameAuthAPI.Services;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

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
        public async Task<IActionResult> GetMessages(
            [FromQuery] ChatMessageType? type = null,
            [FromQuery] int? receiverId = null,
            [FromQuery] int? guildId = null,
            [FromQuery] int limit = 50)
        {
            var cacheKey = $"chat_messages_{type}_{receiverId}_{guildId}_{limit}";
            var messages = await _cache.GetAsync<List<ChatMessageDto>>(cacheKey);

            if (messages == null)
            {
                var query = _context.ChatMessages
                    .Include(cm => cm.Sender)
                    .Include(cm => cm.Receiver)
                    .Include(cm => cm.Guild)
                    .AsQueryable();

                if (type.HasValue)
                    query = query.Where(cm => cm.Type == type.Value);

                if (receiverId.HasValue)
                    query = query.Where(cm => cm.ReceiverId == receiverId.Value);

                if (guildId.HasValue)
                    query = query.Where(cm => cm.GuildId == guildId.Value);

                messages = await query
                    .OrderByDescending(cm => cm.Timestamp)
                    .Take(limit)
                    .Select(cm => _mapper.Map<ChatMessageDto>(cm))
                    .ToListAsync();

                messages.Reverse();
                await _cache.SetAsync(cacheKey, messages, TimeSpan.FromMinutes(5));
            }

            return Ok(ApiResponse<List<ChatMessageDto>>.Ok(messages ?? new List<ChatMessageDto>()));
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessageDto chatMessageDto)
        {
            if (chatMessageDto == null)
                return BadRequest(ApiResponse<object>.Fail("Данные сообщения не предоставлены."));

            // Проверка отправителя
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var authenticatedUserId))
                return Unauthorized(ApiResponse<object>.Fail("Пользователь не авторизован."));

            if (authenticatedUserId != chatMessageDto.SenderId)
                return Forbid();

            var sender = await _context.Players.FindAsync(chatMessageDto.SenderId);
            if (sender == null)
                return NotFound(ApiResponse<object>.Fail("Отправитель не найден."));

            // Валидация в зависимости от типа
            if (chatMessageDto.Type == ChatMessageType.Private)
            {
                if (!chatMessageDto.ReceiverId.HasValue)
                    return BadRequest(ApiResponse<object>.Fail("Для личного сообщения нужен получатель."));

                var receiver = await _context.Players.FindAsync(chatMessageDto.ReceiverId.Value);
                if (receiver == null)
                    return NotFound(ApiResponse<object>.Fail("Получатель не найден."));
            }
            else if (chatMessageDto.Type == ChatMessageType.Guild)
            {
                if (!chatMessageDto.GuildId.HasValue)
                    return BadRequest(ApiResponse<object>.Fail("Для сообщения в гильдию нужен GuildId."));

                // Проверяем, что игрок состоит в этой гильдии
                var isMember = await _context.PlayerGuilds
                    .AnyAsync(pg => pg.PlayerId == chatMessageDto.SenderId && pg.GuildId == chatMessageDto.GuildId.Value);
                if (!isMember)
                    return Forbid();
            }

            var chatMessage = _mapper.Map<ChatMessage>(chatMessageDto);
            chatMessage.Timestamp = DateTime.UtcNow;

            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync();

            var createdMessageDto = _mapper.Map<ChatMessageDto>(chatMessage);

            // Инвалидация кэша
            await _cache.RemoveAsync($"chat_messages_{null}_{null}_{null}_50");
            await _cache.RemoveAsync($"chat_messages_Global_{null}_{null}_50");
            if (chatMessageDto.ReceiverId.HasValue)
            {
                await _cache.RemoveAsync($"chat_messages_Private_{chatMessageDto.ReceiverId}_{null}_50");
                await _cache.RemoveAsync($"chat_messages_Private_{chatMessageDto.SenderId}_{null}_50");
            }
            if (chatMessageDto.GuildId.HasValue)
            {
                await _cache.RemoveAsync($"chat_messages_Guild_{null}_{chatMessageDto.GuildId}_50");
            }

            return CreatedAtAction(nameof(GetMessages), new { id = createdMessageDto.Id },
                ApiResponse<ChatMessageDto>.Ok(createdMessageDto, "Сообщение отправлено."));
        }

        [HttpGet("guild/{guildId}")]
        [Authorize]
        public async Task<IActionResult> GetGuildMessages(int guildId, [FromQuery] int limit = 50)
        {
            // Проверка, что пользователь состоит в гильдии
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var authenticatedUserId))
                return Unauthorized(ApiResponse<object>.Fail("Пользователь не авторизован."));

            var isMember = await _context.PlayerGuilds
                .AnyAsync(pg => pg.PlayerId == authenticatedUserId && pg.GuildId == guildId);
            if (!isMember)
                return Forbid();

            // Используем общий метод с фильтром по guildId
            return await GetMessages(ChatMessageType.Guild, null, guildId, limit);
        }
    }
}