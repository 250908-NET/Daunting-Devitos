using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Project.Api.Models;
using Project.Api.Repositories;
using Project.Api.Services;
using Xunit;

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
                new Mock<ILogger<HandService>>().Object
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
                CardsJson = "[]",
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
            Assert.Equal("[]", createdHand.CardsJson);
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
                new Hand
                {
                    Id = Guid.NewGuid(),
                    RoomPlayerId = roomId,
                    Order = 1,
                    CardsJson = "[]",
                    Bet = 100,
                },
                new Hand
                {
                    Id = Guid.NewGuid(),
                    RoomPlayerId = roomId,
                    Order = 2,
                    CardsJson = "[]",
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
                CardsJson = "[]",
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
        }
    }
}
