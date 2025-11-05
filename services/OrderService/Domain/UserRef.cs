namespace OrderService.Domain
{
    public class UserRef
    {
        public Guid Id { get; set; }        // from UserCreated event
        public string Email { get; set; } = default!;
    }
}
