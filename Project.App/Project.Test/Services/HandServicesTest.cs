using Microsoft.Extensions.Logging;
using Moq;
using Project.Api.Models;
using Project.Api.Repositories.Interface;
using Project.Api.Services;

namespace Project.Test.Services
{
    public class HandServicesTest
    {
        private readonly Mock<IHandRepository> _handRepositoryMock;

        private readonly HandService _handService;

        public HandServicesTest()
        {
            _handRepositoryMock = new Mock<IHandRepository>();
            _handService = new HandService(
                _handRepositoryMock.Object,
                Mock.Of<ILogger<HandService>>()
            );
        }

        [Fact]
        public async Task CreateHandAsync_ValidHand_ReturnsCreatedHand()
        {
            var handId = Guid.NewGuid();
            var roomId = Guid.NewGuid();
            // Arrange
            var hand = new Hand
            {
                Id = handId,
                RoomPlayerId = roomId,
                Order = 1,
                Bet = 100,
            };

            _handRepositoryMock
                .Setup(repo => repo.CreateHandAsync(It.IsAny<Hand>()))
                .ReturnsAsync(hand);

            var createdHand = await _handService.CreateHandAsync(hand);

            Assert.NotNull(createdHand);
            Assert.Equal(handId, createdHand.Id);
            Assert.Equal(roomId, createdHand.RoomPlayerId);
            Assert.Equal(1, createdHand.Order);
            Assert.Equal(100, createdHand.Bet);
            _handRepositoryMock.Verify(repo => repo.CreateHandAsync(It.IsAny<Hand>()), Times.Once);
        }

        [Fact]
        public async Task GetHandsByRoomIdAsync_ValidRoomId_ReturnsHandsList()
        {
            var roomId = Guid.NewGuid();
            // Arrange
            var hands = new List<Hand>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    RoomPlayerId = roomId,
                    Order = 1,
                    Bet = 100,
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    RoomPlayerId = roomId,
                    Order = 2,
                    Bet = 200,
                },
            };

            _handRepositoryMock
                .Setup(repo => repo.GetHandsByRoomIdAsync(roomId))
                .ReturnsAsync(hands);

            var resultHands = await _handService.GetHandsByRoomIdAsync(roomId);

