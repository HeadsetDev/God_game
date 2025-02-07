namespace GameAuthAPI.Data
{
    using Microsoft.EntityFrameworkCore;
    using GameAuthAPI.Models;

    public class GameDbContext : DbContext
    {
        public GameDbContext(DbContextOptions<GameDbContext> options) : base(options) { }

        // Таблица игроков
        public DbSet<Player> Players { get; set; }

        // Таблица предметов
        public DbSet<Item> Items { get; set; }

        // Таблица связи игрока и предметов (инвентарь)
        public DbSet<PlayerItem> PlayerItems { get; set; }


        // Таблица торговли

        public DbSet<PlayerTrade> PlayerTrades { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer("Server=WIN-UPTPM1TVFS1\\GAME;Database=Game;User Id=sa;Password=GloryOrDead!;TrustServerCertificate=True;");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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
        }
    }
}
