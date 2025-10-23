using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Project.Api.Models;
using Project.Api.Repositories.Interface;
using Project.Api.Services;
using Project.Test.Helpers;

namespace Project.Test.Services;

public class UserServiceTest
{
    private readonly UserService _service;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ILogger<UserService>> _loggerMock;

    public UserServiceTest()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _loggerMock = new Mock<ILogger<UserService>>();
        _service = new UserService(_userRepositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAllUsers_ReturnsAllUsers_WhenSuccessful()
    {
        // Arrange
        var users = new List<User>
        {
            RepositoryTestHelper.CreateTestUser(),
            RepositoryTestHelper.CreateTestUser(),
            RepositoryTestHelper.CreateTestUser(),
        };
        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(users);

        // Act
        var result = await _service.GetAllUsersAsync();

        // Assert
        result.Should().BeEquivalentTo(users);
        _userRepositoryMock.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task GetAllUsers_ThrowsException_WhenRepositoryFails()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.GetAllAsync())
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _service.GetAllUsersAsync());
    }

    [Fact]
    public async Task GetUserById_ReturnsUser_WhenExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = RepositoryTestHelper.CreateTestUser(id: userId);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        // Act
        var result = await _service.GetUserByIdAsync(userId);

        // Assert
        result.Should().BeEquivalentTo(user);
        _userRepositoryMock.Verify(r => r.GetByIdAsync(userId), Times.Once);
    }

    [Fact]
    public async Task GetUserById_ThrowsException_WhenRepositoryFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _service.GetUserByIdAsync(userId));
    }

    [Fact]
    public async Task GetUserByEmail_ReturnsUser_WhenExists()
    {
        // Arrange
        var email = "test@example.com";
        var user = RepositoryTestHelper.CreateTestUser(email: email);
        _userRepositoryMock.Setup(r => r.GetByEmailAsync(email)).ReturnsAsync(user);

        // Act
        var result = await _service.GetUserByEmailAsync(email);

        // Assert
        result.Should().BeEquivalentTo(user);
        _userRepositoryMock.Verify(r => r.GetByEmailAsync(email), Times.Once);
    }

    [Fact]
    public async Task GetUserByEmail_ThrowsException_WhenRepositoryFails()
    {
        // Arrange
        var email = "test@example.com";
        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(email))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _service.GetUserByEmailAsync(email));
    }

    [Fact]
    public async Task CreateUser_ReturnsCreatedUser_WhenSuccessful()
    {
        // Arrange
        var newUser = RepositoryTestHelper.CreateTestUser(name: "Danny", email: "danny@devito.net");

        // Setup AddAsync to complete successfully
        _userRepositoryMock.Setup(r => r.AddAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateUserAsync(newUser);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Danny");
        result.Email.Should().Be("danny@devito.net");

        // Verify AddAsync was called exactly once with the expected user
        _userRepositoryMock.Verify(
            r => r.AddAsync(It.Is<User>(u => u.Name == "Danny" && u.Email == "danny@devito.net")),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateUser_ReturnsUpdatedUser_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = RepositoryTestHelper.CreateTestUser(id: userId, name: "Updated Name");
        _userRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateUserAsync(userId, user);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(userId);
        result.Name.Should().Be("Updated Name");
        _userRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<User>(u => u.Id == userId && u.Name == "Updated Name")),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateUser_ThrowsException_WhenRepositoryFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = RepositoryTestHelper.CreateTestUser(id: userId);
        _userRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<User>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _service.UpdateUserAsync(userId, user));
    }

    [Fact]
    public async Task DeleteUser_ReturnsTrue_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepositoryMock.Setup(r => r.DeleteAsync(userId)).Returns(Task.CompletedTask);

        // Act
        var result = await _service.DeleteUserAsync(userId);

        // Assert
        result.Should().BeTrue();
        _userRepositoryMock.Verify(r => r.DeleteAsync(userId), Times.Once);
    }

    [Fact]
    public async Task DeleteUser_ThrowsException_WhenRepositoryFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(r => r.DeleteAsync(userId))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _service.DeleteUserAsync(userId));
    }

    [Fact]
    public async Task UpdateUserBalance_ReturnsUpdatedUser_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var startingUser = RepositoryTestHelper.CreateTestUser(id: userId, balance: 1000.0);
        var newBalance = 2000.0;

        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(startingUser);

        _userRepositoryMock
            .Setup(r => r.UpdateBalanceAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateUserBalanceAsync(userId, newBalance);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(userId);
        result.Balance.Should().Be(newBalance);

        _userRepositoryMock.Verify(r => r.GetByIdAsync(userId), Times.Once);
        _userRepositoryMock.Verify(
            r => r.UpdateBalanceAsync(It.Is<User>(u => u.Id == userId && u.Balance == newBalance)),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateUserBalance_ThrowsException_WhenUserNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync((User?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _service.UpdateUserBalanceAsync(userId, 2000.0)
        );
        exception.Message.Should().Contain($"User {userId} not found");
    }

    [Fact]
    public async Task UpsertGoogleUser_CreatesNewUser_WhenUserDoesNotExist()
    {
        // Arrange
        var email = "test@gmail.com";
        var name = "Test User";
        var avatarUrl = "http://example.com/avatar.jpg";

        _userRepositoryMock.Setup(r => r.GetByEmailAsync(email)).ReturnsAsync((User?)null);

        _userRepositoryMock.Setup(r => r.AddAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpsertGoogleUserByEmailAsync(email, name, avatarUrl);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be(email);
        result.Name.Should().Be(name);
        result.AvatarUrl.Should().Be(avatarUrl);
        _userRepositoryMock.Verify(
            r =>
                r.AddAsync(
                    It.Is<User>(u => u.Email == email && u.Name == name && u.AvatarUrl == avatarUrl)
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task UpsertGoogleUser_UpdatesExistingUser_WhenUserExists()
    {
        // Arrange
        var email = "test@gmail.com";
        var existingUser = RepositoryTestHelper.CreateTestUser(
            email: email,
            name: "Old Name",
            avatarUrl: "old-avatar.jpg"
        );
        var newName = "New Name";
        var newAvatarUrl = "new-avatar.jpg";

        _userRepositoryMock.Setup(r => r.GetByEmailAsync(email)).ReturnsAsync(existingUser);

        _userRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpsertGoogleUserByEmailAsync(email, newName, newAvatarUrl);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be(email);
        result.Name.Should().Be(newName);
        result.AvatarUrl.Should().Be(newAvatarUrl);
        _userRepositoryMock.Verify(
            r =>
                r.UpdateAsync(
                    It.Is<User>(u =>
                        u.Email == email && u.Name == newName && u.AvatarUrl == newAvatarUrl
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task UpsertGoogleUser_UsesEmailAsName_WhenNameIsEmpty()
    {
        // Arrange
        var email = "test@gmail.com";
        string? name = null;
        var avatarUrl = "http://example.com/avatar.jpg";

        _userRepositoryMock.Setup(r => r.GetByEmailAsync(email)).ReturnsAsync((User?)null);

        _userRepositoryMock.Setup(r => r.AddAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpsertGoogleUserByEmailAsync(email, name, avatarUrl);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be(email);
        result.Name.Should().Be(email);
        result.AvatarUrl.Should().Be(avatarUrl);
    }
}
