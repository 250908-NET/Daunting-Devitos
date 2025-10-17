using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Project.Api.Data;
using Project.Api.Models;
using Project.Api.Repositories;

namespace Project.Test;

public class RoomRepositoryTests
{
    private AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private Room CreateTestRoom(
        Guid? id = null,
        Guid? hostId = null,
        bool isPublic = true,
        bool isActive = true
    )
    {
        return new Room
        {
            Id = id ?? Guid.NewGuid(),
            HostId = hostId ?? Guid.NewGuid(),
            isPublic = isPublic,
            isActive = isActive,
            CreatedAt = DateTime.UtcNow,
            GameMode = "Texas Hold'em",
            GameState = "{}",
            Description = "Test room",
            MaxPlayers = 6,
            MinPlayers = 2,
            DeckId = 1,
            Round = 0,
            State = "Waiting",
        };
    }

    private User CreateTestUser(Guid? id = null)
    {
        return new User
        {
            Id = id ?? Guid.NewGuid(),
            Name = "Test User",
            Email = "test@example.com",
            PasswordHash = "hashedpassword",
            Balance = 1000,
        };
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsRoom_WhenRoomExists()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);
        var hostId = Guid.NewGuid();
        var host = CreateTestUser(hostId);
        var room = CreateTestRoom(hostId: hostId);

        await context.Users.AddAsync(host);
        await context.Rooms.AddAsync(room);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetByIdAsync(room.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(room.Id);
        result.Host.Should().NotBeNull();
        result.RoomPlayers.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenRoomDoesNotExist()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllRooms()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);
        var host = CreateTestUser();
        var room1 = CreateTestRoom(hostId: host.Id, isPublic: true, isActive: true);
        var room2 = CreateTestRoom(hostId: host.Id, isPublic: false, isActive: false);
        var room3 = CreateTestRoom(hostId: host.Id, isPublic: true, isActive: false);

