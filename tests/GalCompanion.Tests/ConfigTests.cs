using Xunit;

namespace GalCompanion.Tests
{
    public class ConfigTests
    {
        [Fact]
        public void Defaults_are_sane()
        {
            var config = new GalCompanionConfig();
            Assert.Equal("Shift+F12", config.Hotkey);
            Assert.Equal("auto", config.CaptureMode);
            Assert.True(config.ClientAreaOnly);
            Assert.False(config.SaveToFile);
            Assert.True(config.PlaySound);
            Assert.True(config.ShowBubble);
            Assert.Equal(0.55, config.BubbleOpacity, 3);
            Assert.Null(config.BubbleX);
            Assert.Null(config.BubbleY);
            Assert.Equal(string.Empty, config.ScreenshotRoot);
            Assert.False(config.TriliumEnabled);
            Assert.Equal(string.Empty, config.TriliumUrl);
            Assert.Equal(string.Empty, config.TriliumToken);
            Assert.Equal(string.Empty, config.TriliumParentNoteId);
            Assert.True(config.TriliumSendScreenshots);
            Assert.NotNull(config.TriliumNoteBindings);
            Assert.Empty(config.TriliumNoteBindings);
        }

        [Fact]
        public void Default_hotkey_parses()
        {
            var config = new GalCompanionConfig();
            HotkeyListener.ParseHotkey(config.Hotkey, out _, out _);
        }

        [Theory]
        [InlineData(0.55, 0.55)]
        [InlineData(0.0, 0.1)]
        [InlineData(-1.0, 0.1)]
        [InlineData(1.0, 1.0)]
        [InlineData(5.0, 1.0)]
        public void Opacity_is_clamped(double input, double expected)
        {
            Assert.Equal(expected, GalCompanionConfig.ClampOpacity(input), 3);
        }
    }
}
