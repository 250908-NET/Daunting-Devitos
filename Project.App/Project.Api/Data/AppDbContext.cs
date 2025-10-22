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
    /// DO NOT MODIFY THIS METHOD.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
