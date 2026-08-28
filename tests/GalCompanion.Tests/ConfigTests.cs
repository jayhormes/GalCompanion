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
            Assert.True(config.CopyToClipboard);
            Assert.True(config.SaveToFile);
            Assert.True(config.PlaySound);
            Assert.True(config.ShowBubble);
            Assert.Null(config.BubbleX);
            Assert.Null(config.BubbleY);
            Assert.Equal(string.Empty, config.ScreenshotRoot);
        }

        [Fact]
        public void Default_hotkey_parses()
        {
            var config = new GalCompanionConfig();
            HotkeyListener.ParseHotkey(config.Hotkey, out _, out _);
        }
    }
}
