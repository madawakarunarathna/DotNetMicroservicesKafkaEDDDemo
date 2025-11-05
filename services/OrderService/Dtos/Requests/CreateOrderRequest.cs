using System.ComponentModel.DataAnnotations;

namespace OrderService.Dtos.Requests
{
    public class CreateOrderRequest
    {
        [Required]
        public Guid UserId { get; set; }

        [Required, StringLength(100)]
        public string Product { get; set; } = default!;

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; } = default!;

        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0.")]
        public decimal Price { get; set; }
    }
}
