namespace LMS.Models
{
    public enum NotificationType
    {
        Enrollment,
        CourseCompleted,
        CertificateIssued,
        CoursePublished,
        NewCourse,
        QuizResult,
        LiveClass,
        System
    }
    
    public class Notification
    {
        public int Id { get; set; }
        
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
        
        public NotificationType Type { get; set; }
        
        public string Title { get; set; } = string.Empty;
        
        public string Message { get; set; } = string.Empty;
        
        public string? Link { get; set; }
        
        public bool IsRead { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
