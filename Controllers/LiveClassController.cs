using LMS.Data;
using LMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Controllers
{
    [Authorize]
    public class LiveClassController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public LiveClassController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var isInstructor = await _userManager.IsInRoleAsync(user!, "Instructor") || await _userManager.IsInRoleAsync(user!, "Admin");

            List<LiveClass> liveClasses;

            if (isInstructor)
            {
                liveClasses = await _context.LiveClasses
                    .Include(l => l.Course)
                    .Where(l => l.InstructorId == user!.Id)
                    .OrderByDescending(l => l.ScheduledAt)
                    .ToListAsync();
            }
            else
            {
                var enrolledCourseIds = await _context.Enrollments
                    .Where(e => e.UserId == user!.Id)
                    .Select(e => e.CourseId)
                    .ToListAsync();

                liveClasses = await _context.LiveClasses
                    .Include(l => l.Course)
                    .Where(l => enrolledCourseIds.Contains(l.CourseId))
                    .OrderByDescending(l => l.ScheduledAt)
                    .ToListAsync();
            }

            return View(liveClasses);
        }

        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> Create(int courseId)
        {
            var user = await _userManager.GetUserAsync(User);
            
            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == user!.Id);
            
            if (course == null)
            {
                return NotFound();
            }

            var model = new LiveClass
            {
                CourseId = courseId,
                Course = course,
                ScheduledAt = DateTime.Now.AddHours(1),
                DurationMinutes = 60
            };

            ViewBag.MeetingUrlSuggestions = new[]
            {
                "https://meet.jit.si/LMS-" + Guid.NewGuid().ToString("N")[..8],
                "https://meet.jit.si/" + course.Title.Replace(" ", "-") + "-" + DateTime.Now.ToString("yyyyMMdd")
            };

            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Instructor,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LiveClass model)
        {
            var user = await _userManager.GetUserAsync(User);
            
            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == model.CourseId && c.InstructorId == user!.Id);
            
            if (course == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                model.Course = course;
                return View(model);
            }

            model.InstructorId = user!.Id;
            model.Status = LiveClassStatus.Scheduled;
            model.CreatedAt = DateTime.UtcNow;

            _context.LiveClasses.Add(model);
            await _context.SaveChangesAsync();

            await NotifyStudentsAsync(model);

            TempData["Success"] = "Live class scheduled successfully!";
            return RedirectToAction(nameof(MyLiveClasses));
        }

        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> MyLiveClasses()
        {
            var user = await _userManager.GetUserAsync(User);
            
            var liveClasses = await _context.LiveClasses
                .Include(l => l.Course)
                .Where(l => l.InstructorId == user!.Id)
                .OrderByDescending(l => l.ScheduledAt)
                .ToListAsync();

            return View(liveClasses);
        }

        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> Manage(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            
            var liveClass = await _context.LiveClasses
                .Include(l => l.Course)
                .FirstOrDefaultAsync(l => l.Id == id && l.InstructorId == user!.Id);
            
            if (liveClass == null)
            {
                return NotFound();
            }

            return View(liveClass);
        }

        [Authorize(Roles = "Instructor,Admin")]
        [HttpPost]
        public async Task<IActionResult> Start(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            
            var liveClass = await _context.LiveClasses
                .FirstOrDefaultAsync(l => l.Id == id && l.InstructorId == user!.Id);
            
            if (liveClass == null)
            {
                return NotFound();
            }

            liveClass.Status = LiveClassStatus.Live;
            liveClass.StartedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Redirect(liveClass.MeetingUrl);
        }

        [Authorize(Roles = "Instructor,Admin")]
        [HttpPost]
        public async Task<IActionResult> End(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            
            var liveClass = await _context.LiveClasses
                .FirstOrDefaultAsync(l => l.Id == id && l.InstructorId == user!.Id);
            
            if (liveClass == null)
            {
                return NotFound();
            }

            liveClass.Status = LiveClassStatus.Completed;
            liveClass.EndedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Live session ended successfully.";
            return RedirectToAction(nameof(Manage), new { id = id });
        }

        public async Task<IActionResult> Join(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            
            var liveClass = await _context.LiveClasses
                .Include(l => l.Course)
                .Include(l => l.Instructor)
                .FirstOrDefaultAsync(l => l.Id == id);
            
            if (liveClass == null)
            {
                return NotFound();
            }

            var isEnrolled = await _context.Enrollments
                .AnyAsync(e => e.CourseId == liveClass.CourseId && e.UserId == user!.Id);
            
            var isInstructor = liveClass.InstructorId == user!.Id;
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (!isEnrolled && !isInstructor && !isAdmin)
            {
                return RedirectToAction("Details", "Courses", new { id = liveClass.CourseId });
            }

            return View(liveClass);
        }

        public async Task<IActionResult> Watch(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            
            var liveClass = await _context.LiveClasses
                .Include(l => l.Course)
                .Include(l => l.Instructor)
                .FirstOrDefaultAsync(l => l.Id == id);
            
            if (liveClass == null)
            {
                return NotFound();
            }

            var isEnrolled = await _context.Enrollments
                .AnyAsync(e => e.CourseId == liveClass.CourseId && e.UserId == user!.Id);
            
            var isInstructor = liveClass.InstructorId == user!.Id;
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (!isEnrolled && !isInstructor && !isAdmin)
            {
                return RedirectToAction("Details", "Courses", new { id = liveClass.CourseId });
            }

            return View(liveClass);
        }

        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> Cancel(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            
            var liveClass = await _context.LiveClasses
                .FirstOrDefaultAsync(l => l.Id == id && l.InstructorId == user!.Id);
            
            if (liveClass == null)
            {
                return NotFound();
            }

            liveClass.Status = LiveClassStatus.Cancelled;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Live class cancelled.";
            return RedirectToAction(nameof(MyLiveClasses));
        }

        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            
            var liveClass = await _context.LiveClasses
                .FirstOrDefaultAsync(l => l.Id == id && l.InstructorId == user!.Id);
            
            if (liveClass == null)
            {
                return NotFound();
            }

            _context.LiveClasses.Remove(liveClass);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Live class deleted.";
            return RedirectToAction(nameof(MyLiveClasses));
        }

        public async Task<IActionResult> GetUpcoming()
        {
            var user = await _userManager.GetUserAsync(User);
            var isInstructor = await _userManager.IsInRoleAsync(user!, "Instructor") || await _userManager.IsInRoleAsync(user!, "Admin");

            List<LiveClass> liveClasses;

            if (isInstructor)
            {
                liveClasses = await _context.LiveClasses
                    .Include(l => l.Course)
                    .Where(l => l.InstructorId == user!.Id && l.ScheduledAt > DateTime.UtcNow && l.Status == LiveClassStatus.Scheduled)
                    .OrderBy(l => l.ScheduledAt)
                    .Take(5)
                    .ToListAsync();
            }
            else
            {
                var enrolledCourseIds = await _context.Enrollments
                    .Where(e => e.UserId == user!.Id)
                    .Select(e => e.CourseId)
                    .ToListAsync();

                liveClasses = await _context.LiveClasses
                    .Include(l => l.Course)
                    .Where(l => enrolledCourseIds.Contains(l.CourseId) && l.ScheduledAt > DateTime.UtcNow && l.Status == LiveClassStatus.Scheduled)
                    .OrderBy(l => l.ScheduledAt)
                    .Take(5)
                    .ToListAsync();
            }

            return Json(liveClasses.Select(l => new
            {
                l.Id,
                l.Title,
                l.ScheduledAt,
                l.DurationMinutes,
                l.Status,
                CourseName = l.Course!.Title,
                l.MeetingUrl
            }));
        }

        private async Task NotifyStudentsAsync(LiveClass liveClass)
        {
            var enrolledStudents = await _context.Enrollments
                .Where(e => e.CourseId == liveClass.CourseId)
                .Select(e => e.UserId)
                .ToListAsync();

            var notifications = enrolledStudents.Select(studentId => new Notification
            {
                UserId = studentId,
                Title = "New Live Class Scheduled",
                Message = $"A new live class '{liveClass.Title}' has been scheduled for {liveClass.ScheduledAt.ToString("MMMM d, yyyy 'at' h:mm tt")}.",
                Type = NotificationType.LiveClass,
                Link = $"/LiveClass/Join/{liveClass.Id}",
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }
    }
}
