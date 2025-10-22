using Microsoft.EntityFrameworkCore;
using Moq;
using Project.Api.Data;
using Project.Api.Models;
using Project.Api.Repositories;
using Project.Api.Repositories.Interface;

namespace Project.Test.Repositories
{
    public class HandRepositoryTest
    {
        private readonly Mock<IHandRepository> _handRepositoryMock;

        public HandRepositoryTest()
        {
            _handRepositoryMock = new Mock<IHandRepository>();
        }

        private DbContextOptions<AppDbContext> GetInMemoryDbOptions()
        {
            return new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public async Task GetHandAsyncById_ValidId_ReturnsHand()
        {
            // Arrange
            var options = GetInMemoryDbOptions();
            var handId = Guid.NewGuid();
            var roomPlayerId = Guid.NewGuid();

            using var context = new AppDbContext(options);
            context.Hands.Add(
                new Hand
                {
                    Id = handId,
                    Order = 1,
                    RoomPlayerId = roomPlayerId,
                    Bet = 0,
                }
            );
            await context.SaveChangesAsync();

            // Act
            var repo = new HandRepository(context);
            Hand? res = await repo.GetHandByIdAsync(context.Hands.First().Id);

            // Assert

            Assert.NotNull(res);
            Assert.Equal(1, res!.Order);
            Assert.Equal(roomPlayerId, res.RoomPlayerId);
            Assert.Equal(0, res.Bet);
        }

        [Fact]
        public async Task GetHandAsyncById_InvalidId_ReturnsNull()
        {
            // Arrange
            var options = GetInMemoryDbOptions();

            using var context = new AppDbContext(options);
            context.Hands.Add(
                new Hand
                {
                    Id = Guid.NewGuid(),
                    Order = 1,
                    RoomPlayerId = Guid.NewGuid(),
                    Bet = 0,
                }
            );
            await context.SaveChangesAsync();

            // Act
            var repo = new HandRepository(context);

            // Assert
            await Assert.ThrowsAsync<Exception>(async () =>
            {
                Hand? res = await repo.GetHandByIdAsync(Guid.NewGuid());
            });
        }

        [Fact]
        public async Task GetHandsByRoomIdAsync_ValidRoomId_ReturnsHands()
        {
            // Arrange
            var options = GetInMemoryDbOptions();
            var roomId = Guid.NewGuid();
            var roomPlayerId1 = Guid.NewGuid();
            var roomPlayerId2 = Guid.NewGuid();
            var roomPlayerId3 = Guid.NewGuid();
            var roomPlayerId4 = Guid.NewGuid();
            var handId1 = Guid.NewGuid();
            var handId2 = Guid.NewGuid();
            var handId3 = Guid.NewGuid();
            var handId4 = Guid.NewGuid();

            // Act
            using var context = new AppDbContext(options);
            context.RoomPlayers.Add(
                new RoomPlayer
                {
                    Id = roomPlayerId1,
                    RoomId = roomId,
                    UserId = Guid.NewGuid(),
                    Balance = 1000,
                }
            );
            context.RoomPlayers.Add(
                new RoomPlayer
                {
                    Id = roomPlayerId2,
                    RoomId = roomId,
                    UserId = Guid.NewGuid(),
                    Balance = 1000,
                }
            );
            context.RoomPlayers.Add(
                new RoomPlayer
                {
                    Id = roomPlayerId3,
                    RoomId = roomId,
                    UserId = Guid.NewGuid(),
                    Balance = 1000,
                }
            );
            context.RoomPlayers.Add(
                new RoomPlayer
                {
                    Id = roomPlayerId4,
                    RoomId = roomId,
                    UserId = Guid.NewGuid(),
                    Balance = 1000,
                }
            );
            context.Hands.Add(
                new Hand
                {
                    Id = handId1,
                    Order = 1,
                    RoomPlayerId = roomPlayerId1,
                    Bet = 0,
                }
            );
            context.Hands.Add(
                new Hand
                {
                    Id = handId2,
                    Order = 2,
                    RoomPlayerId = roomPlayerId2,
                    Bet = 0,
                }
            );
            context.Hands.Add(
                new Hand
                {
                    Id = handId3,
                    Order = 3,
                    RoomPlayerId = roomPlayerId3,
                    Bet = 0,
                }
            );
            context.Hands.Add(
                new Hand
                {
                    Id = handId4,
                    Order = 4,
                    RoomPlayerId = roomPlayerId4,
                    Bet = 0,
                }
            ); // Different RoomPlayer
            await context.SaveChangesAsync();

            // Assert

            var repo = new HandRepository(context);
            List<Hand> res = await repo.GetHandsByRoomIdAsync(roomId);
            Assert.Equal(4, res.Count);
            Assert.Equal(handId1, res[0].Id);
            Assert.Equal(handId2, res[1].Id);
            Assert.Equal(handId3, res[2].Id);
            Assert.Equal(handId4, res[3].Id);
            Assert.Equal(roomPlayerId1, res[0].RoomPlayerId);
            Assert.Equal(roomPlayerId2, res[1].RoomPlayerId);
            Assert.Equal(roomPlayerId3, res[2].RoomPlayerId);
            Assert.Equal(roomPlayerId4, res[3].RoomPlayerId);
        }

        [Fact]
        public async Task GetHandsByRoomIdAsync_InvalidRoomId_ThrowsArgumentException()
        {
            // Arrange
            var options = GetInMemoryDbOptions();

            using var context = new AppDbContext(options);
            var repo = new HandRepository(context);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await repo.GetHandsByRoomIdAsync(Guid.Empty);
            });
        }

