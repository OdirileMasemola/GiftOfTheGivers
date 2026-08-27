using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages
{
    public class VolunteerModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        [BindProperty]
        public string FirstName { get; set; } = string.Empty;

        [BindProperty]
        public string LastName { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Skills { get; set; } = string.Empty;

        [BindProperty]
        public string Availability { get; set; } = string.Empty;

        public VolunteerModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName))
            {
                ModelState.AddModelError(string.Empty, "First name, last name, and other required fields are required.");
                return Page();
            }

            try
            {
                // Check if user already exists
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == Email);

                User volunteerUser;
                if (existingUser == null)
                {
                    // Create a new user for the volunteer
                    volunteerUser = new User
                    {
                        FirstName = FirstName.Trim(),
                        LastName = LastName.Trim(),
                        Email = Email.Trim(),
                        PasswordHash = "", // User can set password later
                        Role = "Donor", // Volunteers can also be donors
                        CreatedAt = DateTime.Now
                    };
                    _context.Users.Add(volunteerUser);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    volunteerUser = existingUser;
                }

                // Create volunteer record linked to the user
                var volunteer = new Volunteer
                {
                    UserId = volunteerUser.UserId,
                    Skills = Skills.Trim(),
                    Availability = Availability.Trim(),
                    RegistrationDate = DateTime.Now,
                    Status = "Pending"
                };

                _context.Volunteers.Add(volunteer);
                await _context.SaveChangesAsync();

                // Show success message and redirect
                TempData["SuccessMessage"] = $"Thank you for registering, {FirstName}! We'll review your application and contact you soon.";
                return RedirectToPage("/VolunteerConfirmation", new { id = volunteer.VolunteerId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Error registering volunteer: {ex.Message}");
                return Page();
            }
        }
    }
}
