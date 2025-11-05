using OrderService.Dtos.Requests;
using OrderService.Dtos.Responses;

namespace OrderService.Application.Services
{
    public interface IOrderService
    {
        Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct);
        Task<List<OrderResponse>> GetAllOrdersAsync(CancellationToken ct);
    }
}
