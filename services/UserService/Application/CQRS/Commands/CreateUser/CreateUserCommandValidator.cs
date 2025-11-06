using Microsoft.EntityFrameworkCore;
using UserService.Application.Exceptions;
using UserService.Persistence;

namespace UserService.Application.CQRS.Commands.CreateUser
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="dbContext"></param>
    public class CreateUserCommandValidator(UserDbContext dbContext) : ICreateUserCommandValidator
    {
        private readonly UserDbContext _dbContext = dbContext;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="DuplicateEmailException"></exception>
        public async Task<string> ValidateUserAsync(CreateUserCommand request, CancellationToken ct)
        {
            var normalized = NormalizeEmail(request.Email);

            var exists = await _dbContext.Users
                .AsNoTracking()
                .AnyAsync(u => u.Email.ToLower() == normalized, ct);

            if (exists)
                throw new DuplicateEmailException(request.Email);

            return normalized;
        }

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    }
}
