using Microsoft.EntityFrameworkCore;
using Project.Api.Models;

namespace Project.Api.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Room> Rooms { get; set; }
    public DbSet<RoomPlayer> RoomPlayers { get; set; }
    public DbSet<Hand> Hands { get; set; }

    /// <summary>
    /// Provides the configuration for TradeHubContext models.
    /// Any custom configurations should be defined in <see cref="OnModelCreatingPartial"/>.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure GameState as JSON column for SQL Server
        modelBuilder.Entity<Room>().Property(r => r.GameState).HasColumnType("nvarchar(max)");

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
