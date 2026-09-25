using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages.Dashboards
{
    [Authorize(Roles = "Employee")]
    public class VolunteerDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public VolunteerDetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public Volunteer? Volunteer { get; set; }

        public List<ReliefOperation> AvailableOperations { get; set; } = new();

        [BindProperty]
        public int ReliefOperationId { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Volunteer = await _context.Volunteers
                .Include(v => v.User)
                .Include(v => v.VolunteerAssignments)
                    .ThenInclude(a => a.ReliefOperation)
                .FirstOrDefaultAsync(v => v.VolunteerId == id);

            if (Volunteer == null)
            {
                return NotFound();
            }

            AvailableOperations = await _context.ReliefOperations
                .Where(o => o.Status == "Planning" || o.Status == "Active")
                .OrderByDescending(o => o.StartDate)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAssignAsync(int id)
        {
            var volunteer = await _context.Volunteers
                .FirstOrDefaultAsync(v => v.VolunteerId == id);

            if (volunteer == null)
            {
                return NotFound();
            }

            // Only Approved or Active volunteers can be assigned.
            if (volunteer.Status != "Approved" &&
                volunteer.Status != "Active")
            {
                TempData["VolunteersError"] =
                    "Only approved or active volunteers can be assigned.";

                return RedirectToPage(new { id });
            }

            // Make sure the selected operation exists
            // and is eligible for volunteer assignments.
            var operation = await _context.ReliefOperations
                .FirstOrDefaultAsync(o =>
                    o.ReliefOperationId == ReliefOperationId &&
                    (o.Status == "Planning" || o.Status == "Active"));

            if (operation == null)
            {
                TempData["VolunteersError"] =
                    "Please select an eligible relief operation.";

                return RedirectToPage(new { id });
            }

            // Prevent duplicate assignments.
            var duplicateExists = await _context.VolunteerAssignments
                .AnyAsync(a =>
                    a.VolunteerId == id &&
                    a.ReliefOperationId == ReliefOperationId);

            if (duplicateExists)
            {
                TempData["VolunteersError"] =
                    "This volunteer is already assigned to that relief operation.";

                return RedirectToPage(new { id });
            }

            var assignment = new VolunteerAssignment
            {
                VolunteerId = id,
                ReliefOperationId = ReliefOperationId,
                AssignedDate = DateTime.Now
            };

            _context.VolunteerAssignments.Add(assignment);

            await _context.SaveChangesAsync();

            TempData["VolunteersMessage"] =
                "Volunteer assigned to the relief operation successfully.";

            return RedirectToPage(new { id });
        }
    }
}