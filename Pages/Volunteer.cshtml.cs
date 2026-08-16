using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiftOfTheGivers.Pages
{
    public class VolunteerModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        [BindProperty]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Skills { get; set; } = string.Empty;

        [BindProperty]
        public string Availability { get; set; } = string.Empty;

        [BindProperty]
        public List<string> SelectedSkills { get; set; } = new();

        public VolunteerModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Combine selected skills with other skills
            string allSkills = Skills;
            if (SelectedSkills.Any())
            {
                allSkills = string.Join(", ", SelectedSkills) + (string.IsNullOrWhiteSpace(Skills) ? "" : ", " + Skills);
            }

            // Create volunteer record
            var volunteer = new Volunteer
            {
                Name = Name,
                Email = Email,
                Skills = allSkills,
                Availability = Availability,
                RegistrationDate = DateTime.Now,
                Status = "Pending"
            };

            _context.Volunteers.Add(volunteer);
            await _context.SaveChangesAsync();

            // Show success message and redirect
            TempData["SuccessMessage"] = $"Thank you for registering, {Name}! We'll review your application and contact you soon.";
            return RedirectToPage("/VolunteerConfirmation", new { id = volunteer.Id });
        }
    }
}
