using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Data;
using GameAuthAPI.Models;
using GameAuthAPI.Services;
using GameAuthAPI.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using GameAuthAPI.Hubs;
using Microsoft.AspNetCore.Authorization;

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MovementController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly IHubContext<GameHub> _hubContext;
        private readonly RedisCacheService _cache;

        public MovementController(GameDbContext context, IHubContext<GameHub> hubContext, RedisCacheService cache)
        {
            _context = context;
            _hubContext = hubContext;
            _cache = cache;
        }

        [HttpPost("move/{playerId}/{locationId}")]
        [Authorize]
        public async Task<IActionResult> MovePlayer(int playerId, int locationId)
        {
            var player = await _context.Players
                .Include(p => p.CurrentLocation)
                .FirstOrDefaultAsync(p => p.Id == playerId);

            if (player == null)
                return NotFound(ApiResponse<object>.Fail("Игрок не найден."));

            var newLocation = await _context.Locations
                .Include(l => l.ConnectedLocations)
                .FirstOrDefaultAsync(l => l.Id == locationId);

            if (newLocation == null)
                return NotFound(ApiResponse<object>.Fail("Локация не найдена."));

            if (!player.CurrentLocation.ConnectedLocations.Any(l => l.Id == locationId))
                return BadRequest(ApiResponse<object>.Fail("Невозможно переместиться в эту локацию."));

            player.CurrentLocation = newLocation;
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync($"player_{playerId}");
            await _cache.RemoveAsync($"player_location_{playerId}");

            await _hubContext.Clients.All.SendAsync("ReceivePlayerMovement", playerId, player.Name, newLocation.Name);

            return Ok(ApiResponse<object>.Ok(
                new { playerId, playerName = player.Name, locationName = newLocation.Name, locationId = newLocation.Id },
                $"Игрок {player.Name} перемещён в локацию {newLocation.Name}."
            ));
        }

        [HttpGet("location/{playerId}")]
        [Authorize]
        public async Task<IActionResult> GetPlayerLocation(int playerId)
        {
            var cacheKey = $"player_location_{playerId}";
            var location = await _cache.GetAsync<Location>(cacheKey);

            if (location == null)
            {
                var player = await _context.Players
                    .Include(p => p.CurrentLocation)
                    .FirstOrDefaultAsync(p => p.Id == playerId);

                if (player == null)
                    return NotFound(ApiResponse<object>.Fail("Игрок не найден."));

                location = player.CurrentLocation;
                await _cache.SetAsync(cacheKey, location, TimeSpan.FromMinutes(5));
            }

            return Ok(ApiResponse<Location>.Ok(location));
        }

        [HttpGet("locations")]
        public async Task<IActionResult> GetAllLocations()
        {
            const string cacheKey = "all_locations";
            var locations = await _cache.GetAsync<List<Location>>(cacheKey);

            if (locations == null)
            {
                locations = await _context.Locations
                    .Include(l => l.ConnectedLocations)
                    .ToListAsync();

                await _cache.SetAsync(cacheKey, locations, TimeSpan.FromMinutes(10));
            }

            return Ok(ApiResponse<List<Location>>.Ok(locations ?? new List<Location>()));
        }

        [HttpPost("location")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CreateLocation([FromBody] Location location)
        {
            if (location == null)
                return BadRequest(ApiResponse<object>.Fail("Данные локации не предоставлены."));

            _context.Locations.Add(location);
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync("all_locations");

            return Ok(ApiResponse<Location>.Ok(location, "Локация создана."));
        }
    }
}