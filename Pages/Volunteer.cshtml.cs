using GiftOfTheGivers.Data;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages
{
    public class VolunteerModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<VolunteerModel> _logger;

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

        public VolunteerModel(ApplicationDbContext context, ILogger<VolunteerModel> logger)
        {
            _context = context;
            _logger = logger;
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
                var normalizedEmail = Email.ToUpperInvariant();
                var existingUser = await _context.Users
                    .Include(user => user.Volunteers)
                    .FirstOrDefaultAsync(user => user.Email.ToUpper() == normalizedEmail);

                User volunteerUser;
                if (existingUser == null)
                {
                    // Public applicants receive no login credential until an activation flow exists.
                    volunteerUser = new User
                    {
                        FirstName = FirstName.Trim(),
                        LastName = LastName.Trim(),
                        Email = Email.Trim(),
                        PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim(),
                        PasswordHash = SeedData.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
                        Role = "Volunteer",
                        CreatedAt = DateTime.Now
                    };
                    _context.Users.Add(volunteerUser);
                }
                else
                {
                    volunteerUser = existingUser;
                    if (existingUser.Volunteers.Any())
                    {
                        ModelState.AddModelError(string.Empty, "An application already exists for this email address. Please contact support if you need to update it.");
                        return Page();
                    }

                    volunteerUser.FirstName = FirstName;
                    volunteerUser.LastName = LastName;
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
                _logger.LogError(ex, "Error registering volunteer application for {Email}", Email);
                ModelState.AddModelError(string.Empty, "We could not submit your application right now. Please try again.");
                return Page();
            }
        }
    }
}
