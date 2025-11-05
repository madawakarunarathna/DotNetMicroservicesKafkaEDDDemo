using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using UserService.Application.Exceptions;
using UserService.Dtos.Requests;
using UserService.Persistence;

namespace UserService.Validation
{
    public class UserValidator : IUserValidator
    {
        private readonly UserDbContext _db;

        public UserValidator(UserDbContext db) => _db = db;
        public async Task<string> ValidateUserAsync(CreateUserRequest request, CancellationToken ct)
        {
            var normalized = NormalizeEmail(request.Email);

            var exists = await _db.Users
                .AsNoTracking()
                .AnyAsync(u => u.Email.ToLower() == normalized, ct);

            if (exists)
                throw new DuplicateEmailException(request.Email);

            return normalized;
        }

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    }

    
}
