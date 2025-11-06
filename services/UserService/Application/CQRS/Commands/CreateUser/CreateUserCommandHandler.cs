using AutoMapper;
using Contracts.Messaging.Configuration;
using Contracts.Messaging.Producers;
using MediatR;
using Microsoft.Extensions.Options;
using UserService.Dtos.Responses;
using UserService.Persistence;

namespace UserService.Application.CQRS.Commands.CreateUser
{
    /// <summary>
    /// Handles the creation of a new user.
    /// </summary>
    public class CreateUserCommandHandler(UserDbContext dbContext, IEventProducer producer, ICreateUserCommandValidator userValidator, IOptions<KafkaOptions> kafka, IMapper mapper, ILogger<CreateUserCommandHandler> logger) : IRequestHandler<CreateUserCommand, UserResponse>
    {
        private readonly UserDbContext _dbContext = dbContext;
        private readonly IEventProducer _producer = producer;
        private readonly ICreateUserCommandValidator _userValidator = userValidator;
        private readonly KafkaOptions _kafka = kafka.Value;
        private readonly IMapper _mapper = mapper;
        private readonly ILogger<CreateUserCommandHandler> _logger = logger;

        /// <summary>
        /// Handles the CreateUserCommand and returns a UserResponse.
        /// </summary>
        /// <param name="request">The command containing user details.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A UserResponse representing the created user.</returns>
        public async Task<UserResponse> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await _userValidator.ValidateUserAsync(request, cancellationToken);

            var user = _mapper.Map<Domain.User>(request);
            user.Id = Guid.NewGuid();
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var evt = new Contracts.Events.UserCreated
            {
                UserId = user.Id,
                UserName = user.Name,
                Email = user.Email
            };
            await _producer.ProduceAsync(_kafka.Topics.Users, user.Id.ToString(), evt, cancellationToken);

            return _mapper.Map<UserResponse>(user);
        }
    }
}
