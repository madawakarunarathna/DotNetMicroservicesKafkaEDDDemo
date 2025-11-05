using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using AutoMapper;
using Contracts.Events;
using Contracts.Messaging.Configuration;
using Contracts.Messaging.Producers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using OrderService.Application.Services;
using OrderService.Domain;
using OrderService.Dtos.Requests;
using OrderService.Dtos.Responses;
using OrderService.Persistence;
using UserService.Profiles;
using Xunit;

namespace OrderService.Tests
{
    public class OrderServiceTests
    {
        private static OrderDbContext CreateInMemoryDb(string dbName)
        {
            var options = new DbContextOptionsBuilder<OrderDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            return new OrderDbContext(options);
        }

        private static IMapper CreateMapper()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile<OrderMappingProfile>());
            return config.CreateMapper();
        }

        [Fact]
        public async Task CreateOrderAsync_Succeeds_WhenUserExists()
        {
            // Arrange
            var dbName = nameof(CreateOrderAsync_Succeeds_WhenUserExists) + Guid.NewGuid();
            await using var db = CreateInMemoryDb(dbName);

            var user = new UserRef { Id = Guid.NewGuid(), Email = "test@example.com" };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var producerMock = new Mock<IEventProducer>();
            producerMock.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var kafka = Options.Create(new KafkaOptions());
            var mapper = CreateMapper();
            var service = new OrderService.Application.Services.OrderService(db, producerMock.Object, kafka, mapper);

            var request = new CreateOrderRequest
            {
                UserId = user.Id,
                Product = "Widget",
                Quantity = 2,
                Price = 9.99m
            };

            // Act
            var result = await service.CreateOrderAsync(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().NotBeEmpty();
            result.UserId.Should().Be(user.Id);
            result.Product.Should().Be(request.Product);
            result.Quantity.Should().Be(request.Quantity);
            result.Price.Should().Be(request.Price);

            var persisted = await db.Orders.FindAsync(result.Id);
            persisted.Should().NotBeNull();

            producerMock.Verify(p => p.ProduceAsync(It.Is<string>(t => t == kafka.Value.Topics.Orders),
                It.Is<string>(k => k == result.Id.ToString()),
                It.Is<object>(o => o is OrderCreated && ((OrderCreated)o).OrderId == result.Id && ((OrderCreated)o).UserId == user.Id && ((OrderCreated)o).TotalAmount == request.Price),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateOrderAsync_ThrowsValidationException_WhenUserDoesNotExist()
        {
            // Arrange
            var dbName = nameof(CreateOrderAsync_ThrowsValidationException_WhenUserDoesNotExist) + Guid.NewGuid();
            await using var db = CreateInMemoryDb(dbName);

            var producerMock = new Mock<IEventProducer>();
            var kafka = Options.Create(new KafkaOptions());
            var mapper = CreateMapper();
            var service = new OrderService.Application.Services.OrderService(db, producerMock.Object, kafka, mapper);

            var request = new CreateOrderRequest
            {
                UserId = Guid.NewGuid(),
                Product = "Widget",
                Quantity = 1,
                Price = 1m
            };

            // Act
            Func<Task> act = async () => await service.CreateOrderAsync(request, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ValidationException>().WithMessage("User does not exist.");
            producerMock.Verify(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetAllOrdersAsync_ReturnsAllOrders()
        {
            // Arrange
            var dbName = nameof(GetAllOrdersAsync_ReturnsAllOrders) + Guid.NewGuid();
            await using var db = CreateInMemoryDb(dbName);

            var o1 = new Order { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Product = "A", Quantity = 1, Price = 1m };
            var o2 = new Order { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Product = "B", Quantity = 2, Price = 2m };
            db.Orders.AddRange(o1, o2);
            await db.SaveChangesAsync();

            var producerMock = new Mock<IEventProducer>();
            var kafka = Options.Create(new KafkaOptions());
            var mapper = CreateMapper();
            var service = new OrderService.Application.Services.OrderService(db, producerMock.Object, kafka, mapper);

            // Act
            var results = await service.GetAllOrdersAsync(CancellationToken.None);

            // Assert
            results.Should().HaveCount(2);
            results.Select(r => r.Id).Should().Contain(new[] { o1.Id, o2.Id });
        }

        [Fact]
        public async Task GetAllOrdersAsync_ReturnsEmptyList_WhenNoOrders()
        {
            // Arrange
            var dbName = nameof(GetAllOrdersAsync_ReturnsEmptyList_WhenNoOrders) + Guid.NewGuid();
            await using var db = CreateInMemoryDb(dbName);

            var producerMock = new Mock<IEventProducer>();
            var kafka = Options.Create(new KafkaOptions());
            var mapper = CreateMapper();
            var service = new OrderService.Application.Services.OrderService(db, producerMock.Object, kafka, mapper);

            // Act
            var results = await service.GetAllOrdersAsync(CancellationToken.None);

            // Assert
            results.Should().NotBeNull();
            results.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateOrderAsync_PropagatesProducerException()
        {
            // Arrange
            var dbName = nameof(CreateOrderAsync_PropagatesProducerException) + Guid.NewGuid();
            await using var db = CreateInMemoryDb(dbName);

            var user = new UserRef { Id = Guid.NewGuid(), Email = "ex@ex.com" };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var producerMock = new Mock<IEventProducer>();
            producerMock.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Producer failed"));

            var kafka = Options.Create(new KafkaOptions());
            var mapper = CreateMapper();
            var service = new OrderService.Application.Services.OrderService(db, producerMock.Object, kafka, mapper);

            var request = new CreateOrderRequest
            {
                UserId = user.Id,
                Product = "X",
                Quantity = 1,
                Price = 5m
            };

            // Act
            Func<Task> act = async () => await service.CreateOrderAsync(request, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Producer failed");

            // Ensure order was persisted before producer failed
            var anyOrder = db.Orders.Any();
            anyOrder.Should().BeTrue();
        }
    }
}
