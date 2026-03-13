namespace LMS.Models
{
    public enum PaymentStatus
    {
        Pending,
        Processing,
        Completed,
        Failed,
        Refunded
    }

    public class Payment
    {
        public int Id { get; set; }
        
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
        
        public string Reference { get; set; } = string.Empty;
        
        public decimal Amount { get; set; }
        
        public string Currency { get; set; } = "NGN";
        
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        
        public string? TransactionId { get; set; }
        
        public string? PaymentMethod { get; set; }
        
        public string? Email { get; set; }
        
        public string? Metadata { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? CompletedAt { get; set; }
        
        public ICollection<PaymentItem> Items { get; set; } = new List<PaymentItem>();
        
        public decimal AmountInNaira => Amount / 100;
    }

    public class PaymentItem
    {
        public int Id { get; set; }
        
        public int PaymentId { get; set; }
        public Payment? Payment { get; set; }
        
        public int CourseId { get; set; }
        public Course? Course { get; set; }
        
        public decimal PriceAtPurchase { get; set; }
    }
}
