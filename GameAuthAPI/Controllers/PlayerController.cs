using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Data;
using GameAuthAPI.Models;

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlayerController : ControllerBase
    {
        private readonly GameDbContext _context;

        public PlayerController(GameDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetAllPlayers()
        {
            return Ok(_context.Players.ToList());
        }

        [HttpGet("{id}")]
        public IActionResult GetPlayer(int id)
        {
            var player = _context.Players.Find(id);
            if (player == null) return NotFound("Игрок не найден.");
            return Ok(player);
        }
    }
}
