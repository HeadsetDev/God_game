using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GameAuthAPI.Models;
using Microsoft.Extensions.Configuration;
using GameAuthAPI.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace GameAuthAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly GameDbContext _context;

        public AuthController(IConfiguration config, GameDbContext context)
        {
            _config = config;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterUser user)
        {
            if (await _context.Players.AnyAsync(u => u.Name == user.Username))
                return BadRequest("Пользователь уже существует.");

            var newUser = new Player(user.Username, user.Password);
            _context.Players.Add(newUser);
            await _context.SaveChangesAsync();

            return Ok("Пользователь зарегистрирован.");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] RegisterUser user)
        {
            var player = await _context.Players.FirstOrDefaultAsync(u => u.Name == user.Username);
            if (player == null || !player.CheckPassword(user.Password))
                return Unauthorized("Неверные данные.");

            var token = GenerateJwtToken(player.Name);
            return Ok(new { token });
        }

        private string GenerateJwtToken(string username)
        {
            var jwtSettings = _config.GetSection("JwtSettings");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, "Player")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
