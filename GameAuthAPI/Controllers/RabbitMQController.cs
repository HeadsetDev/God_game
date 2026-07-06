using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Services;
using GameAuthAPI.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RabbitMQController : ControllerBase
    {
        private readonly RabbitMQService _rabbitMQService;

        public RabbitMQController(RabbitMQService rabbitMQService)
        {
            _rabbitMQService = rabbitMQService;
        }

        [HttpPost("send")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult SendMessage([FromBody] string message)
        {
            if (string.IsNullOrEmpty(message))
                return BadRequest(ApiResponse<object>.Fail("Сообщение не может быть пустым."));

            _rabbitMQService.SendMessage("notifications", message);

            return Ok(ApiResponse<object>.Ok(null, "Сообщение отправлено в очередь."));
        }

        [HttpGet("receive")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult ReceiveMessage()
        {
            var message = _rabbitMQService.ReceiveMessage("notifications");

            if (message == "Нет сообщений в очереди.")
                return Ok(ApiResponse<object>.Ok(null, "Нет сообщений."));

            return Ok(ApiResponse<object>.Ok(new { message }, "Сообщение получено."));
        }
    }
}