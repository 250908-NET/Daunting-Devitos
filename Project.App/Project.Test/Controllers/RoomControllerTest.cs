using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Project.Api.Controllers;
using Project.Api.DTOs;
using Project.Api.Services.Interface;
using Project.Api.Utilities;

namespace Project.Test.Tests.Controllers
{
    public class RoomControllerTest
    {
        private readonly Mock<IRoomService> _mockRoomService;
        private readonly Mock<ILogger<RoomController>> _mockLogger;
        private readonly Mock<IRoomSSEService> _mockRoomSseService;
        private readonly RoomController _controller;

        public RoomControllerTest()
        {
            _mockRoomService = new Mock<IRoomService>();
            _mockRoomSseService = new Mock<IRoomSSEService>();
            _mockLogger = new Mock<ILogger<RoomController>>();
            _controller = new RoomController(
                _mockRoomService.Object,
                _mockRoomSseService.Object,
                _mockLogger.Object
            );
        }

        #region GET Tests

        [Fact]
        public async Task GetAllRooms_ReturnsOkResult_WithListOfRooms()
        {
            // Arrange
            var rooms = new List<RoomDTO>
            {
                new RoomDTO
                {
                    Id = Guid.NewGuid(),
                    HostId = Guid.NewGuid(),
                    GameMode = "Blackjack",
                    IsActive = true,
                },
                new RoomDTO
                {
                    Id = Guid.NewGuid(),
                    HostId = Guid.NewGuid(),
                    GameMode = "Poker",
                    IsActive = true,
                },
            };
            _mockRoomService.Setup(service => service.GetAllRoomsAsync()).ReturnsAsync(rooms);

            // Act
            var result = await _controller.GetAllRooms();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnRooms = Assert.IsAssignableFrom<IEnumerable<RoomDTO>>(okResult.Value);
            Assert.Equal(2, returnRooms.Count());
        }

        [Fact]
        public async Task GetActiveRooms_ReturnsOkResult_WithActiveRooms()
        {
            // Arrange
            var activeRooms = new List<RoomDTO>
            {
                new RoomDTO
                {
                    Id = Guid.NewGuid(),
                    HostId = Guid.NewGuid(),
                    IsActive = true,
                },
            };
            _mockRoomService
                .Setup(service => service.GetActiveRoomsAsync())
                .ReturnsAsync(activeRooms);

            // Act
            var result = await _controller.GetActiveRooms();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnRooms = Assert.IsAssignableFrom<IEnumerable<RoomDTO>>(okResult.Value);
            Assert.Single(returnRooms);
        }

        [Fact]
        public async Task GetPublicRooms_ReturnsOkResult_WithPublicRooms()
        {
            // Arrange
            var publicRooms = new List<RoomDTO>
            {
                new RoomDTO
                {
                    Id = Guid.NewGuid(),
                    HostId = Guid.NewGuid(),
                    IsPublic = true,
                },
            };
            _mockRoomService
                .Setup(service => service.GetPublicRoomsAsync())
                .ReturnsAsync(publicRooms);

            // Act
            var result = await _controller.GetPublicRooms();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnRooms = Assert.IsAssignableFrom<IEnumerable<RoomDTO>>(okResult.Value);
            Assert.Single(returnRooms);
        }

        [Fact]
        public async Task GetRoomById_ReturnsOkResult_WhenRoomExists()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var room = new RoomDTO
            {
                Id = roomId,
                HostId = Guid.NewGuid(),
                GameMode = "Blackjack",
            };
            _mockRoomService.Setup(service => service.GetRoomByIdAsync(roomId)).ReturnsAsync(room);

            // Act
            var result = await _controller.GetRoomById(roomId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnRoom = Assert.IsType<RoomDTO>(okResult.Value);
            Assert.Equal(roomId, returnRoom.Id);
        }

