using LMS.Data;
using LMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains("Admin"))
                return RedirectToAction("Index", "Admin");
            if (roles.Contains("Instructor"))
                return RedirectToAction("Index", "Instructor");

            // Student dashboard
            var enrollments = await _context.Enrollments
                .Include(e => e.Course)
                    .ThenInclude(c => c!.Lessons)
                .Where(e => e.UserId == user.Id)
                .ToListAsync();

            ViewBag.User = user;
            ViewBag.Enrollments = enrollments;
            return View(enrollments);
        }
    }
}
