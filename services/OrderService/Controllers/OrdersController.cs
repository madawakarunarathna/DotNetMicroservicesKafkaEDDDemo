using Microsoft.AspNetCore.Mvc;
using OrderService.Application.Services;
using OrderService.Dtos.Requests;

namespace OrderService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        //create post endpoint to create order
        [HttpPost]
        public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken ct)
        {
            var created = await _orderService.CreateOrderAsync(request, ct);

            return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
        }

        //create get endpoint to get all orders
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var users = await _orderService.GetAllOrdersAsync(ct);
            return Ok(users);
        }
    }
}
