using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Contracts.Events;
using Contracts.Messaging.Configuration;
using Contracts.Messaging.Producers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using System.ComponentModel.DataAnnotations;
using UserService.Application.Exceptions;
using UserService.Application.Services;
using UserService.Domain;
using UserService.Dtos.Requests;
using UserService.Dtos.Responses;
using UserService.Persistence;
using Xunit;
using UserService.Application.CQRS.Commands.CreateUser;

namespace UserService.Tests
{
    public class UserServiceTests
    {
        private static AutoMapper.IMapper CreateMapperMock()
        {
            var mapperMock = new Mock<AutoMapper.IMapper>();

            // Map CreateUserRequest -> User
            mapperMock.Setup(m => m.Map<User>(It.IsAny<CreateUserRequest>()))
                .Returns((CreateUserRequest r) => new User { Name = r.Name, Email = r.Email });

            // Map User -> UserResponse
            mapperMock.Setup(m => m.Map<UserResponse>(It.IsAny<User>()))
                .Returns((User u) => new UserResponse { Id = u.Id, Name = u.Name, Email = u.Email });

            // Map list (used by GetAll)
            mapperMock.Setup(m => m.Map<System.Collections.Generic.List<UserResponse>>(It.IsAny<System.Collections.Generic.List<User>>() ))
                .Returns((System.Collections.Generic.List<User> users) => users.Select(u => new UserResponse { Id = u.Id, Name = u.Name, Email = u.Email }).ToList());

            return mapperMock.Object;
        }

        private static UserDbContext CreateDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<UserDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            return new UserDbContext(options);
        }

        [Fact]
        public async Task CreateUserAsync_Should_SaveUser_And_ProduceEvent()
        {
            // Arrange
            var db = CreateDbContext(Guid.NewGuid().ToString());

            var producerMock = new Mock<IEventProducer>();
            var validatorMock = new Mock<ICreateUserCommandValidator>();
            validatorMock
                .Setup(v => v.ValidateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CreateUserRequest r, CancellationToken ct) => r.Email.Trim().ToLowerInvariant());

            var kafka = Options.Create(new KafkaOptions { Topics = new KafkaTopics { Users = "users.events" } });
            var mapper = CreateMapperMock();

            var sut = new global::UserService.Application.Services.UserService(db, producerMock.Object, kafka, mapper, validatorMock.Object);

            var request = new CreateUserRequest { Name = "Alice", Email = "Alice@example.com" };

