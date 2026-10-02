using GiftOfTheGivers.Data;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Tests.TestSupport;

/// <summary>
/// Builds an isolated in-memory database for each test, plus a few seed helpers.
/// </summary>
internal static class TestDb
{
    public static DbContextOptions<ApplicationDbContext> NewOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    public static ApplicationDbContext Create() => new(NewOptions());

    public static User AddUser(ApplicationDbContext db, string email, string role = "Donor", string password = "Password@123")
    {
        var user = new User
        {
            FirstName = email.Split('@')[0],
            LastName = "Tester",
            Email = email,
            PasswordHash = SeedData.HashPassword(password),
            Role = role,
            CreatedAt = DateTime.Now
        };

        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static Donation AddDonation(
        ApplicationDbContext db,
        User donor,
        decimal amount,
        string currency = "ZAR",
        string status = "Completed",
        DateTime? date = null)
    {
        var donation = new Donation
        {
            UserId = donor.UserId,
            Amount = amount,
            Currency = currency,
            PaymentStatus = status,
            DonationDate = date ?? DateTime.Now,
            PaymentReference = $"PAY-TEST-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}"
        };

        db.Donations.Add(donation);
        db.SaveChanges();
        return donation;
    }

    public static ReliefOperation AddOperation(ApplicationDbContext db, string location, string status = "Active")
    {
        var operation = new ReliefOperation
        {
            ReliefRequestId = 1,
            OperationType = "Food Parcels",
            Location = location,
            Status = status,
            StartDate = DateTime.Today
        };

        db.ReliefOperations.Add(operation);
        db.SaveChanges();
        return operation;
    }

    public static Volunteer AddVolunteer(ApplicationDbContext db, User user, string status = "Pending", string skills = "Logistics")
    {
        var volunteer = new Volunteer
        {
            UserId = user.UserId,
            Skills = skills,
            Availability = "Weekends only",
            RegistrationDate = DateTime.Now,
            Status = status
        };

        db.Volunteers.Add(volunteer);
        db.SaveChanges();
        return volunteer;
    }
}
