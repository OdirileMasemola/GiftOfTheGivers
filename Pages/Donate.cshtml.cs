using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages
{
    public class DonateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        [BindProperty]
        public decimal Amount { get; set; }

        [BindProperty]
        public string Currency { get; set; } = "ZAR";

        [BindProperty]
        public string? Notes { get; set; }

        public DonateModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid || Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Please enter a valid donation amount.");
                return Page();
            }

            try
            {
                // Get or create donor user
                // For demo purposes, use a default donor or the authenticated user
                // In production, this would be the logged-in user
                var donorUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "donor@test.local");
                if (donorUser == null)
                {
                    // Create a basic donor user if it doesn't exist
                    donorUser = new User
                    {
                        FirstName = "Anonymous",
                        LastName = "Donor",
                        Email = "anonymous@donor.local",
                        PasswordHash = "", // No password for anonymous donations
                        Role = "Donor",
                        CreatedAt = DateTime.Now
                    };
                    _context.Users.Add(donorUser);
                    await _context.SaveChangesAsync();
                }

                // Create donation record
                var donation = new Donation
                {
                    UserId = donorUser.UserId,
                    Amount = Amount,
                    Currency = Currency,
                    DonationDate = DateTime.Now,
                    PaymentStatus = "Completed", // Assume completed for demo
                    PaymentReference = $"PAY-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}"
                };

                _context.Donations.Add(donation);
                await _context.SaveChangesAsync();

                // Optionally create a tax certificate
                var taxCertificate = new TaxCertificate
                {
                    DonationId = donation.DonationId,
                    CertificateNumber = $"CERT-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}",
                    IssueDate = DateTime.Today,
                    CertificateAmount = Amount,
                    CreatedAt = DateTime.Now
                };

                _context.TaxCertificates.Add(taxCertificate);
                await _context.SaveChangesAsync();

                TempData["DonationMessage"] = $"Thank you for your donation of {Amount} {Currency}!";
                return RedirectToPage("/Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Error processing donation: {ex.Message}");
                return Page();
            }
        }
    }
}
