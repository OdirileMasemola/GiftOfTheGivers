using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiftOfTheGivers.Pages.Dashboards
{
    public class VolunteerModel : PageModel
    {
        private readonly ILogger<VolunteerModel> _logger;

        public VolunteerModel(ILogger<VolunteerModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }

        public IActionResult OnPost(int availableHours, string availabilityType, List<string> preferredRoles)
        {
            // For Part 1 prototype, simply log and redirect
            _logger.LogInformation($"Volunteer availability updated: {availableHours} hours, {availabilityType}");

            // In production, this would save to database
            // For now, just redirect back to dashboard
            return RedirectToPage();
        }
    }
}
