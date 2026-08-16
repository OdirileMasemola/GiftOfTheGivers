using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiftOfTheGivers.Pages
{
    public class DonateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        [BindProperty]
        public string DonorName { get; set; } = string.Empty;

        [BindProperty]
        public string DonorEmail { get; set; } = string.Empty;

        [BindProperty]
        public decimal Amount { get; set; }

        [BindProperty]
        public string Currency { get; set; } = "ZAR";

        [BindProperty]
        public string DonationType { get; set; } = "OneTime";

        [BindProperty]
        public string? RecurringFrequency { get; set; }

        public DonateModel(ApplicationDbContext context)
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

            // Validate donation type specific fields
            if (DonationType == "Recurring" && string.IsNullOrEmpty(RecurringFrequency))
            {
                ModelState.AddModelError("RecurringFrequency", "Please select a frequency for recurring donations.");
                return Page();
            }

            // Generate certificate number
            string certificateNumber = $"CERT-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";

            // Create donation record
            var donation = new Donation
            {
                DonorName = DonorName,
                DonorEmail = DonorEmail,
                Amount = Amount,
                Currency = Currency,
                DonationType = DonationType,
                RecurringFrequency = DonationType == "Recurring" ? RecurringFrequency : null,
                DonationDate = DateTime.Now,
                CertificateNumber = certificateNumber
            };

            _context.Donations.Add(donation);
            await _context.SaveChangesAsync();

            // Redirect to tax certificate page
            return RedirectToPage("/TaxCertificate", new { id = donation.Id });
        }
    }
}
