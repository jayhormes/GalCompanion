using System;
using Xunit;

namespace GalCompanion.Tests
{
    public class HotkeyParserTests
    {
        [Theory]
        [InlineData("Shift+F12", NativeMethods.MOD_SHIFT, (uint)0x7B)]
        [InlineData("Ctrl+Alt+S", NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, (uint)0x53)]
        [InlineData("F9", (uint)0, (uint)0x78)]
        [InlineData("ctrl+shift+a", NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT, (uint)0x41)]
        [InlineData("Win+D1", NativeMethods.MOD_WIN, (uint)0x31)]
        [InlineData(" Control + F5 ", NativeMethods.MOD_CONTROL, (uint)0x74)]
        public void Parses_valid_hotkeys(string text, uint expectedModifiers, uint expectedVk)
        {
            HotkeyListener.ParseHotkey(text, out var modifiers, out var vk);
            Assert.Equal(expectedModifiers, modifiers);
            Assert.Equal(expectedVk, vk);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Ctrl+")]
        [InlineData("Foo+F1")]
        [InlineData("Ctrl+NotAKey")]
        public void Rejects_invalid_hotkeys(string text)
        {
            Assert.Throws<FormatException>(() => HotkeyListener.ParseHotkey(text, out _, out _));
        }

        // Enum.TryParse 會把 "1" 解析成 (Key)1 = Cancel，這種輸入必須報錯而不是靜默給錯的鍵
        [Theory]
        [InlineData("Ctrl+1")]
        [InlineData("5")]
        public void Rejects_bare_numeric_keys(string text)
        {
            Assert.Throws<FormatException>(() => HotkeyListener.ParseHotkey(text, out _, out _));
        }
    }
}
