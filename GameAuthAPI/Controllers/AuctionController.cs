using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Services;
using GameAuthAPI.Models;

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuctionController : ControllerBase
    {
        private readonly RabbitMQService _rabbitMQService;

        public AuctionController(RabbitMQService rabbitMQService)
        {
            _rabbitMQService = rabbitMQService;
        }

        [HttpPost("publish")]
        public IActionResult PublishLot([FromBody] AuctionLot lot)
        {
            if (lot == null)
            {
                return BadRequest("Лот не может быть пустым.");
            }

            _rabbitMQService.PublishAuctionLot(lot);
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
    }
}