using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Project.Api.Data;
using Project.Api.Enums;
using Project.Api.Models;
using Project.Api.Repositories;
using Project.Api.Utilities;
using Project.Test.Helpers;

namespace Project.Test.Tests.Repositories;

public class RoomPlayerRepositoryTests
{
    #region Bet

    [Fact]
    public async Task GetByIdAsync_ReturnsRoomPlayer_WhenRoomPlayerExists()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user = RepositoryTestHelper.CreateTestUser();
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user.Id);
        var roomPlayerId = Guid.NewGuid();
        var roomPlayer = RepositoryTestHelper.CreateTestRoomPlayer(
            id: roomPlayerId,
            roomId: room.Id,
            userId: user.Id,
            role: Role.Admin
        );

        await context.Users.AddAsync(user);
        await context.Rooms.AddAsync(room);
        await context.RoomPlayers.AddAsync(roomPlayer);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetByIdAsync(roomPlayerId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(roomPlayerId);
        result.RoomId.Should().Be(room.Id);
        result.UserId.Should().Be(user.Id);
        result.Role.Should().Be(Role.Admin);
        result.Room.Should().NotBeNull();
        result.User.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenRoomPlayerDoesNotExist()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllRoomPlayers()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user1 = RepositoryTestHelper.CreateTestUser(name: "Alice");
        var user2 = RepositoryTestHelper.CreateTestUser(name: "Bob");
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user1.Id);
        var rp1 = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user1.Id,
            role: Role.Admin
        );
        var rp2 = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user2.Id,
            role: Role.Player
        );

        await context.Users.AddRangeAsync(user1, user2);
        await context.Rooms.AddAsync(room);
        await context.RoomPlayers.AddRangeAsync(rp1, rp2);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(rp => rp.UserId == user1.Id);
        result.Should().Contain(rp => rp.UserId == user2.Id);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoRoomPlayersExist()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByRoomIdAsync_ReturnsAllPlayersInRoom()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user1 = RepositoryTestHelper.CreateTestUser(name: "Alice");
        var user2 = RepositoryTestHelper.CreateTestUser(name: "Bob");
        var user3 = RepositoryTestHelper.CreateTestUser(name: "Charlie");
        var room1 = RepositoryTestHelper.CreateTestRoom(hostId: user1.Id);
        var room2 = RepositoryTestHelper.CreateTestRoom(hostId: user3.Id);

        var rp1 = RepositoryTestHelper.CreateTestRoomPlayer(roomId: room1.Id, userId: user1.Id);
        var rp2 = RepositoryTestHelper.CreateTestRoomPlayer(roomId: room1.Id, userId: user2.Id);
        var rp3 = RepositoryTestHelper.CreateTestRoomPlayer(roomId: room2.Id, userId: user3.Id);

        await context.Users.AddRangeAsync(user1, user2, user3);
        await context.Rooms.AddRangeAsync(room1, room2);
        await context.RoomPlayers.AddRangeAsync(rp1, rp2, rp3);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetByRoomIdAsync(room1.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(rp => rp.UserId == user1.Id);
        result.Should().Contain(rp => rp.UserId == user2.Id);
        result.Should().NotContain(rp => rp.UserId == user3.Id);
    }

    [Fact]
    public async Task GetByRoomIdAsync_ReturnsEmptyList_WhenNoPlayersInRoom()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var nonExistentRoomId = Guid.NewGuid();

        // Act
        var result = await repository.GetByRoomIdAsync(nonExistentRoomId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsAllRoomsForUser()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user = RepositoryTestHelper.CreateTestUser(name: "Alice");
        var room1 = RepositoryTestHelper.CreateTestRoom(hostId: user.Id);
        var room2 = RepositoryTestHelper.CreateTestRoom(hostId: user.Id);

        var rp1 = RepositoryTestHelper.CreateTestRoomPlayer(roomId: room1.Id, userId: user.Id);
        var rp2 = RepositoryTestHelper.CreateTestRoomPlayer(roomId: room2.Id, userId: user.Id);

        await context.Users.AddAsync(user);
        await context.Rooms.AddRangeAsync(room1, room2);
        await context.RoomPlayers.AddRangeAsync(rp1, rp2);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetByUserIdAsync(user.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(rp => rp.RoomId == room1.Id);
        result.Should().Contain(rp => rp.RoomId == room2.Id);
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsEmptyList_WhenUserNotInAnyRoom()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await repository.GetByUserIdAsync(nonExistentUserId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByRoomAndUserAsync_ReturnsRoomPlayer_WhenExists()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user = RepositoryTestHelper.CreateTestUser(name: "Alice");
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user.Id);
        var roomPlayer = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user.Id,
            role: Role.Admin
        );

        await context.Users.AddAsync(user);
        await context.Rooms.AddAsync(room);
        await context.RoomPlayers.AddAsync(roomPlayer);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetByRoomIdAndUserIdAsync(room.Id, user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.RoomId.Should().Be(room.Id);
        result.UserId.Should().Be(user.Id);
        result.Role.Should().Be(Role.Admin);
    }

    [Fact]
    public async Task GetByRoomAndUserAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var nonExistentRoomId = Guid.NewGuid();
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await repository.GetByRoomIdAndUserIdAsync(
            nonExistentRoomId,
            nonExistentUserId
        );

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActivePlayersInRoomAsync_ReturnsOnlyActivePlayers()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user1 = RepositoryTestHelper.CreateTestUser(name: "Alice");
        var user2 = RepositoryTestHelper.CreateTestUser(name: "Bob");
        var user3 = RepositoryTestHelper.CreateTestUser(name: "Charlie");
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user1.Id);

        var rp1 = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user1.Id,
            status: Status.Active
        );
        var rp2 = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user2.Id,
            status: Status.Inactive
        );
        var rp3 = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user3.Id,
            status: Status.Away
        );

        await context.Users.AddRangeAsync(user1, user2, user3);
        await context.Rooms.AddAsync(room);
        await context.RoomPlayers.AddRangeAsync(rp1, rp2, rp3);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetActivePlayersInRoomAsync(room.Id);

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(rp => rp.UserId == user1.Id && rp.Status == Status.Active);
        result.Should().NotContain(rp => rp.Status != Status.Active);
    }

    [Fact]
    public async Task GetActivePlayersInRoomAsync_ReturnsEmptyList_WhenNoActivePlayers()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user1 = RepositoryTestHelper.CreateTestUser(name: "Alice");
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user1.Id);
        var rp1 = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user1.Id,
            status: Status.Inactive
        );

        await context.Users.AddAsync(user1);
        await context.Rooms.AddAsync(room);
        await context.RoomPlayers.AddAsync(rp1);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetActivePlayersInRoomAsync(room.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_AddsRoomPlayerToDatabase()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user = RepositoryTestHelper.CreateTestUser();
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user.Id);
        var roomPlayer = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user.Id
        );

        await context.Users.AddAsync(user);
        await context.Rooms.AddAsync(room);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.CreateAsync(roomPlayer);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(roomPlayer.Id);
        context.RoomPlayers.Should().HaveCount(1);
        var savedRoomPlayer = await context.RoomPlayers.FirstOrDefaultAsync(rp =>
            rp.Id == roomPlayer.Id
        );
        savedRoomPlayer.Should().NotBeNull();
        savedRoomPlayer!.RoomId.Should().Be(room.Id);
        savedRoomPlayer.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task CreateAsync_SetsDefaultValues()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user = RepositoryTestHelper.CreateTestUser();
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user.Id);
        var roomPlayer = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user.Id,
            role: Role.Player,
            status: Status.Active,
            balance: 5000
        );

        await context.Users.AddAsync(user);
        await context.Rooms.AddAsync(room);
        await context.SaveChangesAsync();

        // Act
        await repository.CreateAsync(roomPlayer);

        // Assert
        var savedRoomPlayer = await context.RoomPlayers.FirstOrDefaultAsync(rp =>
            rp.Id == roomPlayer.Id
        );
        savedRoomPlayer.Should().NotBeNull();
        savedRoomPlayer!.Role.Should().Be(Role.Player);
        savedRoomPlayer.Status.Should().Be(Status.Active);
        savedRoomPlayer.Balance.Should().Be(5000);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingRoomPlayer()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user = RepositoryTestHelper.CreateTestUser();
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user.Id);
        var roomPlayer = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user.Id,
            role: Role.Player,
            status: Status.Active,
            balance: 1000
        );

        await context.Users.AddAsync(user);
        await context.Rooms.AddAsync(room);
        await context.RoomPlayers.AddAsync(roomPlayer);
        await context.SaveChangesAsync();

        // Modify roomPlayer
        roomPlayer.Role = Role.Moderator;
        roomPlayer.Status = Status.Away;
        roomPlayer.Balance = 2000;

        // Act
        await repository.UpdateAsync(roomPlayer);

        // Assert
        var updatedRoomPlayer = await context.RoomPlayers.FirstOrDefaultAsync(rp =>
            rp.Id == roomPlayer.Id
        );
        updatedRoomPlayer.Should().NotBeNull();
        updatedRoomPlayer!.Role.Should().Be(Role.Moderator);
        updatedRoomPlayer.Status.Should().Be(Status.Away);
        updatedRoomPlayer.Balance.Should().Be(2000);
    }

    [Fact]
    public async Task DeleteAsync_RemovesRoomPlayer_WhenExists()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user = RepositoryTestHelper.CreateTestUser();
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user.Id);
        var roomPlayer = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user.Id
        );

        await context.Users.AddAsync(user);
        await context.Rooms.AddAsync(room);
        await context.RoomPlayers.AddAsync(roomPlayer);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.DeleteAsync(roomPlayer.Id);

        // Assert
        result.Should().BeTrue();
        context.RoomPlayers.Should().BeEmpty();
        var deletedRoomPlayer = await context.RoomPlayers.FindAsync(roomPlayer.Id);
        deletedRoomPlayer.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenRoomPlayerDoesNotExist()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await repository.DeleteAsync(nonExistentId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_WhenRoomPlayerExists()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user = RepositoryTestHelper.CreateTestUser();
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user.Id);
        var roomPlayer = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user.Id
        );

        await context.Users.AddAsync(user);
        await context.Rooms.AddAsync(room);
        await context.RoomPlayers.AddAsync(roomPlayer);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.ExistsAsync(roomPlayer.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenRoomPlayerDoesNotExist()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await repository.ExistsAsync(nonExistentId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsPlayerInRoomAsync_ReturnsTrue_WhenPlayerInRoom()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user = RepositoryTestHelper.CreateTestUser();
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user.Id);
        var roomPlayer = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user.Id
        );

        await context.Users.AddAsync(user);
        await context.Rooms.AddAsync(room);
        await context.RoomPlayers.AddAsync(roomPlayer);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.IsPlayerInRoomAsync(room.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsPlayerInRoomAsync_ReturnsFalse_WhenPlayerNotInRoom()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var nonExistentRoomId = Guid.NewGuid();
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await repository.IsPlayerInRoomAsync(nonExistentRoomId, nonExistentUserId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetPlayerCountInRoomAsync_ReturnsCorrectCount()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user1 = RepositoryTestHelper.CreateTestUser(name: "Alice");
        var user2 = RepositoryTestHelper.CreateTestUser(name: "Bob");
        var user3 = RepositoryTestHelper.CreateTestUser(name: "Charlie");
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user1.Id);

        var rp1 = RepositoryTestHelper.CreateTestRoomPlayer(roomId: room.Id, userId: user1.Id);
        var rp2 = RepositoryTestHelper.CreateTestRoomPlayer(roomId: room.Id, userId: user2.Id);
        var rp3 = RepositoryTestHelper.CreateTestRoomPlayer(roomId: room.Id, userId: user3.Id);

        await context.Users.AddRangeAsync(user1, user2, user3);
        await context.Rooms.AddAsync(room);
        await context.RoomPlayers.AddRangeAsync(rp1, rp2, rp3);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetPlayerCountInRoomAsync(room.Id);

        // Assert
        result.Should().Be(3);
    }

    [Fact]
    public async Task GetPlayerCountInRoomAsync_ReturnsZero_WhenNoPlayersInRoom()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var nonExistentRoomId = Guid.NewGuid();

        // Act
        var result = await repository.GetPlayerCountInRoomAsync(nonExistentRoomId);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task GetRoomHostAsync_ReturnsHost_WhenHostExists()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var host = RepositoryTestHelper.CreateTestUser(name: "Host");
        var player = RepositoryTestHelper.CreateTestUser(name: "Player");
        var room = RepositoryTestHelper.CreateTestRoom(hostId: host.Id);

        var hostRoomPlayer = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: host.Id,
            role: Role.Admin
        );
        var playerRoomPlayer = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: player.Id,
            role: Role.Player
        );

        await context.Users.AddRangeAsync(host, player);
        await context.Rooms.AddAsync(room);
        await context.RoomPlayers.AddRangeAsync(hostRoomPlayer, playerRoomPlayer);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetRoomHostAsync(room.Id);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(host.Id);
        result.Role.Should().Be(Role.Admin);
    }

    [Fact]
    public async Task GetRoomHostAsync_ReturnsNull_WhenNoHostExists()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var nonExistentRoomId = Guid.NewGuid();

        // Act
        var result = await repository.GetRoomHostAsync(nonExistentRoomId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRoomHostAsync_ReturnsNull_WhenOnlyPlayersExist()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user = RepositoryTestHelper.CreateTestUser(name: "Player");
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user.Id);
        var roomPlayer = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user.Id,
            role: Role.Player
        );

        await context.Users.AddAsync(user);
        await context.Rooms.AddAsync(room);
        await context.RoomPlayers.AddAsync(roomPlayer);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetRoomHostAsync(room.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePlayerStatusAsync_UpdatesStatus_WhenPlayerExists()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user = RepositoryTestHelper.CreateTestUser();
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user.Id);
        var roomPlayer = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user.Id,
            status: Status.Active
        );

        await context.Users.AddAsync(user);
        await context.Rooms.AddAsync(room);
        await context.RoomPlayers.AddAsync(roomPlayer);
        await context.SaveChangesAsync();

        // Act
        await repository.UpdatePlayerStatusAsync(roomPlayer.Id, Status.Away);

        // Assert
        var updatedPlayer = await context.RoomPlayers.FindAsync(roomPlayer.Id);
        updatedPlayer.Should().NotBeNull();
        updatedPlayer!.Status.Should().Be(Status.Away);
    }

    [Fact]
    public async Task UpdatePlayerStatusAsync_ThrowsNotFoundException_WhenPlayerDoesNotExist()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var nonExistentId = Guid.NewGuid();

        // Act
        Func<Task> act = () => repository.UpdatePlayerStatusAsync(nonExistentId, Status.Inactive);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdatePlayerBalanceAsync_UpdatesBalance_WhenPlayerExists()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user = RepositoryTestHelper.CreateTestUser();
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user.Id);
        var roomPlayer = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user.Id,
            balance: 1000
        );

        await context.Users.AddAsync(user);
        await context.Rooms.AddAsync(room);
        await context.RoomPlayers.AddAsync(roomPlayer);
        await context.SaveChangesAsync();

    // Act - add 4000 to initial 1000 to reach expected 5000
    await repository.UpdatePlayerBalanceAsync(roomPlayer.Id, 4000);

    // Assert
    var updatedPlayer = await context.RoomPlayers.FindAsync(roomPlayer.Id);
    updatedPlayer.Should().NotBeNull();
    updatedPlayer!.Balance.Should().Be(5000);
    }

    [Fact]
    public async Task UpdatePlayerBalanceAsync_ThrowsNotFoundException_WhenPlayerDoesNotExist()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var nonExistentId = Guid.NewGuid();

        // Act
        Func<Task> act = () => repository.UpdatePlayerBalanceAsync(nonExistentId, 5000);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdatePlayerBalanceAsync_AllowsNegativeBalance()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user = RepositoryTestHelper.CreateTestUser();
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user.Id);
        var roomPlayer = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user.Id,
            balance: 1000
        );

        await context.Users.AddAsync(user);
        await context.Rooms.AddAsync(room);
        await context.RoomPlayers.AddAsync(roomPlayer);
        await context.SaveChangesAsync();

    // Act - subtract 1500 from initial 1000 to reach expected -500
    await repository.UpdatePlayerBalanceAsync(roomPlayer.Id, -1500);

    // Assert
    var updatedPlayer = await context.RoomPlayers.FindAsync(roomPlayer.Id);
    updatedPlayer.Should().NotBeNull();
    updatedPlayer!.Balance.Should().Be(-500);
    }

    [Fact]
    public async Task GetByIdAsync_IncludesNavigationProperties()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user = RepositoryTestHelper.CreateTestUser(name: "Alice", email: "alice@test.com");
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user.Id);
        var roomPlayer = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user.Id
        );

        await context.Users.AddAsync(user);
        await context.Rooms.AddAsync(room);
        await context.RoomPlayers.AddAsync(roomPlayer);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetByIdAsync(roomPlayer.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Room.Should().NotBeNull();
        result.User.Should().NotBeNull();
        result.User!.Name.Should().Be("Alice");
        result.User.Email.Should().Be("alice@test.com");
        result.Room!.Id.Should().Be(room.Id);
    }

    [Fact]
    public async Task RoomPlayer_CanHaveMultipleRoles()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user1 = RepositoryTestHelper.CreateTestUser(name: "Admin");
        var user2 = RepositoryTestHelper.CreateTestUser(name: "Moderator");
        var user3 = RepositoryTestHelper.CreateTestUser(name: "Player");
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user1.Id);

        var rp1 = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user1.Id,
            role: Role.Admin
        );
        var rp2 = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user2.Id,
            role: Role.Moderator
        );
        var rp3 = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user3.Id,
            role: Role.Player
        );

        await context.Users.AddRangeAsync(user1, user2, user3);
        await context.Rooms.AddAsync(room);
        await context.RoomPlayers.AddRangeAsync(rp1, rp2, rp3);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetByRoomIdAsync(room.Id);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(rp => rp.Role == Role.Admin);
        result.Should().Contain(rp => rp.Role == Role.Moderator);
        result.Should().Contain(rp => rp.Role == Role.Player);
    }

    [Fact]
    public async Task RoomPlayer_CanHaveMultipleStatuses()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new RoomPlayerRepository(context);
        var user1 = RepositoryTestHelper.CreateTestUser(name: "Active");
        var user2 = RepositoryTestHelper.CreateTestUser(name: "Inactive");
        var user3 = RepositoryTestHelper.CreateTestUser(name: "Away");
        var user4 = RepositoryTestHelper.CreateTestUser(name: "Left");
        var room = RepositoryTestHelper.CreateTestRoom(hostId: user1.Id);

        var rp1 = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user1.Id,
            status: Status.Active
        );
        var rp2 = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user2.Id,
            status: Status.Inactive
        );
        var rp3 = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user3.Id,
            status: Status.Away
        );
        var rp4 = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: room.Id,
            userId: user4.Id,
            status: Status.Left
        );

        await context.Users.AddRangeAsync(user1, user2, user3, user4);
        await context.Rooms.AddAsync(room);
        await context.RoomPlayers.AddRangeAsync(rp1, rp2, rp3, rp4);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetByRoomIdAsync(room.Id);

        // Assert
        result.Should().HaveCount(4);
        result.Should().Contain(rp => rp.Status == Status.Active);
        result.Should().Contain(rp => rp.Status == Status.Inactive);
        result.Should().Contain(rp => rp.Status == Status.Away);
        result.Should().Contain(rp => rp.Status == Status.Left);
    }

    #endregion
}
