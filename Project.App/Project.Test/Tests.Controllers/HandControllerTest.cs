using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Project.Api.Controllers;
using Project.Api.DTOs;
using Project.Api.Models;
using Project.Api.Repositories;
using Project.Api.Services;
using Project.Api.Services.Interface;
using Project.Test.Helpers;
using Xunit;

namespace Project.Test.Controllers
{
    public class HandControllerTest
    {
        private readonly Mock<IHandService> _mockHandService;
        private readonly Mock<IMapper> _mockMapper;

    private readonly Mock<IDeckApiService> _mockDeckApiService;

        private HandController _controller;

        public HandControllerTest()
        {
            _mockHandService = new Mock<IHandService>();
            _mockMapper = new Mock<IMapper>();
            _mockDeckApiService = new Mock<IDeckApiService>();
            _controller = new HandController(
                Mock.Of<ILogger<HandController>>(),
                _mockHandService.Object,
                _mockDeckApiService.Object, 
                _mockMapper.Object
            );
        }

        [Fact]
        public async Task GetHandsByRoomId_ReturnsOkResult_WithListOfHandDTOs()
        {
            //Arrange
            var roomId = Guid.NewGuid();
            var handId = Guid.NewGuid();
            var handModels = new List<Hand>
            {
                new()
                {
                    Id = handId,
                    RoomPlayerId = Guid.NewGuid(),
                    Order = 1,
                    Bet = 100,
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    RoomPlayerId = Guid.NewGuid(),
                    Order = 2,
                    Bet = 200,
                },
                // Add more Hand objects if needed
            };

            var handDTOs = handModels
                .Select(h => new HandDTO
                {
                    Id = h.Id,
                    RoomPlayerId = h.RoomPlayerId,
                    Order = h.Order,
                    Bet = h.Bet,
                })
                .ToList();

            _mockHandService
                .Setup(service => service.GetHandsByRoomIdAsync(roomId))
                .ReturnsAsync(handModels);
            _mockMapper.Setup(m => m.Map<List<HandDTO>>(It.IsAny<List<Hand>>())).Returns(handDTOs);

            //Act

            var result = await _controller.GetHandsByRoomId(roomId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsAssignableFrom<List<HandDTO>>(okResult.Value);
            Assert.Equal(handDTOs.Count, returnValue.Count);

            _mockHandService.Verify(service => service.GetHandsByRoomIdAsync(roomId), Times.Once);
        }

        [Fact]
        public async Task GetHandById_ReturnsNotFound_WhenHandDoesNotExist()
        {
            // Arrange
            var handId = Guid.NewGuid();
            var roomId = Guid.NewGuid();

            _mockHandService
                .Setup(service => service.GetHandByIdAsync(handId))
                .ThrowsAsync(new Exception("Hand not found"));

            // Act

            var result = await _controller.GetHandById(handId, roomId);

            Assert.IsType<NotFoundObjectResult>(result);

            // Assert
            _mockHandService.Verify(service => service.GetHandByIdAsync(handId), Times.Once);
        }

        [Fact]
        public async Task GetHandById_ReturnsOkResult_WithHandDTO()
        {
            // Arrange
            var handId = Guid.NewGuid();
            var roomId = Guid.NewGuid();
            var roomPlayerId = Guid.NewGuid();

            var handModel = new Hand
            {
                Id = handId,
                RoomPlayer = new RoomPlayer
                {
                    Id = roomPlayerId,
                    RoomId = roomId,
                    UserId = Guid.NewGuid(),
                },
                RoomPlayerId = roomPlayerId,
                Order = 1,
                Bet = 100,
            };

            var handDTO = new HandDTO
            {
                Id = handModel.Id,
                RoomPlayerId = handModel.RoomPlayerId,
                Order = handModel.Order,
                Bet = handModel.Bet,
            };

            _mockHandService
                .Setup(service => service.GetHandByIdAsync(handId))
                .ReturnsAsync(handModel);
            _mockMapper.Setup(m => m.Map<HandDTO>(It.IsAny<Hand>())).Returns(handDTO);

            // Act
            var result = await _controller.GetHandById(handId, roomId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsType<HandDTO>(okResult.Value);
            Assert.Equal(handDTO.Id, returnValue.Id);

            _mockHandService.Verify(service => service.GetHandByIdAsync(handId), Times.Once);
        }

        [Fact]
        public async Task GetHandsByUserId_ReturnsNotFound_WhenNoHandsExist()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var roomId = Guid.NewGuid();

            _mockHandService
                .Setup(service => service.GetHandsByUserIdAsync(roomId, userId))
                .ThrowsAsync(new Exception("No hands found"));

            // Act
            var result = await _controller.GetHandsByUserId(userId, roomId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("No hands found", notFoundResult.Value);

            _mockHandService.Verify(
                service => service.GetHandsByUserIdAsync(roomId, userId),
                Times.Once
            );
        }

        [Fact]
        public async Task GetHandsByUserId_ReturnsOkResult_WithListOfHandDTOs()
        {
            //Arrange
            var roomId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var handModels = new List<Hand>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    RoomPlayerId = Guid.NewGuid(),
                    Order = 1,
                    Bet = 100,
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    RoomPlayerId = Guid.NewGuid(),
                    Order = 2,
                    Bet = 200,
                },
                // Add more Hand objects if needed
            };

            var handDTOs = handModels
                .Select(h => new HandDTO
                {
                    Id = h.Id,
                    RoomPlayerId = h.RoomPlayerId,
                    Order = h.Order,
                    Bet = h.Bet,
                })
                .ToList();

            _mockHandService
                .Setup(service => service.GetHandsByUserIdAsync(roomId, userId))
                .ReturnsAsync(handModels);
            _mockMapper.Setup(m => m.Map<List<HandDTO>>(It.IsAny<List<Hand>>())).Returns(handDTOs);

            //Act

            var result = await _controller.GetHandsByUserId(userId, roomId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsAssignableFrom<List<HandDTO>>(okResult.Value);
            Assert.Equal(handDTOs.Count, returnValue.Count);

            _mockHandService.Verify(
                service => service.GetHandsByUserIdAsync(roomId, userId),
                Times.Once
            );
        }

        [Fact]
        public async Task CreateHand_ReturnsOkResult_WithCreatedHandDTO()
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var handDTO = new HandDTO
            {
                Id = Guid.NewGuid(),
                RoomPlayerId = Guid.NewGuid(),
                Order = 1,
                Bet = 100,
            };

            var handModel = new Hand
            {
                Id = handDTO.Id,
                RoomPlayerId = handDTO.RoomPlayerId,
                Order = handDTO.Order,
                Bet = handDTO.Bet,
            };

            _mockMapper.Setup(m => m.Map<Hand>(It.IsAny<HandDTO>())).Returns(handModel);
            _mockHandService
                .Setup(service => service.CreateHandAsync(handModel))
                .ReturnsAsync(handModel);
            _mockMapper.Setup(m => m.Map<HandDTO>(It.IsAny<Hand>())).Returns(handDTO);

            // Act
            var result = await _controller.CreateHand(roomId, handDTO);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsType<HandDTO>(okResult.Value);
            Assert.Equal(handDTO.Id, returnValue.Id);

            _mockHandService.Verify(service => service.CreateHandAsync(handModel), Times.Once);
        }

        

