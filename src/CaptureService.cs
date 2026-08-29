using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace GalCompanion
{
    internal static class CaptureService
    {
        public static Bitmap CaptureForegroundWindow(string mode, bool clientAreaOnly)
        {
            return CaptureWindow(NativeMethods.GetForegroundWindow(), mode, clientAreaOnly);
        }

        // 入力欄を開いている間はゲームが前景ではなくなるので、
        // 開いた時点で覚えておいたウィンドウを撮れるようにする。
        public static Bitmap CaptureWindow(IntPtr hwnd, string mode, bool clientAreaOnly)
        {
            if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            {
                return null;
            }

            if (mode == "printwindow")
            {
                return TryPrintWindow(hwnd, clientAreaOnly);
            }

            if (mode != "screencrop")
            {
                var bmp = TryPrintWindow(hwnd, clientAreaOnly);
                if (bmp != null && !IsUniformColor(bmp))
                {
                    return bmp;
                }
                bmp?.Dispose();
            }

            return CaptureByScreenCopy(hwnd, clientAreaOnly);
        }

        private static Bitmap TryPrintWindow(IntPtr hwnd, bool clientAreaOnly)
        {
            NativeMethods.RECT rect;
            var ok = clientAreaOnly
                ? NativeMethods.GetClientRect(hwnd, out rect)
                : NativeMethods.GetWindowRect(hwnd, out rect);
            if (!ok)
            {
                return null;
            }

            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            var bmp = new Bitmap(width, height);
            try
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    var hdc = g.GetHdc();
                    bool printed;
                    try
                    {
                        var flags = NativeMethods.PW_RENDERFULLCONTENT
                            | (clientAreaOnly ? NativeMethods.PW_CLIENTONLY : 0);
                        printed = NativeMethods.PrintWindow(hwnd, hdc, flags);
                    }
                    finally
                    {
                        g.ReleaseHdc(hdc);
                    }
                    if (!printed)
                    {
                        bmp.Dispose();
                        return null;
                    }
                }
                return bmp;
            }
            catch
            {
                bmp.Dispose();
                throw;
            }
        }

        private static Bitmap CaptureByScreenCopy(IntPtr hwnd, bool clientAreaOnly)
        {
            int x, y, width, height;
            if (clientAreaOnly)
            {
                if (!NativeMethods.GetClientRect(hwnd, out var client))
                {
                    return null;
                }
                var origin = new NativeMethods.POINT { X = 0, Y = 0 };
                if (!NativeMethods.ClientToScreen(hwnd, ref origin))
                {
                    return null;
                }
                x = origin.X;
                y = origin.Y;
                width = client.Right - client.Left;
                height = client.Bottom - client.Top;
            }
            else
            {
                // GetWindowRect 會含不可見的 resize 邊框，優先用 DWM 實際邊界
                if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                        out var rect, Marshal.SizeOf(typeof(NativeMethods.RECT))) != 0)
                {
                    if (!NativeMethods.GetWindowRect(hwnd, out rect))
                    {
                        return null;
                    }
                }
                x = rect.Left;
                y = rect.Top;
                width = rect.Right - rect.Left;
                height = rect.Bottom - rect.Top;
            }

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            var bmp = new Bitmap(width, height);
            try
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(x, y, 0, 0, new Size(width, height));
                }
                return bmp;
            }
            catch
            {
                bmp.Dispose();
                throw;
            }
        }

        internal static bool IsUniformColor(Bitmap bmp)
        {
            var stepX = Math.Max(1, bmp.Width / 10);
            var stepY = Math.Max(1, bmp.Height / 10);
            int? first = null;
            for (var x = 0; x < bmp.Width; x += stepX)
            {
                for (var y = 0; y < bmp.Height; y += stepY)
                {
                    var argb = bmp.GetPixel(x, y).ToArgb();
                    if (first == null)
                    {
                        first = argb;
                    }
                    else if (argb != first.Value)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
