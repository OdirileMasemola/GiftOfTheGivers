using System.Text.RegularExpressions;
using GiftOfTheGivers.UiTests.Infrastructure;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Xunit.Abstractions;

namespace GiftOfTheGivers.UiTests;

/// <summary>
/// End-to-end user flows in a real Chrome browser, each run at desktop (1920x1080) and mobile (390x844).
/// Needs the site running first (default http://localhost:5080, see UiTestSettings / README in docs).
/// Category "UI" keeps them out of the normal unit/integration runs and out of CI.
/// </summary>
[Trait("Category", "UI")]
public class UserFlowTests
{
    private readonly ITestOutputHelper _output;
    private static readonly string Stamp = DateTime.Now.ToString("MMdd-HHmmss");

    public UserFlowTests(ITestOutputHelper output) => _output = output;

    // ---------------------------------------------------------------- happy paths

    [Theory]
    [MemberData(nameof(Viewport.All), MemberType = typeof(Viewport))]
    public void Employee_SignsIn_OpensDashboard_AndPostsAnOperationUpdate(string viewport)
    {
        using var ui = UiSession.Start(viewport, _output);
        ui.Run(() =>
        {
            ui.SignIn(UiTestSettings.EmployeeEmail, UiTestSettings.EmployeePassword);

            ui.Open("/Dashboards/Employee");
            Assert.Contains("Employee Dashboard", ui.Find("h1").Text);
            ui.Screenshot("employee-dashboard");

            // Relief Operations is the employee page where updates are posted.
            var link = ui.Driver.FindElements(By.CssSelector("a[href='/Dashboards/Operations']")).FirstOrDefault(e => e.Displayed);
            if (link != null) ui.Click(link); else ui.Open("/Dashboards/Operations");
            ui.WaitForPath("/Dashboards/Operations");
            ui.Screenshot("operations-page");

            var title = $"{UiTestSettings.TestDataTag} - flood relief update {viewport} {Stamp}";
            ui.Type("#OperationType", title);
            ui.Type("#Location", $"{UiTestSettings.TestDataTag} location");
            ui.Type("#Notes", "Automated Selenium UI test data - safe to delete.");
            new SelectElement(ui.Find("#Status")).SelectByValue("Planning");
            ((IJavaScriptExecutor)ui.Driver).ExecuteScript(
                "arguments[0].value = arguments[1];", ui.Find("#StartDate"), DateTime.Today.ToString("yyyy-MM-dd"));
            ui.Screenshot("update-form-filled", ui.Find("#OperationType"));

            ui.Click(ui.Find("form:has(#OperationType) button[type=submit]"));
            ui.WaitForPath("/Dashboards/Operations");

            var row = ui.Wait.Until(d => d.FindElements(By.XPath($"//tr[contains(., '{title}')]")).FirstOrDefault());
            Assert.Contains("planning", row.Text, StringComparison.OrdinalIgnoreCase);
            ui.Screenshot("update-posted-and-listed", row);
        });
    }

    [Theory]
    [MemberData(nameof(Viewport.All), MemberType = typeof(Viewport))]
    public void Guest_DonatesAnonymously(string viewport)
    {
        using var ui = UiSession.Start(viewport, _output);
        ui.Run(() =>
        {
            ui.Open("/Donate");
            ui.Screenshot("donate-page-as-guest");

            // The quick amount buttons fill in the amount box.
            ui.Click(ui.Find("button.amount-button[data-amount='500']"));
            Assert.Equal("500", ui.Find("#Amount").GetAttribute("value"));

            new SelectElement(ui.Find("#Currency")).SelectByValue("ZAR");
            ui.Type("#Amount", "11.11");
            ui.Screenshot("guest-donation-filled", ui.Find("#Amount"));

            ui.Click(ui.Find("#donationForm button[type=submit]"));
            var success = ui.FindVisible(".alert-success");
            Assert.Contains("Donation recorded", success.Text);
            Assert.Contains("11.11 ZAR", success.Text);
            ui.Screenshot("guest-donation-recorded", success);
        });
    }

