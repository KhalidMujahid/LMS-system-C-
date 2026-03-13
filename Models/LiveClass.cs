using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    public class LiveClass
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public int CourseId { get; set; }

        [ForeignKey("CourseId")]
        public Course? Course { get; set; }

        public string? InstructorId { get; set; }

        [ForeignKey("InstructorId")]
        public ApplicationUser? Instructor { get; set; }

        [Required]
        public DateTime ScheduledAt { get; set; }

        public int DurationMinutes { get; set; } = 60;

        [Required]
        public string MeetingUrl { get; set; } = string.Empty;

        public string? MeetingId { get; set; }

        public string? MeetingPassword { get; set; }

        public LiveClassStatus Status { get; set; } = LiveClassStatus.Scheduled;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? StartedAt { get; set; }

        public DateTime? EndedAt { get; set; }

        public bool IsRecordingEnabled { get; set; } = true;
    }

    public enum LiveClassStatus
    {
        Scheduled,
        Live,
        Completed,
        Cancelled
    }
}
