using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages.Dashboards
{
    [Authorize(Roles = "Employee")]
    public class VolunteersModel : PageModel
    {
        private static readonly string[] AllowedStatuses = { "Pending", "Approved", "Active", "Rejected" };
        private readonly ApplicationDbContext _context;

        public VolunteersModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<Volunteer> Volunteers { get; set; } = new();
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int ActiveCount { get; set; }
        public int RejectedCount { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string StatusFilter { get; set; } = "All";

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(int id, string status)
        {
            if (!AllowedStatuses.Contains(status))
            {
                TempData["VolunteersError"] = "That status is not valid.";

                return RedirectToPage(new
                {
                    Search,
                    StatusFilter
                });
            }

            var volunteer = await _context.Volunteers
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.VolunteerId == id);
            if (volunteer == null)
            {
                return NotFound();
            }

            volunteer.Status = status;
            await _context.SaveChangesAsync();

            var volunteerName = volunteer.User != null 
                ? $"{volunteer.User.FirstName} {volunteer.User.LastName}" 
                : "Volunteer";

            TempData["VolunteersMessage"] = status switch
            {
                "Approved" => $"{volunteerName} has been approved.",
                "Active" => $"{volunteerName} is now active.",
                "Rejected" => $"{volunteerName} has been rejected.",
                "Pending" => $"{volunteerName} was moved back to pending.",
                _ => $"{volunteerName} updated."
            };

            return RedirectToPage(new
            {
                Search,
                StatusFilter
            });
        }

        private async Task LoadAsync()
        {
            var query = _context.Volunteers
                .Include(v => v.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var search = Search.Trim();

                query = query.Where(v =>
                    (v.User != null &&
                     (v.User.FirstName.Contains(search) ||
                      v.User.LastName.Contains(search) ||
                      v.User.Email.Contains(search))) ||
                    (v.Skills != null &&
                     v.Skills.Contains(search)));
            }

            if (StatusFilter != "All" &&
                AllowedStatuses.Contains(StatusFilter))
            {
                query = query.Where(
                    v => v.Status == StatusFilter);
            }

            Volunteers = await query
                .OrderByDescending(v => v.Status == "Pending")
                .ThenByDescending(v => v.RegistrationDate)
                .ToListAsync();
            PendingCount = await _context.Volunteers.CountAsync(v => v.Status == "Pending");
            ApprovedCount = await _context.Volunteers.CountAsync(v => v.Status == "Approved");
            ActiveCount = await _context.Volunteers.CountAsync(v => v.Status == "Active");
            RejectedCount = await _context.Volunteers.CountAsync(v => v.Status == "Rejected");
        }
    }
}







