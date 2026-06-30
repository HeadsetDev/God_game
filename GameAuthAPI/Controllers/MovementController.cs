using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Data;
using GameAuthAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using GameAuthAPI.Hubs;

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MovementController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly IHubContext<GameHub> _hubContext;

        public MovementController(GameDbContext context, IHubContext<GameHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpPost("move/{playerId}/{locationId}")]
        public async Task<IActionResult> MovePlayer(int playerId, int locationId)
        {
            var player = await _context.Players
                .Include(p => p.CurrentLocation)
                .FirstOrDefaultAsync(p => p.Id == playerId);

            if (player == null)
            {
                return NotFound("Игрок не найден.");
            }

            var newLocation = await _context.Locations
                .Include(l => l.ConnectedLocations)
                .FirstOrDefaultAsync(l => l.Id == locationId);

            if (newLocation == null)
            {
                return NotFound("Локация не найдена.");
            }

            if (!player.CurrentLocation.ConnectedLocations.Contains(newLocation))
            {
                return BadRequest("Невозможно переместиться в эту локацию.");
            }

            player.CurrentLocation = newLocation;
            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("ReceivePlayerMovement", playerId, player.Name, newLocation.Name);

            return Ok($"Игрок {player.Name} перемещен в локацию {newLocation.Name}.");
        }
    }
}