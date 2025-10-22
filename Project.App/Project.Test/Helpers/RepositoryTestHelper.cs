using Microsoft.EntityFrameworkCore;
using Project.Api.Data;
using Project.Api.Enums;
using Project.Api.Models;

namespace Project.Test.Helpers;

// Helper class for repository unit tests, providing common utilities and test data creation methods.
public static class RepositoryTestHelper
{
    // Creates an in-memory database context for testing.
    public static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    // Creates a test user with optional custom properties.
    public static User CreateTestUser(
        Guid? id = null,
        string? name = null,
        string? email = null,
        double balance = 1000,
        string? avatarUrl = null
    )
    {
        return new User
        {
            Id = id ?? Guid.NewGuid(),
            Name = name ?? "Test User",
            Email = email ?? $"test{Guid.NewGuid()}@example.com",
            Balance = balance,
            AvatarUrl = avatarUrl,
        };
    }

    // Creates a test room with optional custom properties.
    public static Room CreateTestRoom(
        Guid? id = null,
        Guid? hostId = null,
        bool isPublic = true,
        bool isActive = true,
        string? gameMode = null,
        string? gameState = null,
        int maxPlayers = 6,
        int minPlayers = 2
    )
    {
        return new Room
        {
            Id = id ?? Guid.NewGuid(),
            HostId = hostId ?? Guid.NewGuid(),
            IsPublic = isPublic,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            GameMode = gameMode ?? "Texas Hold'em",
            GameState = gameState ?? "Waiting",
            MaxPlayers = maxPlayers,
            MinPlayers = minPlayers,
            DeckId = "1",
            Round = 0,
        };
    }

    // Creates a test room player with optional custom properties.
    public static RoomPlayer CreateTestRoomPlayer(
        Guid? id = null,
        Guid? roomId = null,
        Guid? userId = null,
        Role role = Role.Player,
        Status status = Status.Active,
        long balance = 1000
    )
    {
        return new RoomPlayer
        {
            Id = id ?? Guid.NewGuid(),
            RoomId = roomId ?? Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid(),
            Role = role,
            Status = status,
            Balance = balance,
        };
    }
}
