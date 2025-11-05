using System.ComponentModel.DataAnnotations;

namespace UserService.Dtos.Requests
{
    public class CreateUserRequest
    {
        [Required(ErrorMessage = "Name is required.")]
        public string Name { get; set; } = default!;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = default!;
    }
}
