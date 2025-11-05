namespace OrderService.Dtos.Responses
{
    public class OrderResponse
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Product { get; set; } = default!;
        public int Quantity { get; set; } = default!;
        public decimal Price { get; set; }
    }
}
