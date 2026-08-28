using Xunit;

namespace GalCompanion.Tests
{
    public class LocaleEmulatorActionsTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void Default_profile_uses_bare_quoted_path(string guid)
        {
            Assert.Equal("\"C:\\game\\game.exe\"",
                LocaleEmulatorActions.BuildArguments(guid, "C:\\game\\game.exe"));
        }

        [Fact]
        public void Explicit_profile_uses_runas()
        {
            Assert.Equal("-runas 12345678-1234-1234-1234-123456789abc \"{InstallDir}\\game.exe\"",
                LocaleEmulatorActions.BuildArguments(
                    " 12345678-1234-1234-1234-123456789abc ", "{InstallDir}\\game.exe"));
        }
    }
}
