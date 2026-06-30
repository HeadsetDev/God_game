using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Services;

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
        public IActionResult SendMessage([FromBody] string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return BadRequest("Сообщение не может быть пустым.");
            }

            _rabbitMQService.SendMessage("notifications", message);
            return Ok("Сообщение отправлено: " + message);
        }

        [HttpGet("receive")]
        public IActionResult ReceiveMessage()
        {
            var message = _rabbitMQService.ReceiveMessage("notifications"); // Укажите имя очереди
            return Ok("Получено сообщение: " + message);
        }
    }
}