        [Fact]
        public async Task UpdateHandBet_ReturnsOkResult_WithUpdatedHandDTO()
        {
            // Arrange
            var handId = Guid.NewGuid();
            var newBet = 500;

            var updatedHandModel = new Hand
            {
                Id = handId,
                RoomPlayerId = Guid.NewGuid(),
                Order = 1,
                Bet = newBet,
            };

            var updatedHandDTO = new HandDTO
            {
                Id = updatedHandModel.Id,
                RoomPlayerId = updatedHandModel.RoomPlayerId,
                Order = updatedHandModel.Order,
                Bet = updatedHandModel.Bet,
            };

            _mockHandService
                .Setup(service => service.PatchHandAsync(handId, null, newBet))
                .ReturnsAsync(updatedHandModel);
            _mockMapper.Setup(m => m.Map<HandDTO>(It.IsAny<Hand>())).Returns(updatedHandDTO);

            // Act
            var result = await _controller.UpdateHandBet(handId, newBet);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsType<HandDTO>(okResult.Value);
            Assert.Equal(updatedHandDTO.Id, returnValue.Id);
            Assert.Equal(updatedHandDTO.Bet, returnValue.Bet);

            _mockHandService.Verify(
                service => service.PatchHandAsync(handId, null, newBet),
                Times.Once
            );
        }

        [Fact]
        public async Task UpdateHandBet_ReturnsBadRequest_WhenNewBetIsZero()
        {
            // Arrange
            var handId = Guid.NewGuid();
            var newBet = -1; // Invalid negative bet

            // Act
            var result = await _controller.UpdateHandBet(handId, newBet);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            _mockHandService.Verify(
                service => service.PatchHandAsync(It.IsAny<Guid>(), null, It.IsAny<int>()),
                Times.Never
            );
        }

        [Fact]
        public async Task DeleteHand_ReturnsOkResult_WithDeletedHandDTO()
        {
            // Arrange
            var handId = Guid.NewGuid();

            var deletedHandModel = new Hand
            {
                Id = handId,
                RoomPlayerId = Guid.NewGuid(),
                Order = 1,
                Bet = 100,
            };

            var deletedHandDTO = new HandDTO
            {
                Id = deletedHandModel.Id,
                RoomPlayerId = deletedHandModel.RoomPlayerId,
                Order = deletedHandModel.Order,
                Bet = deletedHandModel.Bet,
            };

            _mockHandService
                .Setup(service => service.DeleteHandAsync(handId))
                .ReturnsAsync(deletedHandModel);
            _mockMapper.Setup(m => m.Map<HandDTO>(It.IsAny<Hand>())).Returns(deletedHandDTO);

            // Act
            var result = await _controller.DeleteHand(handId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsType<HandDTO>(okResult.Value);
            Assert.Equal(deletedHandDTO.Id, returnValue.Id);

            _mockHandService.Verify(service => service.DeleteHandAsync(handId), Times.Once);
        }

        [Fact]
        public async Task DeleteHand_ReturnsNotFound_WhenHandDoesNotExist()
        {
            // Arrange
            var handId = Guid.NewGuid();

            _mockHandService
                .Setup(service => service.DeleteHandAsync(handId))
                .ThrowsAsync(new KeyNotFoundException("Hand not found"));

            // Act
            await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            {
                var result = await _controller.DeleteHand(handId);
            });

            // Assert

            _mockHandService.Verify(service => service.DeleteHandAsync(handId), Times.Once);
        }
    }
}
