namespace GiftOfTheGivers.UiTests.Infrastructure;

/// <summary>The two screen sizes every flow is run at.</summary>
public sealed record Viewport(string Name, int Width, int Height, bool IsMobile)
{
    public static readonly Viewport Desktop = new("desktop-1920x1080", 1920, 1080, false);

    /// <summary>iPhone 12/13/14 size, using Chrome's mobile emulation (touch + mobile user agent).</summary>
    public static readonly Viewport Mobile = new("mobile-390x844", 390, 844, true);

    /// <summary>Theory data for [MemberData]; plain strings keep the Test Explorer names readable.</summary>
    public static TheoryData<string> All => new() { Desktop.Name, Mobile.Name };

    public static Viewport Named(string name) =>
        name == Mobile.Name ? Mobile : name == Desktop.Name ? Desktop : throw new ArgumentException($"Unknown viewport {name}");

    public override string ToString() => Name;
}
