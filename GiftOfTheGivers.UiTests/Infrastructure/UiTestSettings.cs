namespace GiftOfTheGivers.UiTests.Infrastructure;

/// <summary>
/// Where the UI tests point and which accounts they use.
/// Everything can be overridden with environment variables, so nothing secret is committed.
/// The defaults are the seeded demo accounts that the app itself documents on the login page.
/// </summary>
public static class UiTestSettings
{
    /// <summary>One folder per test run, so screenshots from different runs never mix.</summary>
    public static readonly string RunStamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

    /// <summary>Site under test. Default is a local Release run (dotnet run / published build on port 5080).</summary>
    public static string BaseUrl => (Env("GOTG_UI_BASE_URL") ?? "http://localhost:5080").TrimEnd('/');

    public static string DonorEmail => Env("GOTG_UI_DONOR_EMAIL") ?? "donor@test.local";
    public static string DonorPassword => Env("GOTG_UI_DONOR_PASSWORD") ?? "Donor@123";
    public static string EmployeeEmail => Env("GOTG_UI_EMPLOYEE_EMAIL") ?? "employee@test.local";
    public static string EmployeePassword => Env("GOTG_UI_EMPLOYEE_PASSWORD") ?? "Employee@123";

    /// <summary>Set GOTG_UI_HEADED=1 to watch the browser; by default Chrome runs headless.</summary>
    public static bool Headed => Env("GOTG_UI_HEADED") == "1";

    /// <summary>Root folder for the step screenshots (GOTG_UI_SCREENSHOTS, or screenshots/ next to the project).</summary>
    public static string ScreenshotRoot =>
        Path.Combine(Env("GOTG_UI_SCREENSHOTS") ?? Path.Combine(ProjectFolder(), "screenshots"), RunStamp);

    /// <summary>Marks anything the tests create so it is easy to spot (and delete) in the database.</summary>
    public const string TestDataTag = "Selenium Test";

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string ProjectFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.EnumerateFiles("GiftOfTheGivers.UiTests.csproj").Any())
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
