using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiftOfTheGivers.Pages.Dashboards
{
    public class SettingsModel : PageModel
    {
        public string Email { get; set; } = "user@example.com";
        public string RoleLabel { get; set; } = "Donor";

        [BindProperty]
        public string CurrentPassword { get; set; } = string.Empty;

        [BindProperty]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty]
        public string ConfirmPassword { get; set; } = string.Empty;

        public IActionResult OnGet()
        {
            // For prototype, just show default values
            LoadProfile();
            return Page();
        }

        public IActionResult OnPostChangePasswordAsync()
        {
            LoadProfile();

            if (string.IsNullOrWhiteSpace(CurrentPassword) || string.IsNullOrWhiteSpace(NewPassword))
            {
                ModelState.AddModelError(string.Empty, "Enter your current password and a new password.");
                return Page();
            }

            if (NewPassword != ConfirmPassword)
            {
                ModelState.AddModelError(string.Empty, "The new password and confirmation do not match.");
                return Page();
            }

            // For prototype, just show success message
            TempData["SettingsMessage"] = "Your password has been updated.";
            return RedirectToPage();
        }

        private void LoadProfile()
        {
            Email = User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/email")?.Value ?? "user@example.com";
            RoleLabel = User?.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value ?? "Donor";
        }
    }
}
