using UserService.Dtos.Requests;

namespace UserService.Validation
{
    public interface IUserValidator
    {
        /// <summary>
        /// Validates a create-user request. Returns the normalized email if valid; throws otherwise.
        /// </summary>
        Task<string> ValidateUserAsync(CreateUserRequest request, CancellationToken ct);
    }
}
