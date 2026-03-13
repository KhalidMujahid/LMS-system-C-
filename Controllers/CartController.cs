using LMS.Data;
using LMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json;

namespace LMS.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly HttpClient _httpClient;

        public CartController(
            ApplicationDbContext context, 
            IConfiguration configuration, 
            UserManager<ApplicationUser> userManager,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _configuration = configuration;
            _userManager = userManager;
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id;
            
            var cartWithItems = await _context.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.Course)
                .ThenInclude(c => c!.Instructor)
                .FirstOrDefaultAsync(c => c.UserId == userId);
            
            return View(cartWithItems);
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(int courseId)
        {
            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id;
            
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }
            
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null || course.Status != CourseStatus.Published)
            {
                TempData["Error"] = "Course not found or not available";
                return RedirectToAction("Index", "Courses");
            }
            
            var alreadyEnrolled = await _context.Enrollments
                .AnyAsync(e => e.UserId == userId && e.CourseId == courseId);
            if (alreadyEnrolled)
            {
                TempData["Error"] = "You are already enrolled in this course";
                return RedirectToAction("Details", "Courses", new { id = courseId });
            }
            
            var cart = await GetOrCreateCart(userId);
            
            var existingItem = await _context.CartItems
                .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.CourseId == courseId);
            
            if (existingItem != null)
            {
                TempData["Success"] = "Course is already in your cart";
                return RedirectToAction("Index");
            }
            
            var cartItem = new CartItem
            {
                CartId = cart.Id,
                CourseId = courseId,
                AddedAt = DateTime.UtcNow
            };
            
            _context.CartItems.Add(cartItem);
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Course added to cart successfully";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int itemId)
        {
            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id;
            
            var cart = await GetOrCreateCart(userId);
            
            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.Id == itemId && ci.Cart!.UserId == userId);
            
            if (cartItem != null)
            {
                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Item removed from cart";
            }
            
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Checkout()
        {
            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id;
            
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }
            
            var cart = await _context.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.Course)
                .FirstOrDefaultAsync(c => c.UserId == userId);
            
            if (cart == null || !cart.Items.Any())
            {
                TempData["Error"] = "Your cart is empty";
                return RedirectToAction("Index");
            }
            
            var totalAmount = cart.Items.Sum(i => i.Course?.Price ?? 0);
            
            if (totalAmount <= 0)
            {
                foreach (var item in cart.Items)
                {
                    await EnrollUser(userId, item.CourseId);
                }
                
                _context.CartItems.RemoveRange(cart.Items);
                await _context.SaveChangesAsync();
                
                TempData["Success"] = "You have been enrolled in all courses successfully!";
                return RedirectToAction("Index", "Dashboard");
            }
            
            var payment = new Payment
            {
                UserId = userId,
                Reference = $"LMS_{Guid.NewGuid().ToString("N")[..16]}",
                Amount = totalAmount,
                Currency = "NGN",
                Status = PaymentStatus.Pending,
                Email = user?.Email,
                CreatedAt = DateTime.UtcNow
            };
            
            foreach (var item in cart.Items)
            {
                payment.Items.Add(new PaymentItem
                {
                    CourseId = item.CourseId,
                    PriceAtPurchase = item.Course?.Price ?? 0
                });
            }
            
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            
            var paystackSecretKey = _configuration["Paystack:SecretKey"] ?? "sk_test_placeholder";
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {paystackSecretKey}");
            
            var callbackUrl = Url.Action("PaymentCallback", "Cart", null, Request.Scheme);
            
            var requestBody = new
            {
                email = user?.Email,
                amount = (long)totalAmount,
                reference = payment.Reference,
                callback_url = callbackUrl
            };
            
            try
            {
                var response = await _httpClient.PostAsJsonAsync("https://api.paystack.co/transaction/initialize", requestBody);
                var responseData = await response.Content.ReadFromJsonAsync<JsonElement>();
                
                if (response.IsSuccessStatusCode && responseData.GetProperty("status").GetBoolean())
                {
                    HttpContext.Session.SetInt32("PaymentId", payment.Id);
                    
                    var authorizationUrl = responseData.GetProperty("data").GetProperty("authorization_url").GetString();
                    return Redirect(authorizationUrl ?? "");
                }
                else
                {
                    var message = responseData.TryGetProperty("message", out var msg) ? msg.GetString() : "Failed to initialize payment";
                    TempData["Error"] = "Failed to initialize payment: " + message;
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Payment initialization error: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        public async Task<IActionResult> PaymentCallback()
        {
            var reference = Request.Query["reference"].ToString();
            
            if (string.IsNullOrEmpty(reference))
            {
                TempData["Error"] = "Invalid payment reference";
                return RedirectToAction("Index");
            }
            
            var paystackSecretKey = _configuration["Paystack:SecretKey"] ?? "sk_test_placeholder";
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {paystackSecretKey}");
            
            var response = await _httpClient.GetAsync($"https://api.paystack.co/transaction/verify/{reference}");
            var responseData = await response.Content.ReadFromJsonAsync<JsonElement>();
            
            var payment = await _context.Payments
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Reference == reference);
            
            if (payment == null)
            {
                TempData["Error"] = "Payment not found";
                return RedirectToAction("Index");
            }
            
            if (response.IsSuccessStatusCode && responseData.GetProperty("status").GetBoolean())
            {
                var data = responseData.GetProperty("data");
                
                payment.Status = PaymentStatus.Completed;
                payment.TransactionId = data.TryGetProperty("id", out var transId) ? transId.GetInt64().ToString() : null;
                payment.PaymentMethod = data.TryGetProperty("channel", out var channel) ? channel.GetString() : null;
                payment.CompletedAt = DateTime.UtcNow;
                
                foreach (var item in payment.Items)
                {
                    await EnrollUser(payment.UserId, item.CourseId);
                }
                
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == payment.UserId);
                
                if (cart != null)
                {
                    _context.CartItems.RemoveRange(cart.Items);
                }
                
                await _context.SaveChangesAsync();
                
                TempData["Success"] = "Payment successful! You have been enrolled in your courses.";
                return RedirectToAction("Index", "Dashboard");
            }
            else
            {
                payment.Status = PaymentStatus.Failed;
                await _context.SaveChangesAsync();
                
                TempData["Error"] = "Payment failed. Please try again.";
                return RedirectToAction("Index");
            }
        }

        public IActionResult PaymentFailed()
        {
            return View();
        }

        private async Task EnrollUser(string userId, int courseId)
        {
            var existingEnrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == courseId);
            
            if (existingEnrollment == null)
            {
                var enrollment = new Enrollment
                {
                    UserId = userId,
                    CourseId = courseId,
                    EnrolledAt = DateTime.UtcNow,
                    Status = EnrollmentStatus.Active
                };
                
                _context.Enrollments.Add(enrollment);
            }
        }

        private async Task<Cart> GetOrCreateCart(string? userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return new Cart { Id = 0, UserId = "" };
            }
            
            var cart = await _context.Carts
                .FirstOrDefaultAsync(c => c.UserId == userId);
            
            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }
            
            return cart;
        }
    }
}
