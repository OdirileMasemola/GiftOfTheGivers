using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiftOfTheGivers.Pages
{
    public class VolunteerConfirmationModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public Volunteer? Volunteer { get; set; }

        public VolunteerConfirmationModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task OnGetAsync(int id)
        {
            Volunteer = await _context.Volunteers.FindAsync(id);
        }
    }
}
