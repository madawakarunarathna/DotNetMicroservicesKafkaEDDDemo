namespace OrderService.Domain
{
    public class Order
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Product { get; set; } = default!;
        public int Quantity { get; set; } = default!;
        public decimal Price { get; set; }


    }
}
