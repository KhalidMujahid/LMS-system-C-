using System.ComponentModel.DataAnnotations;

namespace LMS.Models
{
    public class Quiz
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }
        public int PassingScore { get; set; } = 70;
        public int TimeLimitMinutes { get; set; } = 30;

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public ICollection<Question> Questions { get; set; } = new List<Question>();
        public ICollection<QuizAttempt> Attempts { get; set; } = new List<QuizAttempt>();
    }

    public class Question
    {
        public int Id { get; set; }

        [Required]
        public string Text { get; set; } = string.Empty;

        public int Points { get; set; } = 1;
        public int Order { get; set; }

        public int QuizId { get; set; }
        public Quiz? Quiz { get; set; }

        public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    }

    public class Answer
    {
        public int Id { get; set; }

        [Required]
        public string Text { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }

        public int QuestionId { get; set; }
        public Question? Question { get; set; }
    }

    public class QuizAttempt
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
        public int QuizId { get; set; }
        public Quiz? Quiz { get; set; }
        public int Score { get; set; }
        public bool Passed { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public ICollection<StudentAnswer> StudentAnswers { get; set; } = new List<StudentAnswer>();
    }

    public class StudentAnswer
    {
        public int Id { get; set; }
        public int QuizAttemptId { get; set; }
        public QuizAttempt? QuizAttempt { get; set; }
        public int QuestionId { get; set; }
        public Question? Question { get; set; }
        public int AnswerId { get; set; }
        public Answer? Answer { get; set; }
    }
}
