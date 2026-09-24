using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages.Dashboards
{
    /// <summary>
    /// Employee donation management and reporting: DB-side stats, search/filter, and pagination.
    /// </summary>
    [Authorize(Roles = "Employee")]
    public class DonationsModel : PageModel
    {
        private const int DefaultPageSize = 15;
        private static readonly string[] KnownStatuses = { "Completed", "Pending", "Failed" };

        private readonly ApplicationDbContext _context;

        public DonationsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<DonationRow> Donations { get; set; } = new();
        public List<AllocationRow> RecentAllocations { get; set; } = new();
        public List<ScheduleRow> ActiveSchedules { get; set; } = new();

        public decimal ThisMonthZar { get; set; }
        public decimal AllTimeZar { get; set; }
        public int CompletedCount { get; set; }
        public int PendingCount { get; set; }
        public int FailedCount { get; set; }
        public int RecurringCount { get; set; }
        public int FilteredTotal { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = DefaultPageSize;
        public int TotalPages { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Status { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? From { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? To { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageIndex { get; set; } = 1;

        public async Task OnGetAsync()
        {
            PageNumber = PageIndex < 1 ? 1 : PageIndex;
            PageSize = DefaultPageSize;

            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var completedZar = _context.Donations.AsNoTracking()
                .Where(d => d.Currency == "ZAR"
                    && d.PaymentStatus == "Completed");

            ThisMonthZar = await completedZar
                .Where(d => d.DonationDate >= monthStart)
                .SumAsync(d => (decimal?)d.Amount) ?? 0m;

            AllTimeZar = await completedZar
                .SumAsync(d => (decimal?)d.Amount) ?? 0m;

            CompletedCount = await _context.Donations.AsNoTracking()
                .CountAsync(d => d.PaymentStatus == "Completed");

            PendingCount = await _context.Donations.AsNoTracking()
                .CountAsync(d => d.PaymentStatus == "Pending");

            FailedCount = await _context.Donations.AsNoTracking()
                .CountAsync(d => d.PaymentStatus == "Failed");

            RecurringCount = await _context.DonationSchedules.AsNoTracking()
                .CountAsync(ds => ds.Status == "Active");

            var query = BuildFilteredQuery();

            FilteredTotal = await query.CountAsync();
            TotalPages = Math.Max(1, (int)Math.Ceiling(FilteredTotal / (double)PageSize));
            if (PageNumber > TotalPages)
            {
                PageNumber = TotalPages;
            }

            Donations = await query
                .OrderByDescending(d => d.DonationDate)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .Select(d => new DonationRow
                {
                    DonationId = d.DonationId,
                    DonationDate = d.DonationDate,
                    Amount = d.Amount,
                    Currency = d.Currency,
                    PaymentStatus = d.PaymentStatus,
                    PaymentReference = d.PaymentReference,
                    DonorName = d.User == null
                        ? "Unknown"
                        : ((d.User.FirstName ?? "") + " " + (d.User.LastName ?? "")).Trim(),
                    DonorEmail = d.User != null ? d.User.Email : null,
                    HasCertificate = d.TaxCertificates.Any()
                })
                .ToListAsync();

            RecentAllocations = await _context.DonationAllocations.AsNoTracking()
                .OrderByDescending(a => a.AllocationDate)
                .Take(8)
                .Select(a => new AllocationRow
                {
                    AllocationDate = a.AllocationDate,
                    AmountAllocated = a.AmountAllocated,
                    DonationId = a.DonationId,
                    OperationType = a.ReliefOperation != null ? a.ReliefOperation.OperationType : "Unknown",
                    OperationLocation = a.ReliefOperation != null ? a.ReliefOperation.Location : "Unknown",
                    DonorName = a.Donation != null && a.Donation.User != null
                        ? ((a.Donation.User.FirstName ?? "") + " " + (a.Donation.User.LastName ?? "")).Trim()
                        : "Unknown"
                })
                .ToListAsync();

            ActiveSchedules = await _context.DonationSchedules.AsNoTracking()
                .Where(s => s.Status == "Active")
                .OrderByDescending(s => s.CreatedAt)
                .Take(8)
                .Select(s => new ScheduleRow
                {
                    Amount = s.Amount,
                    Currency = s.Currency,
                    Frequency = s.Frequency,
                    StartDate = s.StartDate,
                    Status = s.Status,
                    DonorName = s.Donor == null
                        ? "Unknown"
                        : ((s.Donor.FirstName ?? "") + " " + (s.Donor.LastName ?? "")).Trim(),
                    DonorEmail = s.Donor != null ? s.Donor.Email : null
                })
                .ToListAsync();
        }

        private IQueryable<Donation> BuildFilteredQuery()
        {
            var query = _context.Donations.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(Status)
                && KnownStatuses.Contains(Status, StringComparer.OrdinalIgnoreCase))
            {
                var status = KnownStatuses.First(s =>
                    s.Equals(Status, StringComparison.OrdinalIgnoreCase));
                query = query.Where(d => d.PaymentStatus == status);
            }

            if (From.HasValue)
            {
                var fromDate = From.Value.Date;
                query = query.Where(d => d.DonationDate >= fromDate);
            }

            if (To.HasValue)
            {
                var toExclusive = To.Value.Date.AddDays(1);
                query = query.Where(d => d.DonationDate < toExclusive);
            }

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var term = Search.Trim();
                query = query.Where(d =>
                    (d.PaymentReference != null && d.PaymentReference.Contains(term))
                    || (d.User != null && d.User.Email != null && d.User.Email.Contains(term))
                    || (d.User != null && d.User.FirstName != null && d.User.FirstName.Contains(term))
                    || (d.User != null && d.User.LastName != null && d.User.LastName.Contains(term)));
            }

            return query;
        }

        public class DonationRow
        {
            public int DonationId { get; set; }
            public DateTime DonationDate { get; set; }
            public decimal Amount { get; set; }
            public string Currency { get; set; } = string.Empty;
            public string PaymentStatus { get; set; } = string.Empty;
            public string? PaymentReference { get; set; }
            public string DonorName { get; set; } = "Unknown";
            public string? DonorEmail { get; set; }
            public bool HasCertificate { get; set; }
        }

        public class AllocationRow
        {
            public DateTime AllocationDate { get; set; }
            public decimal AmountAllocated { get; set; }
            public int DonationId { get; set; }
            public string OperationType { get; set; } = string.Empty;
            public string OperationLocation { get; set; } = string.Empty;
            public string DonorName { get; set; } = "Unknown";
        }

        public class ScheduleRow
        {
            public decimal Amount { get; set; }
            public string Currency { get; set; } = string.Empty;
            public string Frequency { get; set; } = string.Empty;
            public DateTime StartDate { get; set; }
            public string Status { get; set; } = string.Empty;
            public string DonorName { get; set; } = "Unknown";
            public string? DonorEmail { get; set; }
        }
    }
}
