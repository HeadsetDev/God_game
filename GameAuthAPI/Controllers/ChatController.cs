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

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly IMapper _mapper;

        public ChatController(GameDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ChatMessageDto>>> GetMessages(
            [FromQuery] ChatMessageType? type = null,
            [FromQuery] int? receiverId = null)
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

            var messages = await query.ToListAsync();
            return Ok(_mapper.Map<IEnumerable<ChatMessageDto>>(messages));
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
            return CreatedAtAction(nameof(GetMessages), new { id = createdMessageDto.Id }, createdMessageDto);
        }
    }
}