        [Fact]
        public async Task GetRoomById_ThrowsNotFoundException_WhenRoomDoesNotExist()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            _mockRoomService
                .Setup(service => service.GetRoomByIdAsync(roomId))
                .ReturnsAsync((RoomDTO?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetRoomById(roomId));
        }

        [Fact]
        public async Task GetRoomByHostId_ReturnsOkResult_WhenRoomExists()
        {
            // Arrange
            var hostId = Guid.NewGuid();
            var room = new RoomDTO
            {
                Id = Guid.NewGuid(),
                HostId = hostId,
                GameMode = "Blackjack",
            };
            _mockRoomService
                .Setup(service => service.GetRoomByHostIdAsync(hostId))
                .ReturnsAsync(room);

            // Act
            var result = await _controller.GetRoomByHostId(hostId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnRoom = Assert.IsType<RoomDTO>(okResult.Value);
            Assert.Equal(hostId, returnRoom.HostId);
        }

        [Fact]
        public async Task GetRoomByHostId_ThrowsNotFoundException_WhenRoomDoesNotExist()
        {
            // Arrange
            var hostId = Guid.NewGuid();
            _mockRoomService
                .Setup(service => service.GetRoomByHostIdAsync(hostId))
                .ReturnsAsync((RoomDTO?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetRoomByHostId(hostId));
        }

        [Fact]
        public async Task RoomExists_ReturnsOkResult_WithTrue_WhenRoomExists()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            _mockRoomService.Setup(service => service.RoomExistsAsync(roomId)).ReturnsAsync(true);

            // Act
            var result = await _controller.RoomExists(roomId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.True((bool)okResult.Value!);
        }

        [Fact]
        public async Task RoomExists_ReturnsOkResult_WithFalse_WhenRoomDoesNotExist()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            _mockRoomService.Setup(service => service.RoomExistsAsync(roomId)).ReturnsAsync(false);

            // Act
            var result = await _controller.RoomExists(roomId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.False((bool)okResult.Value!);
        }

        [Fact]
        public async Task GetGameState_ReturnsOkResult_WithGameState()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var gameState = "{\"players\": [], \"currentRound\": 1}";
            _mockRoomService
                .Setup(service => service.GetGameStateAsync(roomId))
                .ReturnsAsync(gameState);

            // Act
            var result = await _controller.GetGameState(roomId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(gameState, okResult.Value);
        }

        [Fact]
        public async Task GetGameConfig_ReturnsOkResult_WithGameConfig()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var gameConfig = "{\"maxBet\": 1000, \"minBet\": 10}";
            _mockRoomService
                .Setup(service => service.GetGameConfigAsync(roomId))
                .ReturnsAsync(gameConfig);

            // Act
            var result = await _controller.GetGameConfig(roomId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(gameConfig, okResult.Value);
        }

        #endregion

        #region POST Tests

        [Fact]
        public async Task CreateRoom_ReturnsCreatedAtAction_WithRoom()
        {
            // Arrange
            var createDto = new CreateRoomDTO
            {
                HostId = Guid.NewGuid(),
                IsPublic = true,
                GameMode = "Blackjack",
                MaxPlayers = 6,
                MinPlayers = 2,
                DeckId = "deck123",
            };
            var createdRoom = new RoomDTO
            {
                Id = Guid.NewGuid(),
                HostId = createDto.HostId,
                IsPublic = createDto.IsPublic,
                GameMode = createDto.GameMode,
                MaxPlayers = createDto.MaxPlayers,
                MinPlayers = createDto.MinPlayers,
                DeckId = createDto.DeckId,
            };
            _mockRoomService
                .Setup(service => service.CreateRoomAsync(createDto))
                .ReturnsAsync(createdRoom);

            // Act
            var result = await _controller.CreateRoom(createDto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnRoom = Assert.IsType<RoomDTO>(createdResult.Value);
            Assert.Equal(createdRoom.Id, returnRoom.Id);
            Assert.Equal(nameof(_controller.GetRoomById), createdResult.ActionName);
        }

        [Fact]
        public async Task CreateRoom_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var createDto = new CreateRoomDTO();
            _controller.ModelState.AddModelError("HostId", "Required");

            // Act
            var result = await _controller.CreateRoom(createDto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task StartGame_ReturnsOkResult_WithRoom()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var gameConfig = "{\"maxBet\": 1000}";
            var room = new RoomDTO
            {
                Id = roomId,
                HostId = Guid.NewGuid(),
                GameMode = "Blackjack",
            };
            _mockRoomService
                .Setup(service => service.StartGameAsync(roomId, gameConfig))
                .ReturnsAsync(room);

            // Act
            var result = await _controller.StartGame(roomId, gameConfig);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnRoom = Assert.IsType<RoomDTO>(okResult.Value);
            Assert.Equal(roomId, returnRoom.Id);
        }

        [Fact]
        public async Task StartGame_ReturnsOkResult_WithNullGameConfig()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var room = new RoomDTO
            {
                Id = roomId,
                HostId = Guid.NewGuid(),
                GameMode = "Blackjack",
            };
            _mockRoomService
                .Setup(service => service.StartGameAsync(roomId, null))
                .ReturnsAsync(room);

            // Act
            var result = await _controller.StartGame(roomId, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnRoom = Assert.IsType<RoomDTO>(okResult.Value);
            Assert.Equal(roomId, returnRoom.Id);
        }

        [Fact]
        public async Task PerformPlayerAction_ReturnsOk_WhenActionIsValid()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var playerId = Guid.NewGuid();
            var request = new PlayerActionRequest
            {
                Action = "Hit",
                Data = JsonDocument.Parse("{}").RootElement,
            };
            _mockRoomService
                .Setup(service =>
                    service.PerformPlayerActionAsync(roomId, playerId, request.Action, request.Data)
                )
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.PerformPlayerAction(roomId, playerId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            _mockRoomService.Verify(
                service =>
                    service.PerformPlayerActionAsync(
                        roomId,
                        playerId,
                        request.Action,
                        request.Data
                    ),
                Times.Once
            );
        }

        [Fact]
        public async Task PerformPlayerAction_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var playerId = Guid.NewGuid();
            var request = new PlayerActionRequest();
            _controller.ModelState.AddModelError("Action", "Required");

            // Act
            var result = await _controller.PerformPlayerAction(roomId, playerId, request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task JoinRoom_ReturnsOkResult_WithRoom()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var request = new JoinRoomRequest { UserId = userId };
            var room = new RoomDTO { Id = roomId, HostId = Guid.NewGuid() };
            _mockRoomService
                .Setup(service => service.JoinRoomAsync(roomId, userId))
                .ReturnsAsync(room);

            // Act
            var result = await _controller.JoinRoom(roomId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnRoom = Assert.IsType<RoomDTO>(okResult.Value);
            Assert.Equal(roomId, returnRoom.Id);
        }

        [Fact]
        public async Task JoinRoom_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var request = new JoinRoomRequest();
            _controller.ModelState.AddModelError("UserId", "Required");

            // Act
            var result = await _controller.JoinRoom(roomId, request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task LeaveRoom_ReturnsOkResult_WithRoom()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var request = new LeaveRoomRequest { UserId = userId };
            var room = new RoomDTO { Id = roomId, HostId = Guid.NewGuid() };
            _mockRoomService
                .Setup(service => service.LeaveRoomAsync(roomId, userId))
                .ReturnsAsync(room);

            // Act
            var result = await _controller.LeaveRoom(roomId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnRoom = Assert.IsType<RoomDTO>(okResult.Value);
            Assert.Equal(roomId, returnRoom.Id);
        }

        [Fact]
        public async Task LeaveRoom_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var request = new LeaveRoomRequest();
            _controller.ModelState.AddModelError("UserId", "Required");

            // Act
            var result = await _controller.LeaveRoom(roomId, request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        #endregion

        #region PUT Tests

        [Fact]
        public async Task UpdateRoom_ReturnsOkResult_WhenUpdateIsSuccessful()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var updateDto = new UpdateRoomDTO
            {
                Id = roomId,
                HostId = Guid.NewGuid(),
                IsPublic = true,
                GameMode = "Blackjack",
                MaxPlayers = 6,
                MinPlayers = 2,
            };
            var updatedRoom = new RoomDTO
            {
                Id = roomId,
                HostId = updateDto.HostId,
                IsPublic = updateDto.IsPublic,
                GameMode = updateDto.GameMode,
                MaxPlayers = updateDto.MaxPlayers,
                MinPlayers = updateDto.MinPlayers,
            };
            _mockRoomService
                .Setup(service => service.UpdateRoomAsync(updateDto))
                .ReturnsAsync(updatedRoom);

            // Act
            var result = await _controller.UpdateRoom(roomId, updateDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnRoom = Assert.IsType<RoomDTO>(okResult.Value);
            Assert.Equal(roomId, returnRoom.Id);
        }

        [Fact]
        public async Task UpdateRoom_ThrowsBadRequestException_WhenIdMismatch()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var updateDto = new UpdateRoomDTO
            {
                Id = Guid.NewGuid(), // Different ID
                HostId = Guid.NewGuid(),
            };

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _controller.UpdateRoom(roomId, updateDto)
            );
        }

        [Fact]
        public async Task UpdateRoom_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var updateDto = new UpdateRoomDTO { Id = roomId };
            _controller.ModelState.AddModelError("HostId", "Required");

            // Act
            var result = await _controller.UpdateRoom(roomId, updateDto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task UpdateRoom_ThrowsNotFoundException_WhenRoomDoesNotExist()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var updateDto = new UpdateRoomDTO
            {
                Id = roomId,
                HostId = Guid.NewGuid(),
                GameMode = "Blackjack",
            };
            _mockRoomService
                .Setup(service => service.UpdateRoomAsync(updateDto))
                .ReturnsAsync((RoomDTO?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _controller.UpdateRoom(roomId, updateDto)
            );
        }

        [Fact]
        public async Task UpdateGameState_ReturnsNoContent_WhenUpdateIsSuccessful()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var gameState = "{\"players\": [], \"currentRound\": 2}";
            _mockRoomService
                .Setup(service => service.UpdateGameStateAsync(roomId, gameState))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateGameState(roomId, gameState);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateGameState_ThrowsNotFoundException_WhenRoomDoesNotExist()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var gameState = "{\"players\": []}";
            _mockRoomService
                .Setup(service => service.UpdateGameStateAsync(roomId, gameState))
                .ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _controller.UpdateGameState(roomId, gameState)
            );
        }

        [Fact]
        public async Task UpdateGameConfig_ReturnsNoContent_WhenUpdateIsSuccessful()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var gameConfig = "{\"maxBet\": 2000}";
            _mockRoomService
                .Setup(service => service.UpdateGameConfigAsync(roomId, gameConfig))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateGameConfig(roomId, gameConfig);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateGameConfig_ThrowsNotFoundException_WhenRoomDoesNotExist()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var gameConfig = "{\"maxBet\": 2000}";
            _mockRoomService
                .Setup(service => service.UpdateGameConfigAsync(roomId, gameConfig))
                .ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _controller.UpdateGameConfig(roomId, gameConfig)
            );
        }

        #endregion

        #region DELETE Tests

        [Fact]
        public async Task DeleteRoom_ReturnsNoContent_WhenDeleteIsSuccessful()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            _mockRoomService.Setup(service => service.DeleteRoomAsync(roomId)).ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteRoom(roomId);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteRoom_ThrowsNotFoundException_WhenRoomDoesNotExist()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            _mockRoomService.Setup(service => service.DeleteRoomAsync(roomId)).ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.DeleteRoom(roomId));
        }

        #endregion
    }
}
