using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Data;
using GameAuthAPI.Models;
using GameAuthAPI.Services;
using GameAuthAPI.DTOs; // <-- ВАЖНО!
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StanceController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly StanceService _stanceService;

        public StanceController(GameDbContext context, StanceService stanceService)
        {
            _context = context;
            _stanceService = stanceService;
        }

        [HttpPost("switch")]
        [Authorize]
        public async Task<IActionResult> SwitchStance([FromBody] StanceType newStance)
        {
            var playerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (playerIdClaim == null)
                return Unauthorized(ApiResponse<object>.Fail("Пользователь не авторизован."));

            var playerId = int.Parse(playerIdClaim.Value);
            var player = await _context.Players.FindAsync(playerId);
            if (player == null)
                return NotFound(ApiResponse<object>.Fail("Игрок не найден."));

            // Проверка кулдауна
            if (!player.CanSwitchStance)
            {
                var remaining = 30 - (DateTime.UtcNow - player.LastStanceSwitch).TotalSeconds;
                return BadRequest(ApiResponse<object>.Fail($"Переключение стойки доступно через {Math.Ceiling(remaining)} сек."));
            }

            // Проверка, что стойка принадлежит классу игрока
            var allowedStances = GetStancesForClass(player.Class);
            if (!allowedStances.Contains(newStance))
                return BadRequest(ApiResponse<object>.Fail("Эта стойка недоступна для вашего класса."));

            // Проверяем, что игрок не пытается переключиться на ту же стойку
            if (player.ActiveStance == newStance)
                return BadRequest(ApiResponse<object>.Fail("Вы уже в этой стойке."));

            player.ActiveStance = newStance;
            player.LastStanceSwitch = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.Ok(
                new
                {
                    player.ActiveStance,
                    NextSwitchAvailable = DateTime.UtcNow.AddSeconds(30)
                },
                $"Стойка изменена на {newStance}."
            ));
        }

        [HttpGet("current")]
        [Authorize]
        public async Task<IActionResult> GetCurrentStance()
        {
            var playerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (playerIdClaim == null)
                return Unauthorized(ApiResponse<object>.Fail("Пользователь не авторизован."));

            var playerId = int.Parse(playerIdClaim.Value);
            var player = await _context.Players.FindAsync(playerId);
            if (player == null)
                return NotFound(ApiResponse<object>.Fail("Игрок не найден."));

            var bonuses = await _stanceService.GetStanceStatsAsync(player.ActiveStance);

            return Ok(ApiResponse<object>.Ok(
                new
                {
                    player.ActiveStance,
                    player.Class,
                    CanSwitch = player.CanSwitchStance,
                    NextSwitchAvailable = player.LastStanceSwitch.AddSeconds(30),
                    Bonuses = bonuses
                }
            ));
        }

        [HttpGet("available")]
        [Authorize]
        public async Task<IActionResult> GetAvailableStances()
        {
            var playerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (playerIdClaim == null)
                return Unauthorized(ApiResponse<object>.Fail("Пользователь не авторизован."));

            var playerId = int.Parse(playerIdClaim.Value);
            var player = await _context.Players.FindAsync(playerId);
            if (player == null)
                return NotFound(ApiResponse<object>.Fail("Игрок не найден."));

            var stances = GetStancesForClass(player.Class);
            var result = new List<object>();

            foreach (var stance in stances)
            {
                var bonuses = await _stanceService.GetStanceStatsAsync(stance);
                result.Add(new
                {
                    Stance = stance,
                    Bonuses = bonuses,
                    IsActive = player.ActiveStance == stance
                });
            }

            return Ok(ApiResponse<object>.Ok(result));
        }

        private List<StanceType> GetStancesForClass(ClassType classType)
        {
            return classType switch
            {
                ClassType.Warrior => new List<StanceType> { StanceType.SwordAndShield, StanceType.DualWield },
                ClassType.Archer => new List<StanceType> { StanceType.Bow, StanceType.Assassin },
                ClassType.Mage => new List<StanceType> { StanceType.Death, StanceType.Life },
                ClassType.Bard => new List<StanceType> { StanceType.Support, StanceType.Combat },
                _ => new List<StanceType>()
            };
        }
    }
}