    [Theory]
    [MemberData(nameof(Viewport.All), MemberType = typeof(Viewport))]
    public void Donor_GivesInUsd_AndGetsATaxCertificate(string viewport)
    {
        using var ui = UiSession.Start(viewport, _output);
        ui.Run(() =>
        {
            ui.SignIn(UiTestSettings.DonorEmail, UiTestSettings.DonorPassword);

            ui.Open("/Donate");
            new SelectElement(ui.Find("#Currency")).SelectByValue("USD");
            ui.Type("#Amount", "25.50");
            ui.Screenshot("usd-donation-filled", ui.Find("#Amount"));
            ui.Click(ui.Find("#donationForm button[type=submit]"));

            var success = ui.FindVisible(".alert-success");
            Assert.Contains("25.50 USD", success.Text);
            var reference = Regex.Match(success.Text, @"Reference (PAY-[A-Z0-9-]+)").Groups[1].Value;
            Assert.False(string.IsNullOrEmpty(reference), "The success message should show a payment reference.");
            ui.Screenshot("usd-donation-recorded", success);

            ui.Click(ui.Find("a[href='/Dashboards/Donor']"));
            ui.WaitForPath("/Dashboards/Donor");
            Assert.Contains("Donor Dashboard", ui.Find("h1").Text);
            ui.Screenshot("donor-dashboard");

            IWebElement Row() => ui.Wait.Until(d => d.FindElements(By.XPath($"//tr[contains(., '{reference}')]")).FirstOrDefault());

            // A certificate is normally issued straight away; if not, request it from the row.
            var request = Row().FindElements(By.XPath(".//button[contains(., 'Request certificate')]")).FirstOrDefault();
            if (request != null)
            {
                ui.Click(request);
                ui.WaitForPage();
                ui.Screenshot("certificate-requested", ui.FindVisible(".alert-info"));
            }

            var certificate = Row().FindElement(By.CssSelector(".status-badge.active"));
            Assert.False(string.IsNullOrWhiteSpace(certificate.Text));
            Assert.Contains("completed", Row().Text, StringComparison.OrdinalIgnoreCase);   // the badge is shown in capitals by CSS
            ui.Log($"Donation {reference} has tax certificate {certificate.Text}");
            ui.Screenshot("tax-certificate-on-dashboard", Row());
        });
    }

    [Theory]
    [MemberData(nameof(Viewport.All), MemberType = typeof(Viewport))]
    public void Volunteer_Registers(string viewport)
    {
        using var ui = UiSession.Start(viewport, _output);
        ui.Run(() =>
        {
            ui.Open("/Volunteer");
            ui.Screenshot("volunteer-page");

            ui.Type("#FirstName", "Selenium");
            ui.Type("#LastName", $"Test {viewport}");
            ui.Type("#Email", $"selenium.test+{Guid.NewGuid().ToString()[..8]}@test.local");
            ui.Type("#PhoneNumber", "0000000000");
            ui.Click(ui.Find("#skill_logistics"));
            ui.Type("#Skills", "Automated Selenium UI test data - safe to delete.");
            ui.Click(ui.Find("#avail_weekends"));
            ui.Screenshot("volunteer-form-filled", ui.Find("#avail_weekends"));

            ui.Click(ui.Find("#volunteerForm button[type=submit]"));
            ui.WaitForPath("/VolunteerConfirmation");
            Assert.Contains("Thank You for Volunteering", ui.Find("h1").Text);
            ui.Screenshot("volunteer-confirmation");
        });
    }

