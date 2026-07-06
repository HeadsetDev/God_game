using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Models;
using GameAuthAPI.Services;
using GameAuthAPI.Data;
using GameAuthAPI.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SpawnController : ControllerBase
    {
        private readonly SpawnService _spawnService;
        private readonly GameDbContext _context;
        private readonly RedisCacheService _cache;

        public SpawnController(SpawnService spawnService, GameDbContext context, RedisCacheService cache)
        {
            _spawnService = spawnService;
            _context = context;
            _cache = cache;
        }

        [HttpPost("mob")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> SpawnMob([FromBody] Mob mob)
        {
            if (mob == null)
                return BadRequest(ApiResponse<object>.Fail("Данные моба не предоставлены."));

            var location = await _context.Locations.FindAsync(mob.SpawnLocationId);
            if (location == null)
                return NotFound(ApiResponse<object>.Fail("Локация не найдена."));

            _spawnService.SpawnMob(mob);

            _context.Mobs.Add(mob);
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync($"mobs_location_{mob.SpawnLocationId}");
            await _cache.RemoveAsync("mobs_all");

            return Ok(ApiResponse<object>.Ok(
                new { mob.Id, mob.Name, mob.SpawnLocationId },
                $"Моб {mob.Name} появился в локации {location.Name}."
            ));
        }

        [HttpPost("npc")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> SpawnNPC([FromBody] NPC npc)
        {
            if (npc == null)
                return BadRequest(ApiResponse<object>.Fail("Данные NPC не предоставлены."));

            var location = await _context.Locations.FindAsync(npc.SpawnLocationId);
            if (location == null)
                return NotFound(ApiResponse<object>.Fail("Локация не найдена."));

            _spawnService.SpawnNPC(npc);

            _context.NPCs.Add(npc);
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync($"npcs_location_{npc.SpawnLocationId}");
            await _cache.RemoveAsync("npcs_all");

            return Ok(ApiResponse<object>.Ok(
                new { npc.Id, npc.Name, npc.SpawnLocationId },
                $"NPC {npc.Name} появился в локации {location.Name}."
            ));
        }

        [HttpGet("mobs")]
        public async Task<IActionResult> GetAllMobs()
        {
            const string cacheKey = "mobs_all";
            var mobs = await _cache.GetAsync<List<Mob>>(cacheKey);

            if (mobs == null)
            {
                mobs = await _context.Mobs
                    .Include(m => m.SpawnLocation)
                    .ToListAsync();

                await _cache.SetAsync(cacheKey, mobs, TimeSpan.FromMinutes(10));
            }

            return Ok(ApiResponse<List<Mob>>.Ok(mobs ?? new List<Mob>()));
        }

        [HttpGet("mobs/location/{locationId}")]
        public async Task<IActionResult> GetMobsByLocation(int locationId)
        {
            var cacheKey = $"mobs_location_{locationId}";
            var mobs = await _cache.GetAsync<List<Mob>>(cacheKey);

            if (mobs == null)
            {
                mobs = await _context.Mobs
                    .Where(m => m.SpawnLocationId == locationId)
                    .Include(m => m.SpawnLocation)
                    .ToListAsync();

                await _cache.SetAsync(cacheKey, mobs, TimeSpan.FromMinutes(5));
            }

            return Ok(ApiResponse<List<Mob>>.Ok(mobs ?? new List<Mob>()));
        }

        [HttpGet("npcs")]
        public async Task<IActionResult> GetAllNPCs()
        {
            const string cacheKey = "npcs_all";
            var npcs = await _cache.GetAsync<List<NPC>>(cacheKey);

            if (npcs == null)
            {
                npcs = await _context.NPCs
                    .Include(n => n.SpawnLocation)
                    .ToListAsync();

                await _cache.SetAsync(cacheKey, npcs, TimeSpan.FromMinutes(10));
            }

            return Ok(ApiResponse<List<NPC>>.Ok(npcs ?? new List<NPC>()));
        }

        [HttpGet("npcs/location/{locationId}")]
        public async Task<IActionResult> GetNPCsByLocation(int locationId)
        {
            var cacheKey = $"npcs_location_{locationId}";
            var npcs = await _cache.GetAsync<List<NPC>>(cacheKey);

            if (npcs == null)
            {
                npcs = await _context.NPCs
                    .Where(n => n.SpawnLocationId == locationId)
                    .Include(n => n.SpawnLocation)
                    .ToListAsync();

                await _cache.SetAsync(cacheKey, npcs, TimeSpan.FromMinutes(5));
            }

            return Ok(ApiResponse<List<NPC>>.Ok(npcs ?? new List<NPC>()));
        }
    }
}