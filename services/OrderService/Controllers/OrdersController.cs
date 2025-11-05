using Microsoft.AspNetCore.Mvc;
using OrderService.Application.Services;
using OrderService.Dtos.Requests;

namespace OrderService.Controllers
{
    /// <summary>
    /// API endpoints for order management.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        /// <summary>
        /// Creates a new order.
        /// </summary>
        /// <param name="request">The order creation request.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Returns the created order with HTTP 201 Created.</returns>
        [HttpPost]
        public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken ct)
        {
            var created = await _orderService.CreateOrderAsync(request, ct);

            return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
        }

        /// <summary>
        /// Retrieves all orders.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of orders.</returns>
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var users = await _orderService.GetAllOrdersAsync(ct);
            return Ok(users);
        }
    }
}
