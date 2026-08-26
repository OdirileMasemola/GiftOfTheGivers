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

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(int id, string status)
        {
            if (!AllowedStatuses.Contains(status))
            {
                TempData["VolunteersError"] = "That status is not valid.";
                return RedirectToPage();
            }

            var volunteer = await _context.Volunteers.FindAsync(id);
            if (volunteer == null)
            {
                return NotFound();
            }

            volunteer.Status = status;
            await _context.SaveChangesAsync();

            TempData["VolunteersMessage"] = status switch
            {
                "Approved" => $"{volunteer.Name} has been approved.",
                "Active" => $"{volunteer.Name} is now active.",
                "Rejected" => $"{volunteer.Name} has been rejected.",
                "Pending" => $"{volunteer.Name} was moved back to pending.",
                _ => $"{volunteer.Name} updated."
            };

            return RedirectToPage();
        }

        private async Task LoadAsync()
        {
            Volunteers = await _context.Volunteers
                .OrderByDescending(v => v.Status == "Pending")
                .ThenByDescending(v => v.RegistrationDate)
                .ToListAsync();

            PendingCount = Volunteers.Count(v => v.Status == "Pending");
            ApprovedCount = Volunteers.Count(v => v.Status == "Approved");
            ActiveCount = Volunteers.Count(v => v.Status == "Active");
            RejectedCount = Volunteers.Count(v => v.Status == "Rejected");
        }
    }
}