            Assert.NotNull(resultHands);
            Assert.Equal(2, resultHands.Count);
            _handRepositoryMock.Verify(repo => repo.GetHandsByRoomIdAsync(roomId), Times.Once);
        }

        [Fact]
        public async Task GetTaskByRoomIdAsync_InvalidRoomId_ThrowsException()
        {
            // Arrange

            _handRepositoryMock
                .Setup(repo => repo.GetHandsByRoomIdAsync(It.IsAny<Guid>()))
                .ThrowsAsync(new Exception("No hands found"));

            await Assert.ThrowsAsync<Exception>(() =>
                _handService.GetHandsByRoomIdAsync(Guid.NewGuid())
            );

            _handRepositoryMock.Verify(
                repo => repo.GetHandsByRoomIdAsync(It.IsAny<Guid>()),
                Times.Once
            );
        }

        [Fact]
        public async Task GetHandByIdAsync_ValidHandId_ReturnsHand()
        {
            var handId = Guid.NewGuid();
            // Arrange
            var hand = new Hand
            {
                Id = handId,
                RoomPlayerId = Guid.NewGuid(),
                Order = 1,
                Bet = 100,
            };

            _handRepositoryMock.Setup(repo => repo.GetHandByIdAsync(handId)).ReturnsAsync(hand);

            var resultHand = await _handService.GetHandByIdAsync(handId);

            Assert.NotNull(resultHand);
            Assert.Equal(handId, resultHand.Id);
            _handRepositoryMock.Verify(repo => repo.GetHandByIdAsync(handId), Times.Once);
        }

        [Fact]
        public async Task GetHandByIdAsync_InvalidHandId_ThrowsException()
        {
            // Arrange
            _handRepositoryMock
                .Setup(repo => repo.GetHandByIdAsync(It.IsAny<Guid>()))
                .ThrowsAsync(new Exception("Hand not found"));

            // Act
            await Assert.ThrowsAsync<Exception>(() =>
                _handService.GetHandByIdAsync(Guid.NewGuid())
            );

            _handRepositoryMock.Verify(repo => repo.GetHandByIdAsync(It.IsAny<Guid>()), Times.Once);
        }

        [Fact]
        public async Task GetHandsByUserIdAsync_ValidIds_ReturnsHandsList()
        {
            var roomId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            // Arrange
            var hands = new List<Hand>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    RoomPlayerId = roomId,
                    Order = 1,
                    Bet = 100,
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    RoomPlayerId = roomId,
                    Order = 2,
                    Bet = 200,
                },
            };

            _handRepositoryMock
                .Setup(repo => repo.GetHandsByUserIdAsync(roomId, userId))
                .ReturnsAsync(hands);

            var resultHands = await _handService.GetHandsByUserIdAsync(roomId, userId);

            Assert.NotNull(resultHands);
            Assert.Equal(2, resultHands.Count);
            _handRepositoryMock.Verify(
                repo => repo.GetHandsByUserIdAsync(roomId, userId),
                Times.Once
            );
        }

        [Fact]
        public async Task GetHandsByUserIdAsync_InvalidIds_ThrowsException()
        {
            //Arrange
            _handRepositoryMock
                .Setup(repo => repo.GetHandsByUserIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ThrowsAsync(new Exception("No hands found"));
            // Act
            await Assert.ThrowsAsync<Exception>(() =>
                _handService.GetHandsByUserIdAsync(Guid.NewGuid(), Guid.NewGuid())
            );
            // Assert
            _handRepositoryMock.Verify(
                repo => repo.GetHandsByUserIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()),
                Times.Once
            );
        }

        [Fact]
        public async Task UpdateHandAsync_ValidHand_ReturnsUpdatedHand()
        {
            var handId = Guid.NewGuid();
            var roomPlayerId = Guid.NewGuid();
            // Arrange
            var hand = new Hand
            {
                Id = handId,
                RoomPlayerId = roomPlayerId,
                Order = 1,
                Bet = 100,
            };

            var updatedHand = new Hand
            {
                Id = handId,
                RoomPlayerId = roomPlayerId,
                Order = 2,
                Bet = 200,
            };

            _handRepositoryMock
                .Setup(repo => repo.UpdateHandAsync(handId, It.IsAny<Hand>()))
                .ReturnsAsync(updatedHand);

            var resultHand = await _handService.UpdateHandAsync(handId, updatedHand);

            Assert.NotNull(resultHand);
            Assert.Equal(2, resultHand.Order);
            Assert.Equal(200, resultHand.Bet);
            _handRepositoryMock.Verify(
                repo => repo.UpdateHandAsync(handId, It.IsAny<Hand>()),
                Times.Once
            );
        }

        [Fact]
        public async Task UpdateHandAsync_InvalidHand_ThrowsException()
        {
            var handId = Guid.NewGuid();
            var roomPlayerId = Guid.NewGuid();
            // Arrange
            var updatedHand = new Hand
            {
                Id = handId,
                RoomPlayerId = roomPlayerId,
                Order = 2,
                Bet = 200,
            };

            _handRepositoryMock
                .Setup(repo => repo.UpdateHandAsync(handId, It.IsAny<Hand>()))
                .ThrowsAsync(new Exception("Hand not found"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() =>
                _handService.UpdateHandAsync(handId, updatedHand)
            );

            _handRepositoryMock.Verify(
                repo => repo.UpdateHandAsync(handId, It.IsAny<Hand>()),
                Times.Once
            );
        }

        [Fact]
        public async Task PatchHandAsync_ValidHand_ReturnsPatchedHand()
        {
            var handId = Guid.NewGuid();
            var roomPlayerId = Guid.NewGuid();
            // Arrange
            var hand = new Hand
            {
                Id = handId,
                RoomPlayerId = roomPlayerId,
                Order = 1,
                Bet = 100,
            };

            var patchedHand = new Hand
            {
                Id = handId,
                RoomPlayerId = roomPlayerId,
                Order = 1,
                Bet = 150,
            };

            _handRepositoryMock
                .Setup(repo => repo.PatchHandAsync(handId, 2, 150))
                .ReturnsAsync(patchedHand);

            var resultHand = await _handService.PatchHandAsync(handId, 2, 150);

            Assert.NotNull(resultHand);
            Assert.Equal(1, resultHand.Order);
            Assert.Equal(150, resultHand.Bet);
            _handRepositoryMock.Verify(repo => repo.PatchHandAsync(handId, 2, 150), Times.Once);
        }

        [Fact]
        public async Task PatchHandAsync_InvalidHand_ThrowsException()
        {
            var handId = Guid.NewGuid();
            // Arrange

            _handRepositoryMock
                .Setup(repo => repo.PatchHandAsync(handId, It.IsAny<int?>(), It.IsAny<int?>()))
                .ThrowsAsync(new Exception("Hand not found"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _handService.PatchHandAsync(handId, 2, 150));

            _handRepositoryMock.Verify(
                repo => repo.PatchHandAsync(handId, It.IsAny<int?>(), It.IsAny<int?>()),
                Times.Once
            );
        }

        [Fact]
        public async Task DeleteHandAsync_ValidHandId_ReturnsDeletedHand()
        {
            var handId = Guid.NewGuid();
            var roomPlayerId = Guid.NewGuid();
            // Arrange
            var hand = new Hand
            {
                Id = handId,
                RoomPlayerId = roomPlayerId,
                Order = 1,
                Bet = 100,
            };

            _handRepositoryMock.Setup(repo => repo.DeleteHandAsync(handId)).ReturnsAsync(hand);

            var resultHand = await _handService.DeleteHandAsync(handId);

            Assert.NotNull(resultHand);
            Assert.Equal(handId, resultHand.Id);
            _handRepositoryMock.Verify(repo => repo.DeleteHandAsync(handId), Times.Once);
        }

        [Fact]
        public async Task DeleteHandAsync_InvalidHandId_ThrowsException()
        {
            var handId = Guid.NewGuid();
            // Arrange

            _handRepositoryMock
                .Setup(repo => repo.DeleteHandAsync(handId))
                .ThrowsAsync(new Exception("Hand not found"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _handService.DeleteHandAsync(handId));

            _handRepositoryMock.Verify(repo => repo.DeleteHandAsync(handId), Times.Once);
        }
    }
}
