using System.ComponentModel.DataAnnotations;

namespace LMS.Models
{
    public enum CourseStatus
    {
        Draft,
        Published,
        Archived
    }

    public class Course
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public string? ThumbnailUrl { get; set; }

        [MaxLength(100)]
        public string? Category { get; set; }

        // Price in kobo (Nigerian Naira) - Paystack uses kobo
        public decimal Price { get; set; } = 0;

        public CourseStatus Status { get; set; } = CourseStatus.Draft;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public string InstructorId { get; set; } = string.Empty;
        public ApplicationUser? Instructor { get; set; }

        public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

        public int EnrollmentCount => Enrollments?.Count ?? 0;
        public int LessonCount => Lessons?.Count ?? 0;

        // Helper property to display price in Naira
        public decimal PriceInNaira => Price / 100;
    }
}
