using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Donation> Donations { get; set; }
        public DbSet<Volunteer> Volunteers { get; set; }
        public DbSet<ReliefProject> ReliefProjects { get; set; }
        public DbSet<ProjectUpdate> ProjectUpdates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Donation.Amount to handle currency values properly
            modelBuilder.Entity<Donation>()
                .Property(d => d.Amount)
                .HasColumnType("decimal(18, 2)");
        }
    }

    public class Donation
    {
        public int Id { get; set; }
        public string DonorName { get; set; } = string.Empty;
        public string DonorEmail { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "ZAR"; // ZAR, USD, EUR
        public string DonationType { get; set; } = "OneTime"; // OneTime or Recurring
        public string? RecurringFrequency { get; set; } // Monthly, Quarterly
        public DateTime DonationDate { get; set; }
        public string CertificateNumber { get; set; } = string.Empty;
    }

    public class Volunteer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Skills { get; set; } = string.Empty;
        public string Availability { get; set; } = string.Empty;
        public DateTime RegistrationDate { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Approved, Active
    }

    public class ReliefProject
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Planning"; // Planning, Active, Completed
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class ProjectUpdate
    {
        public int Id { get; set; }
        public int ReliefProjectId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime UpdateDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;

        public ReliefProject? ReliefProject { get; set; }
    }
}
