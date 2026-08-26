using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiftOfTheGivers.Pages.Dashboards
{
    [Authorize]
    public class SettingsModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public SettingsModel(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public string Email { get; set; } = string.Empty;
        public string RoleLabel { get; set; } = "User";

        [BindProperty]
        public string CurrentPassword { get; set; } = string.Empty;

        [BindProperty]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty]
        public string ConfirmPassword { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            await LoadProfileAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostChangePasswordAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            await LoadProfileAsync(user);

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

            var result = await _userManager.ChangePasswordAsync(user, CurrentPassword, NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return Page();
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["SettingsMessage"] = "Your password has been updated.";
            return RedirectToPage();
        }

        private async Task LoadProfileAsync(IdentityUser user)
        {
            Email = user.Email ?? user.UserName ?? string.Empty;
            var roles = await _userManager.GetRolesAsync(user);
            RoleLabel = roles.Count > 0 ? string.Join(", ", roles) : "User";
        }
    }
}
