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

            return Page();
        }
    }
}