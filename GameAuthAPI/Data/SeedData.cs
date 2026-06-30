using GameAuthAPI.Models;
using GameAuthAPI.Services; // <-- ЭТО БЫЛО ПРОПУЩЕНО
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GameAuthAPI.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(GameDbContext context)
        {
            if (await context.Locations.AnyAsync())
                return;

            // Локации
            var locations = new List<Location>
            {
                new Location { Name = "Стартовая деревня", Description = "Место, где начинается ваше приключение." },
                new Location { Name = "Тёмный лес", Description = "Опасный лес, полный монстров." },
                new Location { Name = "Город гильдий", Description = "Центр социальной жизни игроков." },
                new Location { Name = "Пещера дракона", Description = "Опаснейшее место во всём мире." },
                new Location { Name = "Арена", Description = "Место для PvP сражений." }
            };

            await context.Locations.AddRangeAsync(locations);
            await context.SaveChangesAsync();

            // Мобы
            var mobs = new List<Mob>
            {
                new Mob { Name = "Гоблин", Health = 100, Damage = 15, Defense = 5, ExperienceReward = 20, Level = 1, SpawnLocationId = locations[1].Id },
                new Mob { Name = "Волк", Health = 150, Damage = 20, Defense = 8, ExperienceReward = 30, Level = 2, SpawnLocationId = locations[1].Id },
                new Mob { Name = "Орк", Health = 300, Damage = 35, Defense = 15, ExperienceReward = 50, Level = 5, SpawnLocationId = locations[1].Id },
                new Mob { Name = "Дракон", Health = 1000, Damage = 60, Defense = 30, ExperienceReward = 200, Level = 10, SpawnLocationId = locations[3].Id },
                new Mob { Name = "Тролль", Health = 500, Damage = 40, Defense = 20, ExperienceReward = 80, Level = 7, SpawnLocationId = locations[3].Id }
            };

            await context.Mobs.AddRangeAsync(mobs);
            await context.SaveChangesAsync();

            // Навыки
            var skills = new List<Skill>
            {
                new Skill { Name = "Огненный шар", Description = "Наносит огненный урон врагу.", Type = "Active", Damage = 50, ManaCost = 20, Cooldown = 5, RequiredLevel = 1, Effect = "{\"type\":\"fire\",\"damage\":50}" },
                new Skill { Name = "Лечение", Description = "Восстанавливает здоровье.", Type = "Active", Damage = -30, ManaCost = 15, Cooldown = 8, RequiredLevel = 1, Effect = "{\"type\":\"heal\",\"amount\":30}" },
                new Skill { Name = "Удар мечом", Description = "Сильный удар мечом.", Type = "Active", Damage = 35, ManaCost = 5, Cooldown = 3, RequiredLevel = 1, Effect = "{\"type\":\"physical\",\"damage\":35}" }
            };

            await context.Skills.AddRangeAsync(skills);
            await context.SaveChangesAsync();

            // Тестовый админ
            var passwordService = new PasswordService();
            var admin = new Player
            {
                Name = "admin",
                PasswordHash = passwordService.HashPassword("admin123"),
                Role = "Admin",
                Level = 10,
                Coins = 1000,
                Crystals = 100,
                CurrentLocationId = locations[0].Id,
                PlayerKills = 100,
                PvP_Wins = 50,
                PvP_Losses = 10,
                PvP_Kills = 80,
                PvP_Deaths = 15
            };

            await context.Players.AddAsync(admin);
            await context.SaveChangesAsync();

            // Тестовый игрок
            var player = new Player
            {
                Name = "player",
                PasswordHash = passwordService.HashPassword("player123"),
                Role = "Player",
                Level = 5,
                Coins = 500,
                Crystals = 50,
                CurrentLocationId = locations[0].Id,
                PlayerKills = 30,
                PvP_Wins = 20,
                PvP_Losses = 5,
                PvP_Kills = 30,
                PvP_Deaths = 8
            };

            await context.Players.AddAsync(player);
            await context.SaveChangesAsync();
        }
    }
}