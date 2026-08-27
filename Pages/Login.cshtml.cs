using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GiftOfTheGivers.Pages
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoginModel> _logger;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public LoginModel(ApplicationDbContext context, ILogger<LoginModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ModelState.AddModelError(string.Empty, "Email and password are required.");
                return Page();
            }

            return await SignInAsync(Email, Password, "/");
        }

        public Task<IActionResult> OnPostDemoEmployeeAsync()
        {
            return SignInAsync("employee@test.local", "Employee@123", "/Dashboards/Employee");
        }

        public Task<IActionResult> OnPostDemoDonorAsync()
        {
            return SignInAsync("donor@test.local", "Donor@123", "/Dashboards/Donor");
        }

        private async Task<IActionResult> SignInAsync(string email, string password, string redirectUrl)
        {
            try
            {
                // Find user by email
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    _logger.LogWarning("Login failed for {Email}: user not found", email);
                    TempData["LoginError"] = "Invalid email or password.";
                    return RedirectToPage();
                }

                // Verify password
                if (!SeedData.VerifyPassword(password, user.PasswordHash))
                {
                    _logger.LogWarning("Login failed for {Email}: invalid password", email);
                    TempData["LoginError"] = "Invalid email or password.";
                    return RedirectToPage();
                }

                // Create claims for the user
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                    new Claim(ClaimTypes.Role, user.Role ?? "Donor")
                };

                var claimsIdentity = new ClaimsIdentity(claims, "Cookies");
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
                };

                await HttpContext.SignInAsync(
                    "Cookies",
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                _logger.LogInformation("User {Email} logged in successfully", email);
                return LocalRedirect(redirectUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for {Email}", email);
                TempData["LoginError"] = "An error occurred during login. Please try again.";
                return RedirectToPage();
            }
        }
    }
}
