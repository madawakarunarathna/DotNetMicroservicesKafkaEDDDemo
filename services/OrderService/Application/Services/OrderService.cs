using AutoMapper;
using Contracts.Events;
using Contracts.Messaging.Configuration;
using Contracts.Messaging.Producers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderService.Domain;
using OrderService.Dtos.Requests;
using OrderService.Dtos.Responses;
using OrderService.Persistence;
using System.ComponentModel.DataAnnotations;

namespace OrderService.Application.Services
{
    public class OrderService : IOrderService
    {
        private readonly OrderDbContext _db;
        private readonly IEventProducer _producer;
        private readonly KafkaOptions _kafka;
        private readonly IMapper _mapper;

        public OrderService(OrderDbContext db, IEventProducer producer, IOptions<KafkaOptions> kafka, IMapper mapper)
        {
            _db = db;
            _producer = producer;
            _kafka = kafka.Value;
            _mapper = mapper;
        }

        public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct)
        {
            var userExists = await _db.Users.AnyAsync(u => u.Id == request.UserId, ct);
            if (!userExists)
                throw new ValidationException("User does not exist.");

            var order = _mapper.Map<Order>(request);
            order.Id = Guid.NewGuid();

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            var evt = new OrderCreated
            {
                OrderId = order.Id,
                UserId = order.UserId,
                TotalAmount = order.Price
            };

            await _producer.ProduceAsync(_kafka.Topics.Orders, order.Id.ToString(), evt, ct);

            // Map domain → output DTO
            return _mapper.Map<OrderResponse>(order);
        }

        public async Task<List<OrderResponse>> GetAllOrdersAsync(CancellationToken ct)
        {
            var orders = await _db.Orders.AsNoTracking().ToListAsync(ct);
            return _mapper.Map<List<OrderResponse>>(orders);
        }
    }
}
