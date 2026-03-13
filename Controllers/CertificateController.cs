using LMS.Data;
using LMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Controllers
{
    [Authorize]
    public class CertificateController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CertificateController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Generate(int enrollmentId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            
            var enrollment = await _context.Enrollments
                .Include(e => e.Course)
                    .ThenInclude(c => c!.Instructor)
                .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.UserId == user.Id);

            if (enrollment == null)
            {
                return NotFound();
            }

            if (enrollment.Status != EnrollmentStatus.Completed)
            {
                TempData["Error"] = "You must complete the course to get a certificate";
                return RedirectToAction("Learn", "Courses", new { id = enrollment.CourseId });
            }

            if (enrollment.Course == null)
            {
                return NotFound();
            }

            var existingCert = await _context.Certificates
                .FirstOrDefaultAsync(c => c.UserId == user.Id && c.CourseId == enrollment.CourseId);

            if (existingCert == null)
            {
                var certificate = new Certificate
                {
                    UserId = user.Id,
                    CourseId = enrollment.CourseId,
                    CertificateNumber = Certificate.GenerateCertificateNumber(),
                    StudentName = user.FullName ?? "Student",
                    CourseName = enrollment.Course!.Title,
                    InstructorName = enrollment.Course.Instructor?.FullName ?? "Instructor",
                    IssuedAt = DateTime.UtcNow
                };

                _context.Certificates.Add(certificate);
                await _context.SaveChangesAsync();
                
                var notification = new Notification
                {
                    UserId = user.Id,
                    Type = NotificationType.CertificateIssued,
                    Title = "Certificate Issued!",
                    Message = $"Your certificate for '{enrollment.Course!.Title}' is ready!",
                    Link = $"/Certificate/View/{certificate.Id}"
                };
                
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Certificate generated successfully!";
            }

            return RedirectToAction("MyCertificates");
        }

        public async Task<IActionResult> MyCertificates()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            
            var certificates = await _context.Certificates
                .Include(c => c.Course)
                .ThenInclude(c => c!.Instructor)
                .Where(c => c.UserId == user.Id)
                .OrderByDescending(c => c.IssuedAt)
                .ToListAsync();

            return View(certificates);
        }

        public async Task<IActionResult> View(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            
            var certificate = await _context.Certificates
                .Include(c => c.Course)
                .ThenInclude(c => c!.Instructor)
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id);

            if (certificate == null)
            {
                return NotFound();
            }

            return View(certificate);
        }

        public async Task<IActionResult> Download(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            
            var certificate = await _context.Certificates
                .Include(c => c.Course)
                .ThenInclude(c => c!.Instructor)
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id);

            if (certificate == null)
            {
                return NotFound();
            }

            certificate.DownloadedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return View(certificate);
        }
    }
}