        await context.Users.AddAsync(host);
        await context.Rooms.AddRangeAsync(room1, room2, room3);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(r => r.Id == room1.Id);
        result.Should().Contain(r => r.Id == room2.Id);
        result.Should().Contain(r => r.Id == room3.Id);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoRoomsExist()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveRoomsAsync_ReturnsOnlyActiveRooms()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);
        var host = CreateTestUser();
        var activeRoom1 = CreateTestRoom(hostId: host.Id, isActive: true);
        var activeRoom2 = CreateTestRoom(hostId: host.Id, isActive: true);
        var inactiveRoom = CreateTestRoom(hostId: host.Id, isActive: false);

        await context.Users.AddAsync(host);
        await context.Rooms.AddRangeAsync(activeRoom1, activeRoom2, inactiveRoom);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetActiveRoomsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.Id == activeRoom1.Id);
        result.Should().Contain(r => r.Id == activeRoom2.Id);
        result.Should().NotContain(r => r.Id == inactiveRoom.Id);
    }

    [Fact]
    public async Task GetActiveRoomsAsync_ReturnsEmptyList_WhenNoActiveRooms()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);
        var host = CreateTestUser();
        var inactiveRoom = CreateTestRoom(hostId: host.Id, isActive: false);

        await context.Users.AddAsync(host);
        await context.Rooms.AddAsync(inactiveRoom);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetActiveRoomsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPublicRoomsAsync_ReturnsOnlyPublicAndActiveRooms()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);
        var host = CreateTestUser();
        var publicActiveRoom = CreateTestRoom(hostId: host.Id, isPublic: true, isActive: true);
        var publicInactiveRoom = CreateTestRoom(hostId: host.Id, isPublic: true, isActive: false);
        var privateActiveRoom = CreateTestRoom(hostId: host.Id, isPublic: false, isActive: true);
        var privateInactiveRoom = CreateTestRoom(hostId: host.Id, isPublic: false, isActive: false);

        await context.Users.AddAsync(host);
        await context.Rooms.AddRangeAsync(
            publicActiveRoom,
            publicInactiveRoom,
            privateActiveRoom,
            privateInactiveRoom
        );
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetPublicRoomsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(r => r.Id == publicActiveRoom.Id);
        result.Should().NotContain(r => r.Id == publicInactiveRoom.Id);
        result.Should().NotContain(r => r.Id == privateActiveRoom.Id);
        result.Should().NotContain(r => r.Id == privateInactiveRoom.Id);
    }

    [Fact]
    public async Task GetPublicRoomsAsync_ReturnsEmptyList_WhenNoPublicActiveRooms()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);
        var host = CreateTestUser();
        var privateRoom = CreateTestRoom(hostId: host.Id, isPublic: false, isActive: true);

        await context.Users.AddAsync(host);
        await context.Rooms.AddAsync(privateRoom);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetPublicRoomsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByHostIdAsync_ReturnsRoom_WhenRoomExistsForHost()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);
        var hostId = Guid.NewGuid();
        var host = CreateTestUser(hostId);
        var room = CreateTestRoom(hostId: hostId);

        await context.Users.AddAsync(host);
        await context.Rooms.AddAsync(room);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetByHostIdAsync(hostId);

        // Assert
        result.Should().NotBeNull();
        result!.HostId.Should().Be(hostId);
        result.Host.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByHostIdAsync_ReturnsNull_WhenNoRoomExistsForHost()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);
        var nonExistentHostId = Guid.NewGuid();

        // Act
        var result = await repository.GetByHostIdAsync(nonExistentHostId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByHostIdAsync_ReturnsFirstRoom_WhenMultipleRoomsExistForHost()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);
        var hostId = Guid.NewGuid();
        var host = CreateTestUser(hostId);
        var room1 = CreateTestRoom(hostId: hostId);
        var room2 = CreateTestRoom(hostId: hostId);

        await context.Users.AddAsync(host);
        await context.Rooms.AddRangeAsync(room1, room2);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetByHostIdAsync(hostId);

        // Assert
        result.Should().NotBeNull();
        result!.HostId.Should().Be(hostId);
    }

    [Fact]
    public async Task CreateAsync_AddsRoomToDatabase()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);
        var host = CreateTestUser();
        var room = CreateTestRoom(hostId: host.Id);

        await context.Users.AddAsync(host);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.CreateAsync(room);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(room.Id);

        var savedRoom = await context.Rooms.FindAsync(room.Id);
        savedRoom.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_GeneratesId_WhenNotProvided()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);
        var host = CreateTestUser();
        var room = CreateTestRoom(id: Guid.Empty, hostId: host.Id);

        await context.Users.AddAsync(host);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.CreateAsync(room);

        // Assert
        result.Should().NotBeNull();
        context.Rooms.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingRoom()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);
        var host = CreateTestUser();
        var room = CreateTestRoom(hostId: host.Id);

        await context.Users.AddAsync(host);
        await context.Rooms.AddAsync(room);
        await context.SaveChangesAsync();

        // Modify room properties
        room.Description = "Updated description";
        room.MaxPlayers = 8;
        room.isActive = false;

        // Act
        var result = await repository.UpdateAsync(room);

        // Assert
        result.Should().NotBeNull();
        result!.Description.Should().Be("Updated description");
        result.MaxPlayers.Should().Be(8);
        result.isActive.Should().BeFalse();

        var updatedRoom = await context.Rooms.FindAsync(room.Id);
        updatedRoom!.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenRoomDoesNotExist()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);
        var nonExistentRoom = CreateTestRoom();

        // Act
        var result = await repository.UpdateAsync(nonExistentRoom);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_RemovesRoom_WhenRoomExists()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);
        var host = CreateTestUser();
        var room = CreateTestRoom(hostId: host.Id);

        await context.Users.AddAsync(host);
        await context.Rooms.AddAsync(room);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.DeleteAsync(room.Id);

        // Assert
        result.Should().BeTrue();

        var deletedRoom = await context.Rooms.FindAsync(room.Id);
        deletedRoom.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenRoomDoesNotExist()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await repository.DeleteAsync(nonExistentId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_WhenRoomExists()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);
        var host = CreateTestUser();
        var room = CreateTestRoom(hostId: host.Id);

        await context.Users.AddAsync(host);
        await context.Rooms.AddAsync(room);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.ExistsAsync(room.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenRoomDoesNotExist()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repository = new RoomRepository(context);
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await repository.ExistsAsync(nonExistentId);

        // Assert
        result.Should().BeFalse();
    }
}
