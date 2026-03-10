using System.ComponentModel.DataAnnotations;

namespace LMS.Models
{
    public enum LessonType
    {
        Video,
        Text,
        Document
    }

    public class Lesson
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Content { get; set; }
        public string? VideoUrl { get; set; }
        public int Order { get; set; }
        public int DurationMinutes { get; set; }
        public LessonType Type { get; set; } = LessonType.Text;

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public ICollection<LessonProgress> Progresses { get; set; } = new List<LessonProgress>();
    }

    public class LessonProgress
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
        public int LessonId { get; set; }
        public Lesson? Lesson { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
