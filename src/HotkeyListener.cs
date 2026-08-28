using System;
using System.Windows.Input;
using System.Windows.Interop;

namespace GalCompanion
{
    internal sealed class HotkeyListener : IDisposable
    {
        private const int HotkeyId = 0x4743;

        private HwndSource source;
        private bool registered;
        private readonly Action callback;

        private HotkeyListener(Action callback)
        {
            this.callback = callback;
        }

        public static HotkeyListener Register(string hotkeyText, Action callback)
        {
            var listener = new HotkeyListener(callback);
            listener.RegisterInternal(hotkeyText);
            return listener;
        }

        private void RegisterInternal(string hotkeyText)
        {
            ParseHotkey(hotkeyText, out var modifiers, out var vk);

            var parameters = new HwndSourceParameters("GalCompanionHotkeyWindow")
            {
                WindowStyle = 0,
                ExtendedWindowStyle = 0,
                ParentWindow = new IntPtr(-3), // HWND_MESSAGE
                Width = 0,
                Height = 0
            };
            source = new HwndSource(parameters);
            source.AddHook(WndProc);

            if (!NativeMethods.RegisterHotKey(source.Handle, HotkeyId, modifiers | NativeMethods.MOD_NOREPEAT, vk))
            {
                Dispose();
                throw new InvalidOperationException($"熱鍵 {hotkeyText} 註冊失敗，可能已被其他程式佔用");
            }
            registered = true;
        }

        internal static void ParseHotkey(string text, out uint modifiers, out uint vk)
        {
            modifiers = 0;
            var parts = (text ?? string.Empty).Split('+');
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[parts.Length - 1]))
            {
                throw new FormatException($"熱鍵格式錯誤：{text}");
            }

            for (int i = 0; i < parts.Length - 1; i++)
            {
                switch (parts[i].Trim().ToLowerInvariant())
                {
                    case "ctrl":
                    case "control":
                        modifiers |= NativeMethods.MOD_CONTROL;
                        break;
                    case "alt":
                        modifiers |= NativeMethods.MOD_ALT;
                        break;
                    case "shift":
                        modifiers |= NativeMethods.MOD_SHIFT;
                        break;
                    case "win":
                        modifiers |= NativeMethods.MOD_WIN;
                        break;
                    default:
                        throw new FormatException($"不認識的修飾鍵：{parts[i]}");
                }
            }

            var keyToken = parts[parts.Length - 1].Trim();
            // Enum.TryParse 會把純數字當 enum 數值解析成錯的鍵，必須先擋掉
            if (int.TryParse(keyToken, out _))
            {
                throw new FormatException($"數字鍵請用 D0-D9 或 NumPad0-NumPad9：{keyToken}");
            }
            if (!Enum.TryParse<Key>(keyToken, true, out var key))
            {
                throw new FormatException($"不認識的按鍵：{keyToken}");
            }
            vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                callback?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (source == null)
            {
                return;
            }
            if (registered)
            {
                NativeMethods.UnregisterHotKey(source.Handle, HotkeyId);
                registered = false;
            }
            source.RemoveHook(WndProc);
            source.Dispose();
            source = null;
        }
    }
}
