using System;
using System.IO;
using Xunit;

namespace GalCompanion.Tests
{
    public class ScreenshotPathsTests
    {
        private static readonly Guid GameId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        private const string DefaultRoot = @"C:\Playnite\ExtraMetadata";

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Falls_back_to_default_root(string configRoot)
        {
            var dir = ScreenshotPaths.GetDir(configRoot, DefaultRoot, GameId);
            Assert.Equal(
                Path.Combine(DefaultRoot, "games", GameId.ToString(), "screenshots"),
                dir);
        }

        [Fact]
        public void Custom_root_wins()
        {
            var dir = ScreenshotPaths.GetDir(@"D:\Screens", DefaultRoot, GameId);
            Assert.Equal(
                Path.Combine(@"D:\Screens", "games", GameId.ToString(), "screenshots"),
                dir);
        }

        [Fact]
        public void No_game_goes_to_unassigned()
        {
            var dir = ScreenshotPaths.GetDir(null, DefaultRoot, null);
            Assert.Equal(
                Path.Combine(DefaultRoot, "screenshots", "unassigned"),
                dir);
        }
    }
}
