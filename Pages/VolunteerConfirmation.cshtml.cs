using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages
{
    public class VolunteerConfirmationModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<VolunteerConfirmationModel> _logger;

        public Volunteer? Volunteer { get; set; }
        public bool IsUnavailable { get; private set; }
        public string? SuccessMessage { get; private set; }

        public VolunteerConfirmationModel(ApplicationDbContext context, ILogger<VolunteerConfirmationModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            SuccessMessage = TempData["SuccessMessage"] as string;

            if (id <= 0)
            {
                IsUnavailable = true;
                return Page();
            }

            // Load the applicant in one query so confirmation never renders partial user details.
            Volunteer = await _context.Volunteers
                .Include(volunteer => volunteer.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(volunteer => volunteer.VolunteerId == id);

            IsUnavailable = Volunteer is null;
            if (IsUnavailable)
            {
                _logger.LogWarning("Volunteer confirmation requested for missing application {VolunteerId}", id);
            }

            return Page();
        }
    }
}
