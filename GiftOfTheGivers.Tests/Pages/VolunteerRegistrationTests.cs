using System.ComponentModel.DataAnnotations;
using GiftOfTheGivers.Data;
using GiftOfTheGivers.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PublicVolunteerForm = GiftOfTheGivers.Pages.VolunteerModel;

namespace GiftOfTheGivers.Tests.Pages;

public class VolunteerRegistrationTests
{
    private readonly Mock<ILogger<PublicVolunteerForm>> _logger = new();

    private PublicVolunteerForm NewForm(ApplicationDbContext db) =>
        new PublicVolunteerForm(db, _logger.Object)
        {
            FirstName = "  Lerato ",
            LastName = " Mokoena ",
            Email = " lerato@example.com ",
            PhoneNumber = "0821234567",
            Availability = "Weekends only",
            SelectedSkills = new[] { "Medical", " Logistics " },
            Skills = "First aid trained"
        }.WithContext();

    [Fact]
    public async Task Post_NewApplicant_CreatesVolunteerUserAndPendingApplication()
    {
        using var db = TestDb.Create();
        var form = NewForm(db);

        var result = await form.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/VolunteerConfirmation", redirect.PageName);

        var volunteer = Assert.Single(db.Volunteers.Include(v => v.User));
        Assert.Equal("Pending", volunteer.Status);
        Assert.Equal("Medical, Logistics, First aid trained", volunteer.Skills);
        Assert.Equal("Weekends only", volunteer.Availability);
        Assert.Equal(volunteer.VolunteerId, redirect.RouteValues!["id"]);

        var user = volunteer.User!;
        Assert.Equal("Lerato", user.FirstName);
        Assert.Equal("lerato@example.com", user.Email);
        Assert.Equal("Volunteer", user.Role);
        Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash));
    }

    [Fact]
    public async Task Post_WithoutAnySkills_IsRejected()
    {
        using var db = TestDb.Create();
        var form = NewForm(db);
        form.SelectedSkills = Array.Empty<string>();
        form.Skills = "   ";

        var result = await form.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(form.ModelState.ContainsKey(nameof(PublicVolunteerForm.Skills)));
        Assert.Empty(db.Volunteers);
    }

    [Fact]
    public async Task Post_EmptyOtherSkillsBox_IsAcceptedWhenASkillIsTicked()
    {
        // An empty textarea binds as null, which used to crash the form.
        using var db = TestDb.Create();
        var form = NewForm(db);
        form.Skills = null;
        form.PhoneNumber = null;

        var result = await form.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Medical, Logistics", Assert.Single(db.Volunteers).Skills);
    }

    [Fact]
    public async Task Post_EmailThatAlreadyApplied_IsRejectedIgnoringCase()
    {
        using var db = TestDb.Create();
        var existing = TestDb.AddUser(db, "lerato@example.com", role: "Volunteer");
        TestDb.AddVolunteer(db, existing);
        var form = NewForm(db);
        form.Email = "LERATO@EXAMPLE.COM";

        var result = await form.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Contains("already exists", form.ModelState[string.Empty]!.Errors[0].ErrorMessage);
        Assert.Equal(1, db.Volunteers.Count());
    }

    [Fact]
    public async Task Post_ExistingDonor_ApplicationIsLinkedToTheirAccountAndRoleIsKept()
    {
        using var db = TestDb.Create();
        var donor = TestDb.AddUser(db, "lerato@example.com", role: "Donor");
        var form = NewForm(db);

        await form.OnPostAsync();

        var volunteer = Assert.Single(db.Volunteers);
        Assert.Equal(donor.UserId, volunteer.UserId);
        Assert.Equal(1, db.Users.Count());
        Assert.Equal("Donor", db.Users.Single().Role);
        Assert.Equal("Mokoena", db.Users.Single().LastName);
    }

    [Fact]
    public async Task Post_WhenSaveFails_ShowsFriendlyErrorAndLogsIt()
    {
        var db = new Mock<ApplicationDbContext>(TestDb.NewOptions()) { CallBase = true };
        db.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Database unavailable"));
        var form = NewForm(db.Object);

        var result = await form.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Contains("could not submit your application", form.ModelState[string.Empty]!.Errors[0].ErrorMessage);
        _logger.VerifyLogged(LogLevel.Error, Times.Once());
    }

    [Theory]
    [InlineData("", "Mokoena", "lerato@example.com", "Weekends only", "First name is required.")]
    [InlineData("Lerato", "", "lerato@example.com", "Weekends only", "Last name is required.")]
    [InlineData("Lerato", "Mokoena", "not-an-email", "Weekends only", "Enter a valid email address.")]
    [InlineData("Lerato", "Mokoena", "lerato@example.com", "", "Select your availability.")]
    public void Validation_RequiredFieldsAndEmailFormat_AreEnforced(
        string firstName, string lastName, string email, string availability, string expectedError)
    {
        using var db = TestDb.Create();
        var form = new PublicVolunteerForm(db, _logger.Object)
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Availability = availability
        };

        var errors = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(form, new ValidationContext(form), errors, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage == expectedError);
    }

    [Fact]
    public void Validation_CompleteApplication_Passes()
    {
        using var db = TestDb.Create();
        var form = NewForm(db);

        var errors = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(form, new ValidationContext(form), errors, validateAllProperties: true));
        Assert.Empty(errors);
    }
}