        [Fact]
        public async Task CreateHandAsync_ValidHand_ReturnsCreatedHand()
        {
            // Arrange
            var options = GetInMemoryDbOptions();
            var handId = Guid.NewGuid();
            var roomPlayerId = Guid.NewGuid();
            var newHand = new Hand
            {
                Id = handId,
                Order = 1,
                RoomPlayerId = roomPlayerId,
                Bet = 0,
            };

            using var context = new AppDbContext(options);
            var repo = new HandRepository(context);

            // Act
            Hand res = await repo.CreateHandAsync(newHand);

            // Assert
            Assert.NotNull(res);
            Assert.Equal(handId, res.Id);
            Assert.Equal(1, res.Order);
            Assert.Equal(roomPlayerId, res.RoomPlayerId);
            Assert.Equal(0, res.Bet);
        }

        [Fact]
        public async Task CreateHandAsync_NullHand_ThrowsArgumentNullException()
        {
            // Arrange
            var options = GetInMemoryDbOptions();

            using var context = new AppDbContext(options);
            var repo = new HandRepository(context);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await repo.CreateHandAsync(null!);
            });
        }

        [Fact]
        public async Task UpdateHandAsync_ValidHand_ReturnsUpdatedHand()
        {
            // Arrange
            var options = GetInMemoryDbOptions();

            var handId = Guid.NewGuid();
            var roomPlayerId = Guid.NewGuid();

            using var context = new AppDbContext(options);

            // Act
            context.Hands.Add(
                new Hand
                {
                    Id = handId,
                    Order = 1,
                    RoomPlayerId = roomPlayerId,
                    Bet = 0,
                }
            );
            await context.SaveChangesAsync();
            var repo = new HandRepository(context);
            await repo.UpdateHandAsync(
                handId,
                new Hand
                {
                    Id = handId,
                    Order = 2,
                    RoomPlayerId = roomPlayerId,
                    Bet = 100,
                }
            );

            // Assert

            var res = await repo.GetHandByIdAsync(handId);
            Assert.NotNull(res);
            Assert.Equal(2, res.Order);
            Assert.Equal(roomPlayerId, res.RoomPlayerId);
            Assert.Equal(100, res.Bet);
        }

        [Fact]
        public async Task UpdateHandAsync_InvalidHandId_ThrowsKeyNotFoundException()
        {
            // Arrange
            var options = GetInMemoryDbOptions();

            using var context = new AppDbContext(options);
            var repo = new HandRepository(context);

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            {
                await repo.UpdateHandAsync(
                    Guid.NewGuid(),
                    new Hand
                    {
                        Id = Guid.NewGuid(),
                        Order = 1,
                        RoomPlayerId = Guid.NewGuid(),
                        Bet = 0,
                    }
                );
            });
        }

        [Fact]
        public async Task UpdateHandAsync_NullHand_ThrowsArgumentNullException()
        {
            // Arrange
            var options = GetInMemoryDbOptions();

            using var context = new AppDbContext(options);
            var repo = new HandRepository(context);

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            {
                await repo.UpdateHandAsync(Guid.NewGuid(), null!);
            });
        }

        [Fact]
        public async Task PatchHandAsync_ValidHand_ReturnsPatchedHand()
        {
            // Arrange
            var options = GetInMemoryDbOptions();

            var handId = Guid.NewGuid();
            var roomPlayerId = Guid.NewGuid();

            using var context = new AppDbContext(options);

            // Act
            context.Hands.Add(
                new Hand
                {
                    Id = handId,
                    Order = 1,
                    RoomPlayerId = roomPlayerId,
                    Bet = 0,
                }
            );
            await context.SaveChangesAsync();
            var repo = new HandRepository(context);
            Hand res = await repo.PatchHandAsync(handId, Order: 3, Bet: 200);

            // Assert
            Assert.NotNull(res);
            Assert.Equal(3, res.Order);
            Assert.Equal(roomPlayerId, res.RoomPlayerId);
            Assert.Equal(200, res.Bet);
        }

        [Fact]
        public async Task PatchHandAsync_InvalidHandId_ThrowsKeyNotFoundException()
        {
            // Arrange
            var options = GetInMemoryDbOptions();

            using var context = new AppDbContext(options);
            var repo = new HandRepository(context);

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            {
                await repo.PatchHandAsync(Guid.NewGuid(), Order: 2);
            });
        }

        [Fact]
        public async Task DeleteHandAsync_ValidHandId_ReturnsDeletedHand()
        {
            // Arrange
            var options = GetInMemoryDbOptions();

            var handId = Guid.NewGuid();
            var roomPlayerId = Guid.NewGuid();

            using var context = new AppDbContext(options);

            // Act
            context.Hands.Add(
                new Hand
                {
                    Id = handId,
                    Order = 1,
                    RoomPlayerId = roomPlayerId,
                    Bet = 0,
                }
            );
            await context.SaveChangesAsync();
            var repo = new HandRepository(context);
            Hand res = await repo.DeleteHandAsync(handId);

            // Assert
            Assert.NotNull(res);
            Assert.Equal(handId, res.Id);
            await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            {
                await repo.DeleteHandAsync(Guid.NewGuid());
            });
        }

        [Fact]
        public async Task DeleteHandAsync_InvalidHandId_ThrowsKeyNotFoundException()
        {
            // Arrange
            var options = GetInMemoryDbOptions();

            using var context = new AppDbContext(options);
            var repo = new HandRepository(context);

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            {
                await repo.DeleteHandAsync(Guid.NewGuid());
            });
        }
    }
};
