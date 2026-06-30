using Microsoft.AspNetCore.Mvc;
using GameAuthAPI.Models;
using GameAuthAPI.Data;

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CraftController : ControllerBase
    {
        private readonly GameDbContext _context;

        public CraftController(GameDbContext context)
        {
            _context = context;
        }

        [HttpPost("craft/{playerId}")]
        public IActionResult CraftItem(int playerId, [FromBody] CraftRecipe recipe)
        {
            var player = _context.Players.Find(playerId);
            if (player == null)
            {
                return NotFound("Игрок не найден.");
            }

            // Проверяем, есть ли у игрока необходимые ресурсы
            foreach (var resource in recipe.RequiredResources)
            {
                if (!player.Resources.ContainsKey(resource.Key) || player.Resources[resource.Key] < resource.Value)
                {
                    return BadRequest($"Недостаточно ресурса {resource.Key}.");
                }
            }

            // Вычитаем ресурсы
            foreach (var resource in recipe.RequiredResources)
            {
                player.Resources[resource.Key] -= resource.Value;
            }

            // Добавляем предмет игроку (здесь можно добавить логику для добавления предмета)
            _context.SaveChanges();

            return Ok($"Предмет {recipe.ResultItem} успешно создан.");
        }
    }
}