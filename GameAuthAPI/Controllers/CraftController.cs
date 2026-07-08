using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Data;
using GameAuthAPI.Models;
using GameAuthAPI.Services;
using GameAuthAPI.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CraftController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly RedisCacheService _cache;
        private readonly StaticDataService _staticDataService;

        public CraftController(GameDbContext context, RedisCacheService cache, StaticDataService staticDataService)
        {
            _context = context;
            _cache = cache;
            _staticDataService = staticDataService;
        }

        [HttpGet("recipes/{playerId}")]
        [Authorize]
        public async Task<IActionResult> GetAvailableRecipes(int playerId)
        {
            var authError = EnsureOwnPlayerId(playerId);
            if (authError != null)
                return authError;

            var player = await _context.Players
                .Include(p => p.PlayerItems)
                .FirstOrDefaultAsync(p => p.Id == playerId);

            if (player == null)
                return NotFound(ApiResponse<object>.Fail("Игрок не найден."));

            var allRecipes = await _staticDataService.GetCraftRecipesAsync();
            var availableRecipes = allRecipes
                .Where(r => _staticDataService.IsRecipeAvailable(player, r))
                .ToList();

            return Ok(ApiResponse<List<CraftRecipe>>.Ok(availableRecipes));
        }

        [HttpPost("craft/{playerId}")]
        [Authorize]
        public async Task<IActionResult> CraftItem(int playerId, [FromBody] CraftRequest request)
        {
            var authError = EnsureOwnPlayerId(playerId);
            if (authError != null)
                return authError;

            if (request == null || request.RecipeId <= 0)
                return BadRequest(ApiResponse<object>.Fail("Некорректный запрос."));

            var player = await _context.Players
                .Include(p => p.PlayerItems)
                    .ThenInclude(pi => pi.Item)
                .FirstOrDefaultAsync(p => p.Id == playerId);

            if (player == null)
                return NotFound(ApiResponse<object>.Fail("Игрок не найден."));

            var recipe = await _staticDataService.GetCraftRecipeByIdAsync(request.RecipeId);
            if (recipe == null)
                return NotFound(ApiResponse<object>.Fail("Рецепт не найден."));

            if (!_staticDataService.IsRecipeAvailable(player, recipe))
                return BadRequest(ApiResponse<object>.Fail("Рецепт недоступен для вашего персонажа."));

            foreach (var resource in recipe.RequiredResources)
            {
                var playerItem = player.PlayerItems
                    .FirstOrDefault(pi => pi.Item != null && pi.Item.Name == resource.Key);

                if (playerItem == null || playerItem.Quantity < resource.Value)
                    return BadRequest(ApiResponse<object>.Fail($"Недостаточно ресурса {resource.Key}."));
            }

            foreach (var resource in recipe.RequiredResources)
            {
                var playerItem = player.PlayerItems
                    .FirstOrDefault(pi => pi.Item != null && pi.Item.Name == resource.Key);

                if (playerItem != null)
                {
                    playerItem.Quantity -= resource.Value;
                    if (playerItem.Quantity <= 0)
                        _context.PlayerItems.Remove(playerItem);
                }
            }

            var resultItem = await _context.Items
                .FirstOrDefaultAsync(i => i.Name == recipe.ResultItem);

            if (resultItem == null)
                return NotFound(ApiResponse<object>.Fail($"Предмет {recipe.ResultItem} не найден в базе данных."));

            var existing = player.PlayerItems
                .FirstOrDefault(pi => pi.ItemId == resultItem.Id);

            if (existing != null)
                existing.Quantity++;
            else
            {
                _context.PlayerItems.Add(new PlayerItem
                {
                    PlayerId = playerId,
                    ItemId = resultItem.Id,
                    Quantity = 1
                });
            }

            player.CraftSkillLevel += 1;

            await _context.SaveChangesAsync();

            await _cache.RemoveAsync($"player_inventory_{playerId}");
            await _cache.RemoveAsync($"player_{playerId}");

            return Ok(ApiResponse<object>.Ok(
                new { playerId, craftedItem = recipe.ResultItem },
                $"Предмет {recipe.ResultItem} успешно создан. Навык крафта повышен до {player.CraftSkillLevel}."
            ));
        }

        [HttpGet("recipes/all")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetAllRecipes()
        {
            var recipes = await _staticDataService.GetCraftRecipesAsync();
            return Ok(ApiResponse<List<CraftRecipe>>.Ok(recipes ?? new List<CraftRecipe>()));
        }

        private IActionResult? EnsureOwnPlayerId(int requestedPlayerId)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null || !int.TryParse(claim.Value, out var authenticatedPlayerId))
                return Unauthorized(ApiResponse<object>.Fail("Идентификатор пользователя не найден в токене."));

            if (authenticatedPlayerId != requestedPlayerId)
                return Forbid();

            return null;
        }
    }
}