            // Act
            var result = await sut.CreateUserAsync(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be(request.Name);
            result.Email.Should().Be(request.Email);
            result.Id.Should().NotBe(Guid.Empty);

            // DB persisted
            var userInDb = await db.Users.FindAsync(result.Id);
            userInDb.Should().NotBeNull();
            userInDb!.Name.Should().Be(request.Name);

            // Validator was called
            validatorMock.Verify(v => v.ValidateUserAsync(It.Is<CreateUserRequest>(r => r == request), It.IsAny<CancellationToken>()), Times.Once);

            // Producer was called with expected topic and key
            producerMock.Verify(p => p.ProduceAsync(
                It.Is<string>(t => t == "users.events"),
                It.Is<string>(k => k == result.Id.ToString()),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAllUsersAsync_Should_Return_AllMappedUsers()
        {
            // Arrange
            var db = CreateDbContext(Guid.NewGuid().ToString());
            db.Users.AddRange(
                new User { Id = Guid.NewGuid(), Name = "A", Email = "a@example.com" },
                new User { Id = Guid.NewGuid(), Name = "B", Email = "b@example.com" }
            );
            await db.SaveChangesAsync();

            var producerMock = new Mock<IEventProducer>();
            var validatorMock = new Mock<ICreateUserCommandValidator>();
            var kafka = Options.Create(new KafkaOptions { Topics = new KafkaTopics { Users = "users.events" } });
            var mapper = CreateMapperMock();

            var sut = new global::UserService.Application.Services.UserService(db, producerMock.Object, kafka, mapper, validatorMock.Object);

            // Act
            var list = await sut.GetAllUsersAsync(CancellationToken.None);

            // Assert
            list.Should().HaveCount(2);
            list.Select(r => r.Email).Should().Contain(new[] { "a@example.com", "b@example.com" });
        }

        [Fact]
        public async Task GetUserByIdAsync_Should_Return_User_Or_Null()
        {
            // Arrange
            var db = CreateDbContext(Guid.NewGuid().ToString());
            var existing = new User { Id = Guid.NewGuid(), Name = "Exists", Email = "exists@example.com" };
            db.Users.Add(existing);
            await db.SaveChangesAsync();

            var producerMock = new Mock<IEventProducer>();
            var validatorMock = new Mock<ICreateUserCommandValidator>();
            var kafka = Options.Create(new KafkaOptions { Topics = new KafkaTopics { Users = "users.events" } });
            var mapper = CreateMapperMock();

            var sut = new global::UserService.Application.Services.UserService(db, producerMock.Object, kafka, mapper, validatorMock.Object);

            // Act
            var found = await sut.GetUserByIdAsync(existing.Id, CancellationToken.None);
            var notFound = await sut.GetUserByIdAsync(Guid.NewGuid(), CancellationToken.None);

            // Assert
            found.Should().NotBeNull();
            found!.Id.Should().Be(existing.Id);

            notFound.Should().BeNull();
        }

        [Fact]
        public async Task CreateUserAsync_When_ProducerThrows_Should_SurfaceException_And_UserPersisted()
        {
            // Arrange
            var db = CreateDbContext(Guid.NewGuid().ToString());

            var producerMock = new Mock<IEventProducer>();
            // Simulate broker unavailable
            producerMock
                .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Broker unavailable"));

            var validatorMock = new Mock<ICreateUserCommandValidator>();
            validatorMock
                .Setup(v => v.ValidateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CreateUserRequest r, CancellationToken ct) => r.Email.Trim().ToLowerInvariant());

            var kafka = Options.Create(new KafkaOptions { Topics = new KafkaTopics { Users = "users.events" } });
            var mapper = CreateMapperMock();

            var sut = new global::UserService.Application.Services.UserService(db, producerMock.Object, kafka, mapper, validatorMock.Object);

            var request = new CreateUserRequest { Name = "Bob", Email = "bob@example.com" };

            // Act & Assert: exception should be surfaced
            await Assert.ThrowsAsync<Exception>(async () => await sut.CreateUserAsync(request, CancellationToken.None));

            // DB persisted despite publish failure
            var users = await db.Users.AsNoTracking().ToListAsync();
            users.Should().HaveCount(1);
            users.First().Email.Should().Be(request.Email);

            // Producer was called exactly once
            producerMock.Verify(p => p.ProduceAsync(
                It.Is<string>(t => t == "users.events"),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()), Times.Once);

            // Validator was called once
            validatorMock.Verify(v => v.ValidateUserAsync(It.Is<CreateUserRequest>(r => r == request), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateUserAsync_When_ValidatorThrows_Should_NotPersist_And_NoPublish()
        {
            // Arrange
            var db = CreateDbContext(Guid.NewGuid().ToString());

            var producerMock = new Mock<IEventProducer>();
            var validatorMock = new Mock<ICreateUserCommandValidator>();
            validatorMock
                .Setup(v => v.ValidateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DuplicateEmailException("dup@example.com"));

            var kafka = Options.Create(new KafkaOptions { Topics = new KafkaTopics { Users = "users.events" } });
            var mapper = CreateMapperMock();

            var sut = new global::UserService.Application.Services.UserService(db, producerMock.Object, kafka, mapper, validatorMock.Object);

            var request = new CreateUserRequest { Name = "Dup", Email = "dup@example.com" };

            // Act & Assert
            await Assert.ThrowsAsync<DuplicateEmailException>(async () => await sut.CreateUserAsync(request, CancellationToken.None));

            // No DB record
            var users = await db.Users.AsNoTracking().ToListAsync();
            users.Should().BeEmpty();

            // Producer never called
            producerMock.Verify(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateUserAsync_When_SaveChangesIsCancelled_Should_NotPublish_And_Cancelled()
        {
            // Arrange
            var db = CreateDbContext(Guid.NewGuid().ToString());

            var producerMock = new Mock<IEventProducer>();
            var validatorMock = new Mock<ICreateUserCommandValidator>();
            validatorMock
                .Setup(v => v.ValidateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CreateUserRequest r, CancellationToken ct) => r.Email.Trim().ToLowerInvariant());

            var kafka = Options.Create(new KafkaOptions { Topics = new KafkaTopics { Users = "users.events" } });
            var mapper = CreateMapperMock();

            var sut = new global::UserService.Application.Services.UserService(db, producerMock.Object, kafka, mapper, validatorMock.Object);

            var request = new CreateUserRequest { Name = "Cancel", Email = "cancel@example.com" };

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // pre-cancel to simulate SaveChangesAsync cancellation

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await sut.CreateUserAsync(request, cts.Token));

            // Ensure no publish
            producerMock.Verify(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);

            // And no DB persisted
            var users = await db.Users.AsNoTracking().ToListAsync();
            users.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateUserAsync_EventPayload_ShouldContain_CorrectUserCreated()
        {
            // Arrange
            var db = CreateDbContext(Guid.NewGuid().ToString());

            UserCreated? captured = null;
            var producerMock = new Mock<IEventProducer>();
            producerMock
                .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, object, CancellationToken>((t, k, v, ct) => captured = v as UserCreated)
                .Returns(Task.CompletedTask);

            var validatorMock = new Mock<ICreateUserCommandValidator>();
            validatorMock
                .Setup(v => v.ValidateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CreateUserRequest r, CancellationToken ct) => r.Email.Trim().ToLowerInvariant());

            var kafka = Options.Create(new KafkaOptions { Topics = new KafkaTopics { Users = "users.events" } });
            var mapper = CreateMapperMock();

            var sut = new global::UserService.Application.Services.UserService(db, producerMock.Object, kafka, mapper, validatorMock.Object);

            var request = new CreateUserRequest { Name = "Eventy", Email = "eventy@example.com" };

            // Act
            var result = await sut.CreateUserAsync(request, CancellationToken.None);

            // Assert
            captured.Should().NotBeNull();
            captured!.UserId.Should().Be(result.Id);
            captured.UserName.Should().Be(result.Name);
            captured.Email.Should().Be(result.Email);

            // DB persisted
            var user = await db.Users.FindAsync(result.Id);
            user.Should().NotBeNull();
        }

        [Fact]
        public async Task CreateUserAsync_When_MapperThrows_On_MapToUser_Should_NotPersist_And_NoPublish()
        {
            // Arrange
            var db = CreateDbContext(Guid.NewGuid().ToString());

            var mapperMock = new Mock<AutoMapper.IMapper>();
            mapperMock.Setup(m => m.Map<User>(It.IsAny<CreateUserRequest>())).Throws(new Exception("map to user fail"));

            var producerMock = new Mock<IEventProducer>();
            var validatorMock = new Mock<ICreateUserCommandValidator>();
            validatorMock
                .Setup(v => v.ValidateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CreateUserRequest r, CancellationToken ct) => r.Email.Trim().ToLowerInvariant());

            var kafka = Options.Create(new KafkaOptions { Topics = new KafkaTopics { Users = "users.events" } });

            var sut = new global::UserService.Application.Services.UserService(db, producerMock.Object, kafka, mapperMock.Object, validatorMock.Object);

            var request = new CreateUserRequest { Name = "MapFail", Email = "mapfail@example.com" };

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(async () => await sut.CreateUserAsync(request, CancellationToken.None));

            // Nothing persisted
            var users = await db.Users.AsNoTracking().ToListAsync();
            users.Should().BeEmpty();

            // Producer never called
            producerMock.Verify(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateUserAsync_When_MapperThrows_On_MapToResponse_Should_Persist_And_Publish_But_Throw()
        {
            // Arrange
            var db = CreateDbContext(Guid.NewGuid().ToString());

            var mapperMock = new Mock<AutoMapper.IMapper>();
            // mapping to domain works
            mapperMock.Setup(m => m.Map<User>(It.IsAny<CreateUserRequest>())).Returns((CreateUserRequest r) => new User { Name = r.Name, Email = r.Email });
            // mapping to response fails
            mapperMock.Setup(m => m.Map<UserResponse>(It.IsAny<User>())).Throws(new Exception("map to response fail"));

            var producerMock = new Mock<IEventProducer>();
            producerMock.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var validatorMock = new Mock<ICreateUserCommandValidator>();
            validatorMock
                .Setup(v => v.ValidateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CreateUserRequest r, CancellationToken ct) => r.Email.Trim().ToLowerInvariant());

            var kafka = Options.Create(new KafkaOptions { Topics = new KafkaTopics { Users = "users.events" } });

            var sut = new global::UserService.Application.Services.UserService(db, producerMock.Object, kafka, mapperMock.Object, validatorMock.Object);

            var request = new CreateUserRequest { Name = "MapRespFail", Email = "mapresp@example.com" };

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(async () => await sut.CreateUserAsync(request, CancellationToken.None));

            // DB persisted
            var users = await db.Users.AsNoTracking().ToListAsync();
            users.Should().HaveCount(1);

            // Producer called once
            producerMock.Verify(p => p.ProduceAsync(It.Is<string>(t => t == "users.events"), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAllUsersAsync_When_NoUsers_Returns_EmptyList()
        {
            // Arrange
            var db = CreateDbContext(Guid.NewGuid().ToString());

            var producerMock = new Mock<IEventProducer>();
            var validatorMock = new Mock<ICreateUserCommandValidator>();
            var kafka = Options.Create(new KafkaOptions { Topics = new KafkaTopics { Users = "users.events" } });
            var mapper = CreateMapperMock();

            var sut = new global::UserService.Application.Services.UserService(db, producerMock.Object, kafka, mapper, validatorMock.Object);

            // Act
            var list = await sut.GetAllUsersAsync(CancellationToken.None);

            // Assert
            list.Should().NotBeNull();
            list.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateUserAsync_When_InputHasWhitespaceOrInvalid_ValidatorBlocks_NoSideEffects()
        {
            // Arrange
            var db = CreateDbContext(Guid.NewGuid().ToString());

            var producerMock = new Mock<IEventProducer>();
            var validatorMock = new Mock<ICreateUserCommandValidator>();

            validatorMock
                .Setup(v => v.ValidateUserAsync(It.Is<CreateUserRequest>(r => string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Email)), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ValidationException("Invalid input"));

            // For other inputs, return normalized
            validatorMock
                .Setup(v => v.ValidateUserAsync(It.Is<CreateUserRequest>(r => !string.IsNullOrWhiteSpace(r.Name) && !string.IsNullOrWhiteSpace(r.Email)), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CreateUserRequest r, CancellationToken ct) => r.Email.Trim().ToLowerInvariant());

            var kafka = Options.Create(new KafkaOptions { Topics = new KafkaTopics { Users = "users.events" } });
            var mapper = CreateMapperMock();

            var sut = new global::UserService.Application.Services.UserService(db, producerMock.Object, kafka, mapper, validatorMock.Object);

            var badRequest = new CreateUserRequest { Name = "   ", Email = "   " };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(async () => await sut.CreateUserAsync(badRequest, CancellationToken.None));

            // No DB write
            var users = await db.Users.AsNoTracking().ToListAsync();
            users.Should().BeEmpty();

            // No publish
            producerMock.Verify(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