    [Theory]
    [MemberData(nameof(Viewport.All), MemberType = typeof(Viewport))]
    public void SignOut_EndsTheSession(string viewport)
    {
        using var ui = UiSession.Start(viewport, _output);
        ui.Run(() =>
        {
            ui.SignIn(UiTestSettings.DonorEmail, UiTestSettings.DonorPassword);
            ui.Open("/Dashboards/Donor");
            ui.Screenshot("signed-in-dashboard");

            ui.SignOut();
            ui.Wait.Until(_ => ui.PathAndQuery == "/");
            ui.Screenshot("after-sign-out");

            // The auth cookie must be gone, so the dashboard asks for a login again.
            ui.Open("/Dashboards/Donor");
            ui.WaitForPath("/Login");
            Assert.Contains("ReturnUrl", ui.PathAndQuery);
            ui.Screenshot("dashboard-needs-login-again");
        });
    }

    // ---------------------------------------------------------------- negative / error paths

    [Theory]
    [MemberData(nameof(Viewport.All), MemberType = typeof(Viewport))]
    public void WrongPassword_ShowsAnError(string viewport)
    {
        using var ui = UiSession.Start(viewport, _output);
        ui.Run(() =>
        {
            ui.SubmitLogin(UiTestSettings.DonorEmail, "Selenium-Test-Wrong-Password-1!");

            var error = ui.FindVisible(".alert-danger");
            Assert.Contains("Invalid email or password", error.Text);
            Assert.StartsWith("/Login", ui.PathAndQuery);
            ui.Screenshot("wrong-password-error", error);

            ui.Open("/Dashboards/Donor");
            ui.WaitForPath("/Login");
            ui.Screenshot("still-not-signed-in");
        });
    }

    [Theory]
    [MemberData(nameof(Viewport.All), MemberType = typeof(Viewport))]
    public void InvalidAmount_IsRejected(string viewport)
    {
        using var ui = UiSession.Start(viewport, _output);
        ui.Run(() =>
        {
            ui.Open("/Donate");

            // 1) Zero is stopped in the browser before anything is sent.
            ui.Type("#Amount", "0");
            ui.Click(ui.Find("#donationForm button[type=submit]"));
            ui.Wait.Until(_ => ui.Find("#Amount").GetAttribute("class")!.Contains("is-invalid"));
            Assert.Empty(ui.Driver.FindElements(By.CssSelector(".alert-success")));
            ui.Screenshot("zero-amount-blocked", ui.Find("#Amount"));

            // 2) Over the 1,000,000 limit is also blocked.
            ui.Type("#Amount", "2000000");
            ui.Click(ui.Find("#donationForm button[type=submit]"));
            Assert.Empty(ui.Driver.FindElements(By.CssSelector(".alert-success")));
            ui.Screenshot("over-limit-amount-blocked", ui.Find("#Amount"));

            // 3) Skip the browser checks (form.submit() does not fire the submit handlers)
            //    to prove the server rejects a negative amount on its own.
            ui.Type("#Amount", "-5");
            ((IJavaScriptExecutor)ui.Driver).ExecuteScript("document.getElementById('donationForm').submit();");
            ui.WaitForPage();
            var serverError = ui.FindVisible(".field-validation-error");
            Assert.Contains("greater than 0", serverError.Text);
            Assert.Empty(ui.Driver.FindElements(By.CssSelector(".alert-success")));
            ui.Screenshot("negative-amount-rejected-by-server", serverError);
        });
    }

    [Theory]
    [MemberData(nameof(Viewport.All), MemberType = typeof(Viewport))]
    public void Donor_OnEmployeePages_GetsAccessDenied(string viewport)
    {
        using var ui = UiSession.Start(viewport, _output);
        ui.Run(() =>
        {
            ui.SignIn(UiTestSettings.DonorEmail, UiTestSettings.DonorPassword);

            ui.Open("/Dashboards/Employee");
            ui.WaitForPath("/AccessDenied");
            Assert.Contains("Access Denied", ui.Find("h1").Text);
            ui.Screenshot("donor-denied-employee-dashboard");

            // Operations was missing its role check before this phase.
            ui.Open("/Dashboards/Operations");
            ui.WaitForPath("/AccessDenied");
            Assert.Contains("Access Denied", ui.Find("h1").Text);
            ui.Screenshot("donor-denied-operations");
        });
    }
}
