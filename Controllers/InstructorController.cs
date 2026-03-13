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
        private readonly IWebHostEnvironment _environment;

        public InstructorController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }

        private async Task<string?> SaveImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;
            
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLower();
            
            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("CourseImage", "Only JPG, PNG, and GIF files are allowed.");
                return null;
            }
            
            if (file.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError("CourseImage", "File size must be less than 5MB.");
                return null;
            }
            
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "courses");
            Directory.CreateDirectory(uploadsFolder);
            
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);
            
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            
            return $"/uploads/courses/{fileName}";
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
        public async Task<IActionResult> CreateCourse(Course model, IFormFile? CourseImage)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            model.InstructorId = user!.Id;
            model.CreatedAt = DateTime.UtcNow;
            
            if (CourseImage != null && CourseImage.Length > 0)
            {
                model.ThumbnailUrl = await SaveImageAsync(CourseImage);
            }
            
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
        public async Task<IActionResult> EditCourse(Course model, IFormFile? CourseImage)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _userManager.GetUserAsync(User);
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == model.Id && c.InstructorId == user!.Id);
            if (course == null) return NotFound();

            course.Title = model.Title;
            course.Description = model.Description;
            course.Category = model.Category;
            course.Status = model.Status;
            course.Price = model.Price;
            course.UpdatedAt = DateTime.UtcNow;
            
            if (CourseImage != null && CourseImage.Length > 0)
            {
                course.ThumbnailUrl = await SaveImageAsync(CourseImage);
            }
            
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
            
            var liveClasses = await _context.LiveClasses
                .Where(l => l.CourseId == id)
                .OrderByDescending(l => l.ScheduledAt)
                .ToListAsync();
            
            ViewBag.LiveClasses = liveClasses;
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
