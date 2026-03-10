namespace LMS.Models
{
    public enum EnrollmentStatus
    {
        Active,
        Completed,
        Dropped
    }

    public class Enrollment
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
        public int CourseId { get; set; }
        public Course? Course { get; set; }
        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
        public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
        public int ProgressPercentage { get; set; } = 0;
        public DateTime? CompletedAt { get; set; }
    }
}
