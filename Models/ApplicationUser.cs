using Microsoft.AspNetCore.Identity;

namespace LMS.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? ProfilePicture { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        public string FullName => $"{FirstName} {LastName}";

        public ICollection<Course> CoursesCreated { get; set; } = new List<Course>();
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();
        public ICollection<LessonProgress> LessonProgresses { get; set; } = new List<LessonProgress>();
    }
}
