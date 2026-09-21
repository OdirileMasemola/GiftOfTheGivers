using System.Security.Claims;
using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages.Dashboards
{
    
    [Authorize]
    public class VolunteerModel : PageModel
    {
        // The availability options a volunteer can choose from.
        // These are the same options as the public volunteer registration form,
        // so the value saved here matches the value saved there.
        public static readonly string[] AvailabilityOptions =
            { "Full-time", "Part-time", "Weekends only", "Occasional" };

        // Logger: writes messages to the application log (useful for debugging).
        private readonly ILogger<VolunteerModel> _logger;

        // Database context: lets us read and write the Azure SQL tables.
        private readonly ApplicationDbContext _db;

        // ASP.NET Core supplies both services automatically (dependency injection).
        public VolunteerModel(ILogger<VolunteerModel> logger, ApplicationDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        // ----- Data the .cshtml page reads through "Model" -----

        // The logged-in person's row from the Users table (name, email, phone).
        public User? CurrentUser { get; private set; }

        // The logged-in person's row from the Volunteers table
        // (skills, availability, status). It is null if they never registered as a volunteer.
        public Volunteer? Profile { get; private set; }

        // Assignments whose relief operation is not completed yet.
        public List<VolunteerAssignment> CurrentAssignments { get; private set; } = new();

        // Assignments whose relief operation is completed.
        public List<VolunteerAssignment> CompletedAssignments { get; private set; } = new();

        // Total number of assignments, worked out from the two lists above.
        public int TotalAssignments => CurrentAssignments.Count + CompletedAssignments.Count;

        // ----- Handlers -----

        // Runs when the page is opened (a GET request).
        public async Task<IActionResult> OnGetAsync()
        {
            // Find out which user is logged in.
            var userId = GetUserId();
            if (userId == null)
            {
                // No valid user id in the login cookie, so send them to the login page.
                return RedirectToPage("/Login");
            }

            // Fill CurrentUser, Profile and the assignment lists.
            await LoadAsync(userId.Value);
            return Page();
        }

        // Runs when the "Update Availability" form is submitted (a POST request).
        // "availabilityType" matches the name="availabilityType" select in the form.
        public async Task<IActionResult> OnPostAsync(string availabilityType)
        {
            var userId = GetUserId();
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            // Find this user's volunteer profile. No AsNoTracking here,
            // because we are going to change it and save it.
            var volunteer = await _db.Volunteers.FirstOrDefaultAsync(v => v.UserId == userId.Value);
            if (volunteer == null)
            {
                TempData["ErrorMessage"] = "You need to register as a volunteer first.";
                return RedirectToPage();
            }

            // Only accept one of the four allowed options.
            // This stops someone sending a made-up value by editing the form.
            if (!AvailabilityOptions.Contains(availabilityType))
            {
                TempData["ErrorMessage"] = "Please choose a valid availability option.";
                return RedirectToPage();
            }

            // Save the new availability to the Volunteers table.
            volunteer.Availability = availabilityType;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Volunteer {VolunteerId} availability set to {Availability}",
                volunteer.VolunteerId, availabilityType);

            // TempData survives one redirect, so the page can show this message once.
            TempData["SuccessMessage"] = "Your availability has been updated.";
            return RedirectToPage();
        }

        // ----- Helpers -----

        // Reads the user's id from the login cookie.
        // The Login page stores it as ClaimTypes.NameIdentifier when the user signs in.
        // Returns null if the claim is missing or is not a number.
        private int? GetUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idClaim, out var id) ? id : null;
        }

        // Loads everything the dashboard needs from the database.
        private async Task LoadAsync(int userId)
        {
            // AsNoTracking = read only, which is a little faster when we won't save changes.
            CurrentUser = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            // A user may have no volunteer profile yet (for example a Donor),
            // so Profile can be null.
            Profile = await _db.Volunteers
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.UserId == userId);

            // Without a volunteer profile there are no assignments to load.
            if (Profile == null)
            {
                return;
            }

            // Get this volunteer's assignments together with each relief operation
            // (Include = a JOIN to the ReliefOperations table), newest first.
            var assignments = await _db.VolunteerAssignments
                .AsNoTracking()
                .Include(a => a.ReliefOperation)
                .Where(a => a.VolunteerId == Profile.VolunteerId)
                .OrderByDescending(a => a.AssignedDate)
                .ToListAsync();

            // Split them into completed and current, based on the operation's status.
            CompletedAssignments = assignments
                .Where(a => a.ReliefOperation?.Status == "Completed")
                .ToList();

            CurrentAssignments = assignments
                .Where(a => a.ReliefOperation?.Status != "Completed")
                .ToList();
        }
    }
}
