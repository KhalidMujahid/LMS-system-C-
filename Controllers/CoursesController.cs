using LMS.Data;
using LMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Controllers
{
    public class CoursesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CoursesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? search, string? category)
        {
            var query = _context.Courses
                .Include(c => c.Instructor)
                .Include(c => c.Enrollments)
                .Include(c => c.Lessons)
                .Where(c => c.Status == CourseStatus.Published)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(c => c.Title.Contains(search) || c.Description.Contains(search));

            if (!string.IsNullOrEmpty(category))
                query = query.Where(c => c.Category == category);

            var courses = await query.ToListAsync();
            var categories = await _context.Courses
                .Where(c => c.Status == CourseStatus.Published && c.Category != null)
                .Select(c => c.Category!)
                .Distinct()
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.Search = search;
            ViewBag.SelectedCategory = category;

            // Check enrollments if user is logged in
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                var enrolledCourseIds = await _context.Enrollments
                    .Where(e => e.UserId == user!.Id)
                    .Select(e => e.CourseId)
                    .ToListAsync();
                ViewBag.EnrolledCourseIds = enrolledCourseIds;
            }

            return View(courses);
        }

        public async Task<IActionResult> Details(int id)
        {
            var course = await _context.Courses
                .Include(c => c.Instructor)
                .Include(c => c.Lessons)
                .Include(c => c.Quizzes)
                .Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c => c.Id == id && c.Status == CourseStatus.Published);

            if (course == null) return NotFound();

            bool isEnrolled = false;
            Enrollment? enrollment = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                enrollment = await _context.Enrollments
                    .FirstOrDefaultAsync(e => e.UserId == user!.Id && e.CourseId == id);
                isEnrolled = enrollment != null;
            }

            ViewBag.IsEnrolled = isEnrolled;
            ViewBag.Enrollment = enrollment;
            return View(course);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enroll(int courseId)
        {
            var user = await _userManager.GetUserAsync(User);
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound();

            var existing = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.UserId == user!.Id && e.CourseId == courseId);

            if (existing == null)
            {
                _context.Enrollments.Add(new Enrollment
                {
                    UserId = user!.Id,
                    CourseId = courseId
                });
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Successfully enrolled in {course.Title}!";
            }

            return RedirectToAction(nameof(Learn), new { id = courseId });
        }

        [Authorize]
        public async Task<IActionResult> Learn(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var enrollment = await _context.Enrollments
                .Include(e => e.Course)
                    .ThenInclude(c => c!.Lessons)
                .Include(e => e.Course)
                    .ThenInclude(c => c!.Quizzes)
                .FirstOrDefaultAsync(e => e.UserId == user!.Id && e.CourseId == id);

            if (enrollment == null) return RedirectToAction(nameof(Details), new { id });

            var completedLessons = await _context.LessonProgresses
                .Where(lp => lp.UserId == user!.Id && lp.IsCompleted)
                .Select(lp => lp.LessonId)
                .ToListAsync();

            ViewBag.CompletedLessons = completedLessons;
            return View(enrollment);
        }

        [Authorize]
        public async Task<IActionResult> Lesson(int id)
        {
            var lesson = await _context.Lessons
                .Include(l => l.Course)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lesson == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.UserId == user!.Id && e.CourseId == lesson.CourseId);

            if (enrollment == null) return RedirectToAction(nameof(Details), new { id = lesson.CourseId });

            var progress = await _context.LessonProgresses
                .FirstOrDefaultAsync(lp => lp.UserId == user!.Id && lp.LessonId == id);

            ViewBag.IsCompleted = progress?.IsCompleted ?? false;

            var allLessons = await _context.Lessons
                .Where(l => l.CourseId == lesson.CourseId)
                .OrderBy(l => l.Order)
                .ToListAsync();

            var idx = allLessons.FindIndex(l => l.Id == id);
            ViewBag.PreviousLesson = idx > 0 ? allLessons[idx - 1] : null;
            ViewBag.NextLesson = idx < allLessons.Count - 1 ? allLessons[idx + 1] : null;
            ViewBag.AllLessons = allLessons;

            return View(lesson);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteLesson(int lessonId)
        {
            var user = await _userManager.GetUserAsync(User);
            var lesson = await _context.Lessons.FindAsync(lessonId);
            if (lesson == null) return NotFound();

            var progress = await _context.LessonProgresses
                .FirstOrDefaultAsync(lp => lp.UserId == user!.Id && lp.LessonId == lessonId);

            if (progress == null)
            {
                _context.LessonProgresses.Add(new LessonProgress
                {
                    UserId = user!.Id,
                    LessonId = lessonId,
                    IsCompleted = true,
                    CompletedAt = DateTime.UtcNow
                });
            }
            else
            {
                progress.IsCompleted = true;
                progress.CompletedAt = DateTime.UtcNow;
            }

            // Update enrollment progress
            var totalLessons = await _context.Lessons.CountAsync(l => l.CourseId == lesson.CourseId);
            var completedCount = await _context.LessonProgresses
                .CountAsync(lp => lp.UserId == user!.Id && lp.IsCompleted &&
                    _context.Lessons.Any(l => l.Id == lp.LessonId && l.CourseId == lesson.CourseId));

            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.UserId == user!.Id && e.CourseId == lesson.CourseId);

            if (enrollment != null)
            {
                enrollment.ProgressPercentage = totalLessons > 0 ? (completedCount + 1) * 100 / totalLessons : 0;
                if (enrollment.ProgressPercentage >= 100)
                {
                    enrollment.Status = EnrollmentStatus.Completed;
                    enrollment.CompletedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Lesson marked as complete!";
            return RedirectToAction(nameof(Lesson), new { id = lessonId });
        }

        [Authorize]
        public async Task<IActionResult> TakeQuiz(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                    .ThenInclude(q => q.Answers)
                .Include(q => q.Course)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quiz == null) return NotFound();

            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.UserId == user!.Id && e.CourseId == quiz.CourseId);
            if (enrollment == null) return RedirectToAction(nameof(Details), new { id = quiz.CourseId });

            return View(quiz);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitQuiz(int quizId, Dictionary<int, int> answers)
        {
            var user = await _userManager.GetUserAsync(User);
            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                    .ThenInclude(q => q.Answers)
                .FirstOrDefaultAsync(q => q.Id == quizId);

            if (quiz == null) return NotFound();

            var attempt = new QuizAttempt
            {
                UserId = user!.Id,
                QuizId = quizId,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            int totalPoints = 0;
            int earnedPoints = 0;

            foreach (var question in quiz.Questions)
            {
                totalPoints += question.Points;
                if (answers.TryGetValue(question.Id, out int selectedAnswerId))
                {
                    var correctAnswer = question.Answers.FirstOrDefault(a => a.IsCorrect);
                    attempt.StudentAnswers.Add(new StudentAnswer
                    {
                        QuestionId = question.Id,
                        AnswerId = selectedAnswerId
                    });
                    if (correctAnswer != null && correctAnswer.Id == selectedAnswerId)
                        earnedPoints += question.Points;
                }
            }

            attempt.Score = totalPoints > 0 ? earnedPoints * 100 / totalPoints : 0;
            attempt.Passed = attempt.Score >= quiz.PassingScore;

            _context.QuizAttempts.Add(attempt);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(QuizResult), new { id = attempt.Id });
        }

        [Authorize]
        public async Task<IActionResult> QuizResult(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var attempt = await _context.QuizAttempts
                .Include(a => a.Quiz)
                    .ThenInclude(q => q!.Questions)
                        .ThenInclude(q => q.Answers)
                .Include(a => a.StudentAnswers)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == user!.Id);

            if (attempt == null) return NotFound();
            return View(attempt);
        }
    }
}
