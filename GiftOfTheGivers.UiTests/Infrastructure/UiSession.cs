using System.Runtime.CompilerServices;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Chromium;
using OpenQA.Selenium.Support.UI;
using Xunit.Abstractions;

namespace GiftOfTheGivers.UiTests.Infrastructure;

/// <summary>
/// One Chrome browser for one test at one screen size.
/// Wraps the bits every test needs: opening pages, waiting, signing in/out and
/// saving a numbered screenshot at each key step (the screenshots are the run evidence).
/// </summary>
public sealed class UiSession : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _folder;
    private int _step;

    public IWebDriver Driver { get; }
    public Viewport Viewport { get; }
    public WebDriverWait Wait { get; }

    private UiSession(Viewport viewport, string testName, ITestOutputHelper output)
    {
        Viewport = viewport;
        _output = output;
        _folder = Path.Combine(UiTestSettings.ScreenshotRoot, viewport.Name, testName);
        Directory.CreateDirectory(_folder);

        var options = new ChromeOptions();
        if (!UiTestSettings.Headed)
        {
            options.AddArgument("--headless=new");
        }
        options.AddArgument("--no-first-run");
        options.AddArgument("--no-default-browser-check");
        options.AddArgument("--disable-search-engine-choice-screen");
        options.AddArgument("--lang=en-ZA");
        // Stop Chrome's password manager pop-ups from covering the page in screenshots.
        options.AddUserProfilePreference("credentials_enable_service", false);
        options.AddUserProfilePreference("profile.password_manager_enabled", false);
        options.AddUserProfilePreference("profile.password_manager_leak_detection", false);

        if (viewport.IsMobile)
        {
            options.EnableMobileEmulation(new ChromiumMobileEmulationDeviceSettings
            {
                Width = viewport.Width,
                Height = viewport.Height,
                PixelRatio = 3,
                EnableTouchEvents = true,
                UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1"
            });
        }
        else
        {
            options.AddArgument($"--window-size={viewport.Width},{viewport.Height}");
        }

        // Selenium Manager finds (or downloads) the matching ChromeDriver for the installed Chrome.
        Driver = new ChromeDriver(options);
        Driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(60);
        Wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(20));
        Wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

        var caps = ((ChromeDriver)Driver).Capabilities;
        Log($"Chrome {caps.GetCapability("browserVersion")} | {viewport.Name} | {UiTestSettings.BaseUrl}");
        Log($"Screenshots: {_folder}");
    }

    public static UiSession Start(string viewportName, ITestOutputHelper output, [CallerMemberName] string testName = "") =>
        new(Viewport.Named(viewportName), testName, output);

    /// <summary>
    /// Runs the test body; if anything fails, a "FAILED" screenshot is saved before the error is rethrown.
    /// </summary>
    public void Run(Action body)
    {
        try
        {
            body();
        }
        catch
        {
            try { Screenshot("FAILED"); } catch { /* the browser may already be gone */ }
            throw;
        }
    }

    public void Open(string path)
    {
        Driver.Navigate().GoToUrl(UiTestSettings.BaseUrl + path);
        WaitForPage();
    }

    public string PathAndQuery => new Uri(Driver.Url).PathAndQuery;

    public IWebElement Find(string css) => Wait.Until(d => d.FindElement(By.CssSelector(css)));

    public IWebElement FindVisible(string css) =>
        Wait.Until(d => d.FindElements(By.CssSelector(css)).FirstOrDefault(e => e.Displayed));

    public void Type(string css, string text)
    {
        var box = Find(css);
        box.Clear();
        box.SendKeys(text);
    }

    public void Click(IWebElement element)
    {
        ScrollTo(element);
        try
        {
            element.Click();
        }
        catch (ElementClickInterceptedException)
        {
            // A sticky header can sit over the element on small screens; a script click is the same form action.
            ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].click();", element);
        }
    }

    public void ScrollTo(IWebElement element) =>
        ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].scrollIntoView({block: 'center'});", element);

    public void WaitForPath(string startsWith)
    {
        Wait.Until(_ => PathAndQuery.StartsWith(startsWith, StringComparison.OrdinalIgnoreCase));
        WaitForPage();
    }

    public void WaitForPage() =>
        Wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState")?.ToString() == "complete");

    /// <summary>Fills in the login form and submits it (does not assert the result).</summary>
    public void SubmitLogin(string email, string password)
    {
        Open("/Login");
        Type("#Input_Email", email);
        Type("#Input_Password", password);
        Screenshot($"login-form-filled-{email.Split('@')[0]}");
        Click(Find("form:has(#Input_Email) button[type=submit]"));
        WaitForPage();
    }

    /// <summary>Signs in and waits until the app has left the login page.</summary>
    public void SignIn(string email, string password)
    {
        SubmitLogin(email, password);
        Wait.Until(_ => !PathAndQuery.StartsWith("/Login", StringComparison.OrdinalIgnoreCase));
        WaitForPage();
        Log($"Signed in as {email}, now on {PathAndQuery}");
    }

    /// <summary>
    /// Clicks whichever Sign out / Logout button is visible at this screen size
    /// (dashboard sidebar, top navigation, or the top navigation behind the mobile menu button).
    /// </summary>
    public void SignOut()
    {
        var button = Driver.FindElements(By.CssSelector(".sidebar-sign-out, .nav-logout")).FirstOrDefault(e => e.Displayed);
        if (button == null)
        {
            var toggle = Driver.FindElements(By.Id("navbarToggle")).FirstOrDefault(e => e.Displayed);
            if (toggle != null)
            {
                Click(toggle);
                Screenshot("mobile-menu-open");
            }
            button = FindVisible(".sidebar-sign-out, .nav-logout");
        }
        Click(button);
        WaitForPage();
    }

    /// <summary>Saves a numbered screenshot of what the browser shows right now.</summary>
    public string Screenshot(string label, IWebElement? focus = null)
    {
        if (focus != null)
        {
            ScrollTo(focus);
        }
        _step++;
        var file = Path.Combine(_folder, $"{_step:00}-{Safe(label)}.png");
        ((ITakesScreenshot)Driver).GetScreenshot().SaveAsFile(file);
        Log($"Step {_step:00}: {label} ({PathAndQuery}) -> {Path.GetFileName(file)}");
        if (focus != null)
        {
            // Close-up of the element that proves the step (on small screens the dashboard layout
            // can keep it outside the visible area, so the close-up is the reliable proof).
            try
            {
                ((ITakesScreenshot)focus).GetScreenshot().SaveAsFile(Path.ChangeExtension(file, null) + "-detail.png");
            }
            catch (WebDriverException)
            {
                // Element close-ups are a bonus; the full screenshot above is already saved.
            }
        }
        return file;
    }

    public void Log(string message) => _output.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");

    private static string Safe(string label) =>
        new string(label.Select(c => char.IsLetterOrDigit(c) || c == '-' ? char.ToLowerInvariant(c) : '-').ToArray());

    public void Dispose()
    {
        Driver.Quit();
        Driver.Dispose();
    }
}
