using AutoMapper;
using UserService.Domain;
using UserService.Dtos.Requests;
using UserService.Dtos.Responses;

namespace UserService.Profiles
{
    public class UserMappingProfile : Profile
    {
        public UserMappingProfile()
        {
            // From API input to domain entity
            CreateMap<CreateUserRequest, User>();

            // From domain entity to API output
            CreateMap<User, UserResponse>();
        }
    }
}
