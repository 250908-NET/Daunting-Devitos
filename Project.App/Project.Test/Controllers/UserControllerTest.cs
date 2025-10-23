using Microsoft.AspNetCore.Mvc;
using Moq;
using Project.Api.Controllers;
using Project.Api.Models;
using Project.Api.Services.Interface;

namespace Project.Api.Tests
{
    public class UserControllerTests
    {
        private readonly Mock<IUserService> _mockSvc;
        private readonly UserController _controller;

        public UserControllerTests()
        {
            _mockSvc = new Mock<IUserService>();
            _controller = new UserController(_mockSvc.Object);
        }

        [Fact]
        public async Task GetAllUsers_ReturnsOkResult_WithUsers()
        {
            // Arrange
            var users = new List<User>
            {
                new User
                {
                    Id = Guid.NewGuid(),
                    Name = "Sneha",
                    Email = "sneha@example.com",
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Name = "Leo",
                    Email = "leo@example.com",
                },
            };
            _mockSvc.Setup(s => s.GetAllUsersAsync()).ReturnsAsync(users);

            // Act
            var result = await _controller.GetAllUsers();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnUsers = Assert.IsAssignableFrom<IEnumerable<User>>(okResult.Value);
            Assert.Equal(2, ((List<User>)returnUsers).Count);
        }

        [Fact]
        public async Task GetUserById_ReturnsOk_WhenUserExists()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Name = "Sneha",
                Email = "sneha@example.com",
            };
            _mockSvc.Setup(s => s.GetUserByIdAsync(userId)).ReturnsAsync(user);

            // Act
            var result = await _controller.GetUserById(userId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnUser = Assert.IsType<User>(okResult.Value);
            Assert.Equal(userId, returnUser.Id);
        }

        [Fact]
        public async Task GetUserById_ReturnsNotFound_WhenUserDoesNotExist()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockSvc.Setup(s => s.GetUserByIdAsync(userId)).ReturnsAsync((User?)null);

            // Act
            var result = await _controller.GetUserById(userId);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateUser_ReturnsCreatedAtAction()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = "Sneha",
                Email = "sneha@example.com",
            };
            _mockSvc.Setup(s => s.CreateUserAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);

            // Act
            var result = await _controller.CreateUser(user);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnUser = Assert.IsType<User>(createdResult.Value);
            Assert.Equal(user.Id, returnUser.Id);
        }

        [Fact]
        public async Task UpdateUser_ReturnsNoContent_WhenSuccessful()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var existingUser = new User { Id = userId, Name = "Old Name" };
            var updatedUser = new User { Id = userId, Name = "New Name" };

            _mockSvc.Setup(s => s.GetUserByIdAsync(userId)).ReturnsAsync(existingUser);
            _mockSvc
                .Setup(s => s.UpdateUserAsync(userId, It.IsAny<User>()))
                .ReturnsAsync(updatedUser);

            // Act
            var result = await _controller.UpdateUser(userId, updatedUser);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteUser_ReturnsNoContent_WhenSuccessful()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new Project.Api.Models.User
            {
                Id = userId,
                Name = "Sneha",
                Email = "sneha@example.com",
            };

            _mockSvc.Setup(s => s.GetUserByIdAsync(userId)).ReturnsAsync(user);
            _mockSvc.Setup(s => s.DeleteUserAsync(userId)).ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteUser(userId);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteUser_ReturnsNotFound_WhenUserDoesNotExist()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockSvc.Setup(s => s.DeleteUserAsync(userId)).ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteUser(userId);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }
    }
}