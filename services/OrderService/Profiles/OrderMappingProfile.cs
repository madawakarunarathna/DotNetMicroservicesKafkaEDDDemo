using AutoMapper;
using OrderService.Domain;
using OrderService.Dtos.Requests;
using OrderService.Dtos.Responses;

namespace UserService.Profiles
{
    public class OrderMappingProfile : Profile
    {
        public OrderMappingProfile()
        {
            // From API input to domain entity
            CreateMap<CreateOrderRequest, Order>();

            // From domain entity to API output
            CreateMap<Order, OrderResponse>();
        }
    }
}
