using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using GameAuthAPI.Models;
using System.Text.Json;

namespace GameAuthAPI.Data
{
    public class GameDbContext : DbContext
    {
        public GameDbContext(DbContextOptions<GameDbContext> options) : base(options) { }

        // Определение DbSet для всех сущностей
        public DbSet<Player> Players { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<PlayerItem> PlayerItems { get; set; }
        public DbSet<Quest> Quests { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<PlayerTrade> PlayerTrades { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Mob> Mobs { get; set; }
        public DbSet<NPC> NPCs { get; set; }

        // Добавляем новые DbSet для гильдий и групповых квестов
        public DbSet<Guild> Guilds { get; set; }
        public DbSet<PlayerGuild> PlayerGuilds { get; set; }
        public DbSet<QuestParticipant> QuestParticipants { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Настройка связи между Player и Guild
            modelBuilder.Entity<PlayerGuild>()
                .HasKey(pg => new { pg.PlayerId, pg.GuildId });

            modelBuilder.Entity<PlayerGuild>()
                .HasOne(pg => pg.Player)
                .WithMany(p => p.PlayerGuilds)
                .HasForeignKey(pg => pg.PlayerId);

            modelBuilder.Entity<PlayerGuild>()
                .HasOne(pg => pg.Guild)
                .WithMany(g => g.PlayerGuilds)
                .HasForeignKey(pg => pg.GuildId);

            // Настройка связи между Quest и QuestParticipant
            modelBuilder.Entity<QuestParticipant>()
                .HasKey(qp => new { qp.QuestId, qp.PlayerId });

            modelBuilder.Entity<QuestParticipant>()
                .HasOne(qp => qp.Quest)
                .WithMany(q => q.QuestParticipants)
                .HasForeignKey(qp => qp.QuestId);

            modelBuilder.Entity<QuestParticipant>()
                .HasOne(qp => qp.Player)
                .WithMany()
                .HasForeignKey(qp => qp.PlayerId);

            // Настройка связи "многие ко многим" между игроками и предметами
            modelBuilder.Entity<PlayerItem>()
                .HasKey(pi => new { pi.PlayerId, pi.ItemId });

            modelBuilder.Entity<PlayerItem>()
                .HasOne(pi => pi.Player)
                .WithMany(p => p.PlayerItems)
                .HasForeignKey(pi => pi.PlayerId);

            modelBuilder.Entity<PlayerItem>()
                .HasOne(pi => pi.Item)
                .WithMany()
                .HasForeignKey(pi => pi.ItemId);

            // Настройка связи между PlayerTrade и PlayerItem
            modelBuilder.Entity<PlayerTrade>()
                .HasMany(pt => pt.Player1Items)
                .WithOne()
                .HasForeignKey("PlayerTradeId1")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PlayerTrade>()
                .HasMany(pt => pt.Player2Items)
                .WithOne()
                .HasForeignKey("PlayerTradeId2")
                .OnDelete(DeleteBehavior.Restrict);

            // Настройка связи между PlayerTrade и Players
            modelBuilder.Entity<PlayerTrade>()
                .HasOne(pt => pt.Player1)
                .WithMany()
                .HasForeignKey(pt => pt.Player1Id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PlayerTrade>()
                .HasOne(pt => pt.Player2)
                .WithMany()
                .HasForeignKey(pt => pt.Player2Id)
                .OnDelete(DeleteBehavior.Restrict);

            // Настройка таблицы Items
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // Настройка конвертера и ValueComparer для Stats
            modelBuilder.Entity<Item>()
                .Property(i => i.Stats)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonOptions),
                    v => JsonSerializer.Deserialize<Dictionary<string, double>>(v, jsonOptions)!)
                .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, double>>(
                    (c1, c2) => c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value.GetHashCode())),
                    c => c.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)));

            // Настройка конвертера и ValueComparer для Achievements
            modelBuilder.Entity<Item>()
                .Property(i => i.Achievements)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonOptions),
                    v => JsonSerializer.Deserialize<List<Achievement>>(v, jsonOptions)!)
                .Metadata.SetValueComparer(new ValueComparer<List<Achievement>>(
                    (c1, c2) => c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            // Настройка конвертера и ValueComparer для AchievementBonuses
            modelBuilder.Entity<Item>()
                .Property(i => i.AchievementBonuses)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonOptions),
                    v => JsonSerializer.Deserialize<List<ItemStats>>(v, jsonOptions)!)
                .Metadata.SetValueComparer(new ValueComparer<List<ItemStats>>(
                    (c1, c2) => c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            // Настройка связи между ChatMessage и Player (отправитель)
            modelBuilder.Entity<ChatMessage>()
                .HasOne(cm => cm.Sender)
                .WithMany()
                .HasForeignKey(cm => cm.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            // Настройка связи между ChatMessage и Player (получатель)
            modelBuilder.Entity<ChatMessage>()
                .HasOne(cm => cm.Receiver)
                .WithMany()
                .HasForeignKey(cm => cm.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            // Настройка таблицы Quests
            modelBuilder.Entity<Quest>()
                .Property(q => q.ConditionsJson)
                .HasColumnType("nvarchar(max)");

            modelBuilder.Entity<Quest>()
                .Ignore(q => q.Conditions);

            // Индекс для Player.Name (уникальное имя игрока)
            modelBuilder.Entity<Player>()
                .HasIndex(p => p.Name)
                .IsUnique();

            // Индекс для Item.Name (уникальное имя предмета)
            modelBuilder.Entity<Item>()
                .HasIndex(i => i.Name)
                .IsUnique();

            // Индекс для Quest.Name (уникальное имя квеста)
            modelBuilder.Entity<Quest>()
                .HasIndex(q => q.Name)
                .IsUnique();

            // Настройка связи между локациями (переходы между локациями)
            modelBuilder.Entity<Location>()
                .HasMany(l => l.ConnectedLocations)
                .WithMany()
                .UsingEntity(j => j.ToTable("LocationConnections"));

            // Настройка связи игрока с текущей локацией
            modelBuilder.Entity<Player>()
                .HasOne(p => p.CurrentLocation)
                .WithMany()
                .HasForeignKey(p => p.CurrentLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Настройка связи мобов с локациями
            modelBuilder.Entity<Mob>()
                .HasOne(m => m.SpawnLocation)
                .WithMany()
                .HasForeignKey(m => m.SpawnLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Настройка связи NPC с локациями
            modelBuilder.Entity<NPC>()
                .HasOne(n => n.SpawnLocation)
                .WithMany()
                .HasForeignKey(n => n.SpawnLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}