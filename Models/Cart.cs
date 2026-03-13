namespace LMS.Models
{
    public class Cart
    {
        public int Id { get; set; }
        
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
        
        public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public decimal TotalAmount => Items.Sum(i => i.Course?.Price ?? 0);
        public decimal TotalAmountInNaira => TotalAmount / 100;
        
        public int ItemCount => Items.Count;
    }

    public class CartItem
    {
        public int Id { get; set; }
        
        public int CartId { get; set; }
        public Cart? Cart { get; set; }
        
        public int CourseId { get; set; }
        public Course? Course { get; set; }
        
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
