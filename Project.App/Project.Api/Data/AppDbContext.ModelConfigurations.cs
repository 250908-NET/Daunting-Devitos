using Microsoft.EntityFrameworkCore;
using Project.Api.Models;

namespace Project.Api.Data;

public partial class AppDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // apply any IEntityTypeConfiguration<> implementations found in this assembly
        // fully automatic! the wonders of modern technology
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder
            .Entity<User>() //email should be unique
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder
            .Entity<RoomPlayer>()
            .HasOne(rp => rp.User)
            .WithMany(u => u.RoomPlayers)
            .HasForeignKey(rp => rp.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder
            .Entity<RoomPlayer>()
            .HasOne(rp => rp.Room)
            .WithMany(r => r.RoomPlayers)
            .HasForeignKey(rp => rp.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure GameState as JSON column for SQL Server
        modelBuilder.Entity<Room>().Property(r => r.GameState).HasColumnType("nvarchar(max)");

        // Add unique constraint to prevent duplicate player entries in same room
        modelBuilder
            .Entity<RoomPlayer>()
            .HasIndex(rp => new { rp.RoomId, rp.UserId })
            .IsUnique()
            .HasDatabaseName("IX_RoomPlayer_RoomId_UserId_Unique");

        // example enum -> string conversion config
        // modelBuilder.Entity<Item>().Property(i => i.Condition).HasConversion<string>();
        // modelBuilder.Entity<Item>().Property(i => i.Availability).HasConversion<string>();
    }
}
