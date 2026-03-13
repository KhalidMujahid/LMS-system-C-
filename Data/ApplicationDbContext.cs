using LMS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LMS.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Course> Courses { get; set; }
        public DbSet<Lesson> Lessons { get; set; }
        public DbSet<LessonProgress> LessonProgresses { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<Quiz> Quizzes { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<Answer> Answers { get; set; }
        public DbSet<QuizAttempt> QuizAttempts { get; set; }
        public DbSet<StudentAnswer> StudentAnswers { get; set; }
        
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<PaymentItem> PaymentItems { get; set; }
        
        public DbSet<Certificate> Certificates { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        
        public DbSet<LiveClass> LiveClasses { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Enrollment>()
                .HasIndex(e => new { e.UserId, e.CourseId })
                .IsUnique();

            builder.Entity<LessonProgress>()
                .HasIndex(lp => new { lp.UserId, lp.LessonId })
                .IsUnique();

            builder.Entity<Course>()
                .HasOne(c => c.Instructor)
                .WithMany(u => u.CoursesCreated)
                .HasForeignKey(c => c.InstructorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Enrollment>()
                .HasOne(e => e.User)
                .WithMany(u => u.Enrollments)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<QuizAttempt>()
                .HasOne(qa => qa.User)
                .WithMany(u => u.QuizAttempts)
                .HasForeignKey(qa => qa.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<LessonProgress>()
                .HasOne(lp => lp.User)
                .WithMany(u => u.LessonProgresses)
                .HasForeignKey(lp => lp.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            builder.Entity<Cart>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            builder.Entity<CartItem>()
                .HasOne(ci => ci.Cart)
                .WithMany(c => c.Items)
                .HasForeignKey(ci => ci.CartId)
                .OnDelete(DeleteBehavior.Cascade);
                
            builder.Entity<CartItem>()
                .HasOne(ci => ci.Course)
                .WithMany(c => c.CartItems)
                .HasForeignKey(ci => ci.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
                
            builder.Entity<CartItem>()
                .HasIndex(ci => new { ci.CartId, ci.CourseId })
                .IsUnique();
                
            builder.Entity<Payment>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            builder.Entity<Payment>()
                .HasIndex(p => p.Reference)
                .IsUnique();
                
            builder.Entity<PaymentItem>()
                .HasOne(pi => pi.Payment)
                .WithMany(p => p.Items)
                .HasForeignKey(pi => pi.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);
                
            builder.Entity<PaymentItem>()
                .HasOne(pi => pi.Course)
                .WithMany()
                .HasForeignKey(pi => pi.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
