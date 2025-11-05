using AutoMapper;
using Contracts.Events;
using Contracts.Messaging.Configuration;
using Contracts.Messaging.Producers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UserService.Domain;
using UserService.Dtos.Requests;
using UserService.Dtos.Responses;
using UserService.Persistence;
using UserService.Validation;

namespace UserService.Application.Services
{
    public class UserService : IUserService
    {
        private readonly UserDbContext _db;
        private readonly IEventProducer _producer;
        private readonly KafkaOptions _kafka;
        private readonly IMapper _mapper;
        private readonly IUserValidator _userValidator;

        public UserService(UserDbContext db, IEventProducer producer, IOptions<KafkaOptions> kafka, IMapper mapper, IUserValidator userValidator)
        {
            _db = db;
            _producer = producer;
            _kafka = kafka.Value;
            _mapper = mapper;
            _userValidator = userValidator;
        }


        public async Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken ct)
        {
            await _userValidator.ValidateUserAsync(request, ct);

            var user = _mapper.Map<User>(request);
            user.Id = Guid.NewGuid();

            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);

            // Publish event
            var evt = new UserCreated
            {
                UserId = user.Id,
                UserName = user.Name,
                Email = user.Email
            };

            await _producer.ProduceAsync(_kafka.Topics.Users, user.Id.ToString(), evt, ct);

            // Map domain → output DTO
            return _mapper.Map<UserResponse>(user);
        }

        public async Task<List<UserResponse>> GetAllUsersAsync(CancellationToken ct)
        {
            var users = await _db.Users.AsNoTracking().ToListAsync(ct);
            return _mapper.Map<List<UserResponse>>(users);
        }

        public async Task<UserResponse?> GetUserByIdAsync(Guid id, CancellationToken ct)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
            return user is null ? null : _mapper.Map<UserResponse>(user);
        }
    }
}
