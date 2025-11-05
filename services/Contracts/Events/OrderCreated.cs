namespace Contracts.Events
{
    public class OrderCreated
    {
        public Guid OrderId { get; set; }
        public Guid UserId { get; set; }
        public decimal TotalAmount { get; set; }
        //public DateTime CreatedAtUtc { get; set; }
    }
}
