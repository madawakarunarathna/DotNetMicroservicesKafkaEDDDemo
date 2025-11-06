using Microsoft.AspNetCore.Mvc;
using UserService.Application.CQRS.Commands.CreateUser;
using UserService.Application.Services;

namespace UserService.Controllers
{
    /// <summary>
    /// API endpoints for user management.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Creates a new user.
        /// </summary>
        /// <param name="request">The user creation request containing name and email.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Returns the created user with HTTP 201 Created.</returns>
        [HttpPost]
        public async Task<IActionResult> Create(CreateUserCommand request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var created = await _userService.CreateUserAsync(request, ct);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        /// <summary>
        /// Retrieves all users.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of users.</returns>
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var users = await _userService.GetAllUsersAsync(ct);
            return Ok(users);
        }

        /// <summary>
        /// Retrieves a user by identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the user.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The user if found; otherwise HTTP 404 Not Found.</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var user = await _userService.GetUserByIdAsync(id, ct);
            return user is null ? NotFound() : Ok(user);
        }
    }
}
