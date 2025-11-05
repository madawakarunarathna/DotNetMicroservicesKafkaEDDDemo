using System.ComponentModel.DataAnnotations;

namespace UserService.Application.Exceptions
{
    public sealed class DuplicateEmailException : ValidationException
    {
        public DuplicateEmailException(string email)
            : base($"A user with email '{email}' already exists.") { }
    }
}
