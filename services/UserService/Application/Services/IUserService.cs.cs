using UserService.Dtos.Requests;
using UserService.Dtos.Responses;

namespace UserService.Application.Services
{
    public interface IUserService
    {
        Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken ct);
        Task<UserResponse?> GetUserByIdAsync(Guid id, CancellationToken ct);
        Task<List<UserResponse>> GetAllUsersAsync(CancellationToken ct);
    }
}
