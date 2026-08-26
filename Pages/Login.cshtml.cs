using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiftOfTheGivers.Pages
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(SignInManager<IdentityUser> signInManager, ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
        }

        public void OnGet()
        {
        }

        public Task<IActionResult> OnPostDemoEmployeeAsync()
        {
            return SignInDemoAsync("employee@test.local", "Employee@123", "/Dashboards/Employee");
        }

        public Task<IActionResult> OnPostDemoDonorAsync()
        {
            return SignInDemoAsync("donor@test.local", "Donor@123", "/Dashboards/Donor");
        }

        private async Task<IActionResult> SignInDemoAsync(string email, string password, string redirectUrl)
        {
            var result = await _signInManager.PasswordSignInAsync(email, password, isPersistent: false, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                return LocalRedirect(redirectUrl);
            }

            _logger.LogWarning("Demo login failed for {Email}", email);
            TempData["LoginError"] = "Could not sign in with the demo account. Restart the app so seed data can run.";
            return RedirectToPage();
        }
    }
}
