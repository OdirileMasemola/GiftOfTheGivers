using GiftOfTheGivers;
using Xunit;

namespace GiftOfTheGivers.Tests
{
    public class BuildInfoTests
    {
        [Theory]
        [InlineData("20261003.4", "20261003.4")]
        [InlineData("20261003.4+0a1b2c3d", "20261003.4")]
        [InlineData("1.0.0", "1.0.0")]
        public void Trim_KeepsOnlyTheBuildNumber(string raw, string expected)
        {
            Assert.Equal(expected, BuildInfo.Trim(raw));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Trim_FallsBackToLocal_WhenThereIsNoVersion(string? raw)
        {
            Assert.Equal("local", BuildInfo.Trim(raw));
        }

        [Fact]
        public void Version_IsNeverEmpty_AndHasNoCommitHash()
        {
            Assert.False(string.IsNullOrWhiteSpace(BuildInfo.Version));
            Assert.DoesNotContain("+", BuildInfo.Version);
        }
    }
}
