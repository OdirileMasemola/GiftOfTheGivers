using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets for all 10 Azure tables
        public DbSet<User> Users { get; set; }
        public DbSet<Donation> Donations { get; set; }
        public DbSet<Volunteer> Volunteers { get; set; }
        public DbSet<ReliefRequest> ReliefRequests { get; set; }
        public DbSet<ReliefOperation> ReliefOperations { get; set; }
        public DbSet<DonationAllocation> DonationAllocations { get; set; }
        public DbSet<DonationSchedule> DonationSchedules { get; set; }
        public DbSet<TaxCertificate> TaxCertificates { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<VolunteerAssignment> VolunteerAssignments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Users table
            modelBuilder.Entity<User>()
                .HasKey(u => u.UserId);
            modelBuilder.Entity<User>()
                .ToTable("Users");
            modelBuilder.Entity<User>()
                .Property(u => u.UserId)
                .ValueGeneratedOnAdd();

            // Configure Donations table
            modelBuilder.Entity<Donation>()
                .HasKey(d => d.DonationId);
            modelBuilder.Entity<Donation>()
                .ToTable("Donations");
            modelBuilder.Entity<Donation>()
                .Property(d => d.Amount)
                .HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Donation>()
                .HasOne(d => d.User)
                .WithMany(u => u.Donations)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Volunteers table
            modelBuilder.Entity<Volunteer>()
                .HasKey(v => v.VolunteerId);
            modelBuilder.Entity<Volunteer>()
                .ToTable("Volunteers");
            modelBuilder.Entity<Volunteer>()
                .HasOne(v => v.User)
                .WithMany(u => u.Volunteers)
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure ReliefRequests table
            modelBuilder.Entity<ReliefRequest>()
                .HasKey(r => r.ReliefRequestId);
            modelBuilder.Entity<ReliefRequest>()
                .ToTable("ReliefRequests");
            modelBuilder.Entity<ReliefRequest>()
                .HasOne(r => r.RequestedByUser)
                .WithMany(u => u.ReliefRequests)
                .HasForeignKey(r => r.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure ReliefOperations table
            modelBuilder.Entity<ReliefOperation>()
                .HasKey(o => o.ReliefOperationId);
            modelBuilder.Entity<ReliefOperation>()
                .ToTable("ReliefOperations");
            modelBuilder.Entity<ReliefOperation>()
                .HasOne(o => o.ReliefRequest)
                .WithMany(r => r.ReliefOperations)
                .HasForeignKey(o => o.ReliefRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure DonationAllocations table
            modelBuilder.Entity<DonationAllocation>()
                .HasKey(da => da.DonationAllocationId);
            modelBuilder.Entity<DonationAllocation>()
                .ToTable("DonationAllocations");
            modelBuilder.Entity<DonationAllocation>()
                .Property(da => da.AmountAllocated)
                .HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<DonationAllocation>()
                .HasOne(da => da.Donation)
                .WithMany(d => d.DonationAllocations)
                .HasForeignKey(da => da.DonationId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<DonationAllocation>()
                .HasOne(da => da.ReliefOperation)
                .WithMany(o => o.DonationAllocations)
                .HasForeignKey(da => da.ReliefOperationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure DonationSchedules table
            modelBuilder.Entity<DonationSchedule>()
                .HasKey(ds => ds.DonationScheduleId);
            modelBuilder.Entity<DonationSchedule>()
                .ToTable("DonationSchedules");
            modelBuilder.Entity<DonationSchedule>()
                .Property(ds => ds.Amount)
                .HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<DonationSchedule>()
                .HasOne(ds => ds.Donor)
                .WithMany(u => u.DonationSchedules)
                .HasForeignKey(ds => ds.DonorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure TaxCertificates table
            modelBuilder.Entity<TaxCertificate>()
                .HasKey(tc => tc.TaxCertificateId);
            modelBuilder.Entity<TaxCertificate>()
                .ToTable("TaxCertificates");
            modelBuilder.Entity<TaxCertificate>()
                .Property(tc => tc.CertificateAmount)
                .HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<TaxCertificate>()
                .HasOne(tc => tc.Donation)
                .WithMany(d => d.TaxCertificates)
                .HasForeignKey(tc => tc.DonationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure AuditLogs table
            modelBuilder.Entity<AuditLog>()
                .HasKey(al => al.AuditLogId);
            modelBuilder.Entity<AuditLog>()
                .ToTable("AuditLogs");
            modelBuilder.Entity<AuditLog>()
                .HasOne(al => al.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(al => al.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure VolunteerAssignments table
            modelBuilder.Entity<VolunteerAssignment>()
                .HasKey(va => va.VolunteerAssignmentId);
            modelBuilder.Entity<VolunteerAssignment>()
                .ToTable("VolunteerAssignments");
            modelBuilder.Entity<VolunteerAssignment>()
                .HasOne(va => va.Volunteer)
                .WithMany(v => v.VolunteerAssignments)
                .HasForeignKey(va => va.VolunteerId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<VolunteerAssignment>()
                .HasOne(va => va.ReliefOperation)
                .WithMany(o => o.VolunteerAssignments)
                .HasForeignKey(va => va.ReliefOperationId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    /// <summary>
    /// Custom Users table entity (not ASP.NET Identity)
    /// </summary>
    public class User
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = string.Empty; // Employee, Donor, Volunteer, Admin
        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public ICollection<Donation> Donations { get; set; } = new List<Donation>();
        public ICollection<Volunteer> Volunteers { get; set; } = new List<Volunteer>();
        public ICollection<ReliefRequest> ReliefRequests { get; set; } = new List<ReliefRequest>();
        public ICollection<DonationSchedule> DonationSchedules { get; set; } = new List<DonationSchedule>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }

    /// <summary>
    /// Donations table entity - represents a donation from a User
    /// </summary>
    public class Donation
    {
        public int DonationId { get; set; }
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime DonationDate { get; set; }
        public string PaymentStatus { get; set; } = string.Empty; // Pending, Completed, Failed, etc.
        public string? PaymentReference { get; set; }

        // Foreign key and navigation properties
        public User? User { get; set; }
        public ICollection<DonationAllocation> DonationAllocations { get; set; } = new List<DonationAllocation>();
        public ICollection<TaxCertificate> TaxCertificates { get; set; } = new List<TaxCertificate>();
    }

    /// <summary>
    /// Volunteers table entity - represents a volunteering profile linked to a User
    /// </summary>
    public class Volunteer
    {
        public int VolunteerId { get; set; }
        public int UserId { get; set; }
        public string? Skills { get; set; }
        public string Availability { get; set; } = string.Empty;
        public DateTime RegistrationDate { get; set; }
        public string Status { get; set; } = string.Empty; // Pending, Approved, Active, Inactive, etc.

        // Foreign key and navigation properties
        public User? User { get; set; }
        public ICollection<VolunteerAssignment> VolunteerAssignments { get; set; } = new List<VolunteerAssignment>();
    }

    /// <summary>
    /// ReliefRequests table entity - represents a request for relief assistance
    /// </summary>
    public class ReliefRequest
    {
        public int ReliefRequestId { get; set; }
        public int RequestedByUserId { get; set; }
        public string RequestType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public string Priority { get; set; } = string.Empty; // Low, Medium, High, Critical
        public string Status { get; set; } = string.Empty; // Pending, Approved, In Progress, Completed, Rejected

        // Foreign key and navigation properties
        public User? RequestedByUser { get; set; }
        public ICollection<ReliefOperation> ReliefOperations { get; set; } = new List<ReliefOperation>();
    }

    /// <summary>
    /// ReliefOperations table entity - represents the execution/management of a relief operation
    /// </summary>
    public class ReliefOperation
    {
        public int ReliefOperationId { get; set; }
        public int ReliefRequestId { get; set; }
        public string OperationType { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Planning, Active, Completed, Paused
        public string? Notes { get; set; }

        // Foreign key and navigation properties
        public ReliefRequest? ReliefRequest { get; set; }
        public ICollection<DonationAllocation> DonationAllocations { get; set; } = new List<DonationAllocation>();
        public ICollection<VolunteerAssignment> VolunteerAssignments { get; set; } = new List<VolunteerAssignment>();
    }

    /// <summary>
    /// DonationAllocations table entity - represents allocation of a donation to a relief operation
    /// </summary>
    public class DonationAllocation
    {
        public int DonationAllocationId { get; set; }
        public int DonationId { get; set; }
        public int ReliefOperationId { get; set; }
        public decimal AmountAllocated { get; set; }
        public DateTime AllocationDate { get; set; }

        // Foreign key and navigation properties
        public Donation? Donation { get; set; }
        public ReliefOperation? ReliefOperation { get; set; }
    }

    /// <summary>
    /// DonationSchedules table entity - represents recurring donation schedules
    /// </summary>
    public class DonationSchedule
    {
        public int DonationScheduleId { get; set; }
        public int DonorId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty; // Daily, Weekly, Monthly, Quarterly, Annually
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; } = string.Empty; // Active, Paused, Cancelled
        public DateTime CreatedAt { get; set; }

        // Foreign key and navigation properties
        public User? Donor { get; set; }
    }

    /// <summary>
    /// TaxCertificates table entity - represents tax certificates issued for donations
    /// </summary>
    public class TaxCertificate
    {
        public int TaxCertificateId { get; set; }
        public int DonationId { get; set; }
        public string CertificateNumber { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public decimal CertificateAmount { get; set; }
        public DateTime CreatedAt { get; set; }

        // Foreign key and navigation properties
        public Donation? Donation { get; set; }
    }

    /// <summary>
    /// AuditLogs table entity - represents audit trail for system actions
    /// </summary>
    public class AuditLog
    {
        public int AuditLogId { get; set; }
        public int? UserId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string? EntityName { get; set; }
        public int? EntityId { get; set; }
        public string? Description { get; set; }
        public string? IPAddress { get; set; }
        public DateTime CreatedAt { get; set; }

        // Foreign key and navigation properties
        public User? User { get; set; }
    }

    /// <summary>
    /// VolunteerAssignments table entity - represents assignment of volunteers to relief operations
    /// </summary>
    public class VolunteerAssignment
    {
        public int VolunteerAssignmentId { get; set; }
        public int VolunteerId { get; set; }
        public int ReliefOperationId { get; set; }
        public DateTime AssignedDate { get; set; }

        // Foreign key and navigation properties
        public Volunteer? Volunteer { get; set; }
        public ReliefOperation? ReliefOperation { get; set; }
    }
}
