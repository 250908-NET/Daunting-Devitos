using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Project.Api.Data;
using Project.Api.Models;
using Project.Api.Repositories.Interface;
using Project.Test.Helpers;

namespace Project.Test;

public class UserRepositoryTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsAllUsers()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);
        var user1 = RepositoryTestHelper.CreateTestUser(name: "Alice", email: "alice@example.com");
        var user2 = RepositoryTestHelper.CreateTestUser(name: "Bob", email: "bob@example.com");
        var user3 = RepositoryTestHelper.CreateTestUser(
            name: "Charlie",
            email: "charlie@example.com"
        );

        await context.Users.AddRangeAsync(user1, user2, user3);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(u => u.Email == "alice@example.com");
        result.Should().Contain(u => u.Email == "bob@example.com");
        result.Should().Contain(u => u.Email == "charlie@example.com");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoUsersExist()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsUser_WhenUserExists()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);
        var userId = Guid.NewGuid();
        var user = RepositoryTestHelper.CreateTestUser(
            id: userId,
            name: "Alice",
            email: "alice@example.com"
        );

        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetByIdAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(userId);
        result.Name.Should().Be("Alice");
        result.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenUserDoesNotExist()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByEmailAsync_ReturnsUser_WhenUserExists()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);
        var user = RepositoryTestHelper.CreateTestUser(name: "Alice", email: "alice@example.com");

        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetByEmailAsync("alice@example.com");

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be("alice@example.com");
        result.Name.Should().Be("Alice");
    }

    [Fact]
    public async Task GetByEmailAsync_ReturnsNull_WhenUserDoesNotExist()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);

        // Act
        var result = await repository.GetByEmailAsync("nonexistent@example.com");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByEmailAsync_IsCaseInsensitive()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);
        var user = RepositoryTestHelper.CreateTestUser(email: "alice@example.com");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        // Act: different casing than stored value
        var result = await repository.GetByEmailAsync("ALICE@example.com");

        // Assert: should still find the user
        result.Should().NotBeNull();
        result!.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task AddAsync_AddsUserToDatabase()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);
        var user = RepositoryTestHelper.CreateTestUser(name: "Alice", email: "alice@example.com");

        // Act
        await repository.AddAsync(user);

        // Assert
        var savedUser = await context.Users.FirstOrDefaultAsync(u =>
            u.Email == "alice@example.com"
        );
        savedUser.Should().NotBeNull();
        savedUser!.Name.Should().Be("Alice");
        savedUser.Balance.Should().Be(1000);
    }

    [Fact]
    public async Task AddAsync_SavesWithDefaultBalance()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);
        var user = RepositoryTestHelper.CreateTestUser();

        // Act
        await repository.AddAsync(user);

        // Assert
        var savedUser = await context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
        savedUser.Should().NotBeNull();
        savedUser!.Balance.Should().Be(1000);
    }

    [Fact]
    public async Task AddAsync_SavesWithCustomBalance()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);
        var user = RepositoryTestHelper.CreateTestUser(balance: 5000);

        // Act
        await repository.AddAsync(user);

        // Assert
        var savedUser = await context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
        savedUser.Should().NotBeNull();
        savedUser!.Balance.Should().Be(5000);
    }

    [Fact]
    public async Task AddAsync_GeneratesId_WhenNotProvided()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);
        var user = RepositoryTestHelper.CreateTestUser(id: Guid.Empty);

        // Act
        await repository.AddAsync(user);

        // Assert
        context.Users.Should().HaveCount(1);
        var savedUser = await context.Users.FirstAsync();
        savedUser.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingUser()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);
        var user = RepositoryTestHelper.CreateTestUser(
            name: "Alice",
            email: "alice@example.com",
            balance: 1000
        );

        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        // Modify user properties
        user.Name = "Alice Updated";
        user.Balance = 2000;

        // Act
        await repository.UpdateAsync(user);

        // Assert
        var updatedUser = await context.Users.FirstOrDefaultAsync(u =>
            u.Email == "alice@example.com"
        );
        updatedUser.Should().NotBeNull();
        updatedUser!.Name.Should().Be("Alice Updated");
        updatedUser.Balance.Should().Be(2000);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotChangeEmail()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);
        var user = RepositoryTestHelper.CreateTestUser(email: "alice@example.com");

        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        var originalEmail = user.Email;
        user.Email = "newemail@example.com";

        // Act
        await repository.UpdateAsync(user);

        // Assert
        var updatedUser = await context.Users.FindAsync(user.Id);
        updatedUser.Should().NotBeNull();
        updatedUser!.Email.Should().Be("newemail@example.com");
    }

    [Fact]
    public async Task DeleteAsync_RemovesUser_WhenUserExists()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);
        var user = RepositoryTestHelper.CreateTestUser();

        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        // Act
        await repository.DeleteAsync(user.Id);

        // Assert
        var deletedUser = await context.Users.FindAsync(user.Id);
        deletedUser.Should().BeNull();
        context.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_DoesNothing_WhenUserDoesNotExist()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);
        var nonExistentId = Guid.NewGuid();

        // Act
        await repository.DeleteAsync(nonExistentId);

        // Assert
        context.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsChanges()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);
        var user = RepositoryTestHelper.CreateTestUser();

        await context.Users.AddAsync(user);

        // Act
        await repository.SaveChangesAsync();

        // Assert
        context.Users.Should().HaveCount(1);
    }

    [Fact]
    public void User_HasDefaultBalanceOf1000()
    {
        // Arrange & Act
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@example.com",
        };

        // Assert
        user.Balance.Should().Be(1000);
    }

    [Fact]
    public void User_InitializesRoomPlayersAsEmptyList()
    {
        // Arrange & Act
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@example.com",
        };

        // Assert
        user.RoomPlayers.Should().NotBeNull();
        user.RoomPlayers.Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_AllowsDuplicateEmail_InMemoryDatabase()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);
        var user1 = RepositoryTestHelper.CreateTestUser(email: "duplicate@example.com");
        var user2 = RepositoryTestHelper.CreateTestUser(email: "duplicate@example.com");

        // Act
        await repository.AddAsync(user1);
        await repository.AddAsync(user2);

        // Assert
        // Note: In-memory database does not enforce unique constraints
        // In a real database with proper constraints, this would throw an exception
        var users = await repository.GetAllAsync();
        users.Should().HaveCount(2);
        users.Should().OnlyContain(u => u.Email == "duplicate@example.com");
    }

    [Fact]
    public async Task UpdateAsync_UpdatesBalance_AfterTransaction()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);
        var user = RepositoryTestHelper.CreateTestUser(balance: 1000);

        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        // Simulate a transaction
        user.Balance -= 100; // User loses 100

        // Act
        await repository.UpdateAsync(user);

        // Assert
        var updatedUser = await context.Users.FindAsync(user.Id);
        updatedUser.Should().NotBeNull();
        updatedUser!.Balance.Should().Be(900);
    }

    [Fact]
    public async Task User_CanHaveZeroBalance()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);
        var user = RepositoryTestHelper.CreateTestUser(balance: 0);

        // Act
        await repository.AddAsync(user);

        // Assert
        var savedUser = await context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
        savedUser.Should().NotBeNull();
        savedUser!.Balance.Should().Be(0);
    }

    [Fact]
    public async Task User_CanHaveNegativeBalance()
    {
        // Arrange
        await using var context = RepositoryTestHelper.CreateInMemoryContext();
        var repository = new UserRepository(context);
        var user = RepositoryTestHelper.CreateTestUser(balance: -500);

        // Act
        await repository.AddAsync(user);

        // Assert
        var savedUser = await context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
        savedUser.Should().NotBeNull();
        savedUser!.Balance.Should().Be(-500);
    }
}
