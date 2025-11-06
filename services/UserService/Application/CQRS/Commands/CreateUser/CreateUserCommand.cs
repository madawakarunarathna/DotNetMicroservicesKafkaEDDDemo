using MediatR;
using System.ComponentModel.DataAnnotations;
using UserService.Dtos.Responses;

namespace UserService.Application.CQRS.Commands.CreateUser
{
    /// <summary>
    /// Command to create a new user with the specified name and email.
    /// </summary>
    public class CreateUserCommand : IRequest<UserResponse>
    {
        /// <summary>
        /// Gets or sets the name of the user.
        /// </summary>
        [Required(ErrorMessage = "Name is required.")]
        public string Name { get; set; } = default!;

        /// <summary>
        /// Gets or sets the email address of the user.
        /// </summary>
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = default!;
    }
}
