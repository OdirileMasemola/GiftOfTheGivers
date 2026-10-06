using System.Reflection;

namespace GiftOfTheGivers
{
    /// <summary>
    /// The build number shown in the footer. The Azure DevOps pipeline publishes the app with
    /// /p:InformationalVersion=$(Build.BuildNumber), so the live site shows which pipeline run
    /// it came from. That makes a deploy, or a rollback to an older run, easy to see.
    /// Local builds show the default version from the project file.
    /// </summary>
    public static class BuildInfo
    {
        public static string Version { get; } = Trim(
            typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

        // The SDK can add "+<commit hash>" to the version; the footer only needs the build number.
        public static string Trim(string? informationalVersion)
        {
            if (string.IsNullOrWhiteSpace(informationalVersion))
            {
                return "local";
            }

            var plus = informationalVersion.IndexOf('+');
            return plus > 0 ? informationalVersion[..plus] : informationalVersion;
        }
    }
}
