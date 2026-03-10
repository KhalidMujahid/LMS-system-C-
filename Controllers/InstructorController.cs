using LMS.Data;
using LMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Controllers
{
    [Authorize(Roles = "Instructor,Admin")]
    public class InstructorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public InstructorController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var courses = await _context.Courses
                .Include(c => c.Enrollments)
                .Include(c => c.Lessons)
                .Where(c => c.InstructorId == user!.Id)
                .ToListAsync();

            ViewBag.TotalStudents = courses.SelectMany(c => c.Enrollments).Select(e => e.UserId).Distinct().Count();
            ViewBag.TotalCourses = courses.Count;
            ViewBag.PublishedCourses = courses.Count(c => c.Status == CourseStatus.Published);
            return View(courses);
        }

        public IActionResult CreateCourse() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCourse(Course model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            model.InstructorId = user!.Id;
            model.CreatedAt = DateTime.UtcNow;
            _context.Courses.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Course created successfully.";
            return RedirectToAction(nameof(ManageCourse), new { id = model.Id });
        }

        public async Task<IActionResult> EditCourse(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == id && c.InstructorId == user!.Id);
            if (course == null) return NotFound();
            return View(course);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCourse(Course model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _userManager.GetUserAsync(User);
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == model.Id && c.InstructorId == user!.Id);
            if (course == null) return NotFound();

            course.Title = model.Title;
            course.Description = model.Description;
            course.Category = model.Category;
            course.Status = model.Status;
            course.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Course updated successfully.";
            return RedirectToAction(nameof(ManageCourse), new { id = model.Id });
        }

        public async Task<IActionResult> ManageCourse(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var course = await _context.Courses
                .Include(c => c.Lessons)
                .Include(c => c.Quizzes)
                    .ThenInclude(q => q.Questions)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.User)
                .FirstOrDefaultAsync(c => c.Id == id && c.InstructorId == user!.Id);

            if (course == null) return NotFound();
            return View(course);
        }

        public async Task<IActionResult> AddLesson(int courseId)
        {
            var user = await _userManager.GetUserAsync(User);
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == user!.Id);
            if (course == null) return NotFound();
            ViewBag.Course = course;
            return View(new Lesson { CourseId = courseId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddLesson(Lesson model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Course = await _context.Courses.FindAsync(model.CourseId);
                return View(model);
            }
            _context.Lessons.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Lesson added.";
            return RedirectToAction(nameof(ManageCourse), new { id = model.CourseId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLesson(int id, int courseId)
        {
            var lesson = await _context.Lessons.FindAsync(id);
            if (lesson != null)
            {
                _context.Lessons.Remove(lesson);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Lesson deleted.";
            }
            return RedirectToAction(nameof(ManageCourse), new { id = courseId });
        }

        public async Task<IActionResult> CreateQuiz(int courseId)
        {
            var user = await _userManager.GetUserAsync(User);
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == user!.Id);
            if (course == null) return NotFound();
            ViewBag.Course = course;
            return View(new Quiz { CourseId = courseId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuiz(Quiz model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Course = await _context.Courses.FindAsync(model.CourseId);
                return View(model);
            }
            _context.Quizzes.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Quiz created.";
            return RedirectToAction(nameof(ManageQuiz), new { id = model.Id });
        }

        public async Task<IActionResult> ManageQuiz(int id)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                    .ThenInclude(q => q.Answers)
                .Include(q => q.Course)
                .FirstOrDefaultAsync(q => q.Id == id);
            if (quiz == null) return NotFound();
            return View(quiz);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddQuestion(int quizId, string questionText, int points, string[] answers, bool[] isCorrect)
        {
            var question = new Question
            {
                Text = questionText,
                Points = points,
                QuizId = quizId,
                Order = await _context.Questions.CountAsync(q => q.QuizId == quizId) + 1
            };

            for (int i = 0; i < answers.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(answers[i]))
                {
                    question.Answers.Add(new Answer
                    {
                        Text = answers[i],
                        IsCorrect = i < isCorrect.Length && isCorrect[i]
                    });
                }
            }

            _context.Questions.Add(question);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Question added.";
            return RedirectToAction(nameof(ManageQuiz), new { id = quizId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuestion(int id, int quizId)
        {
            var question = await _context.Questions.FindAsync(id);
            if (question != null)
            {
                _context.Questions.Remove(question);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(ManageQuiz), new { id = quizId });
        }

        public async Task<IActionResult> Students(int courseId)
        {
            var user = await _userManager.GetUserAsync(User);
            var course = await _context.Courses
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.User)
                .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == user!.Id);
            if (course == null) return NotFound();
            return View(course);
        }
    }
}
