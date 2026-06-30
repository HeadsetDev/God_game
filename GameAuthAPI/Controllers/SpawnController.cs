using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Models;
using GameAuthAPI.Services;

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SpawnController : ControllerBase
    {
        private readonly SpawnService _spawnService;

        public SpawnController(SpawnService spawnService)
        {
            _spawnService = spawnService;
        }

        [HttpPost("mob")]
        public IActionResult SpawnMob([FromBody] Mob mob)
        {
            if (mob == null)
            {
                return BadRequest("Данные моба не предоставлены.");
            }

            _spawnService.SpawnMob(mob);
            return Ok($"Моб {mob.Name} появился в локации {mob.SpawnLocation.Name}.");
        }

        [HttpPost("npc")]
        public IActionResult SpawnNPC([FromBody] NPC npc)
        {
            if (npc == null)
            {
                return BadRequest("Данные NPC не предоставлены.");
            }

            _spawnService.SpawnNPC(npc);
            return Ok($"NPC {npc.Name} появился в локации {npc.SpawnLocation.Name}.");
        }
    }
}