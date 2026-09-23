using GiftOfTheGivers.Data;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages
{
    public class VolunteerModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        [BindProperty]
        [Required(ErrorMessage = "First name is required.")]
        public string FirstName { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Last name is required.")]
        public string LastName { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string? PhoneNumber { get; set; }

        [BindProperty]
        public string Skills { get; set; } = string.Empty;

        [BindProperty]
        public string[] SelectedSkills { get; set; } = Array.Empty<string>();

        [BindProperty]
        [Required(ErrorMessage = "Select your availability.")]
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
            FirstName = FirstName.Trim();
            LastName = LastName.Trim();
            Email = Email.Trim();
            Availability = Availability.Trim();
            Skills = Skills.Trim();

            var combinedSkills = SelectedSkills
                .Where(skill => !string.IsNullOrWhiteSpace(skill))
                .Select(skill => skill.Trim())
                .Concat(string.IsNullOrWhiteSpace(Skills) ? Array.Empty<string>() : new[] { Skills })
                .ToArray();

            if (combinedSkills.Length == 0)
            {
                ModelState.AddModelError(nameof(Skills), "Select at least one skill or describe your other skills.");
            }

            if (!ModelState.IsValid)
            {
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
                        PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim(),
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
                    if (!string.IsNullOrWhiteSpace(PhoneNumber))
                    {
                        volunteerUser.PhoneNumber = PhoneNumber.Trim();
                    }
                }

                // Create volunteer record linked to the user
                var volunteer = new Volunteer
                {
                    UserId = volunteerUser.UserId,
                    Skills = string.Join(", ", combinedSkills),
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
