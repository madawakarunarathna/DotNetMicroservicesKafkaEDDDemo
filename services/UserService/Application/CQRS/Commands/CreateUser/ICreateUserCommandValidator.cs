namespace UserService.Application.CQRS.Commands.CreateUser
{
    /// <summary>
    /// Provides validation logic for user-related operations.
    /// </summary>
    public interface ICreateUserCommandValidator
    {
        /// <summary>
        /// Validates a create-user request. Returns the normalized email if valid; throws otherwise.
        /// </summary>
        Task<string> ValidateUserAsync(CreateUserCommand request, CancellationToken ct);
    }
}
