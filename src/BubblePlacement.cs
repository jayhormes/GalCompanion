using System;

namespace GalCompanion
{
    /// <summary>画面領域。WPF に依存しないのでテストできる。</summary>
    internal struct ScreenRect
    {
        public double Left;
        public double Top;
        public double Width;
        public double Height;

        public double Right => Left + Width;
        public double Bottom => Top + Height;

        public ScreenRect(double left, double top, double width, double height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// 気泡ウィンドウの位置決め。解像度変更やモニタ構成の変化で、
    /// 保存済みの座標が画面外に出てしまうことがあるので毎回検証する。
    /// </summary>
    internal static class BubblePlacement
    {
        // SizeToContent なので実測前は概算を使う
        internal const double NominalWidth = 140;
        internal const double NominalHeight = 60;

        // これだけ見えていれば掴んで動かせる
        internal const double MinVisible = 32;

        public static bool IsReachable(double x, double y, double width, double height, ScreenRect screen)
        {
            var w = width > 0 ? width : NominalWidth;
            var h = height > 0 ? height : NominalHeight;

            var overlapX = Math.Min(x + w, screen.Right) - Math.Max(x, screen.Left);
            var overlapY = Math.Min(y + h, screen.Bottom) - Math.Max(y, screen.Top);

            return overlapX >= Math.Min(MinVisible, w)
                && overlapY >= Math.Min(MinVisible, h);
        }

        public static void Center(double width, double height, ScreenRect workArea,
            out double x, out double y)
        {
            var w = width > 0 ? width : NominalWidth;
            var h = height > 0 ? height : NominalHeight;

            x = workArea.Left + (workArea.Width - w) / 2;
            y = workArea.Top + (workArea.Height - h) / 2;
        }

        /// <summary>
        /// 保存座標が使えるならそれを、画面外／未設定なら作業領域の中央を返す。
        /// </summary>
        public static void Resolve(double? savedX, double? savedY, double width, double height,
            ScreenRect virtualScreen, ScreenRect workArea, out double x, out double y)
        {
            if (savedX.HasValue && savedY.HasValue
                && IsReachable(savedX.Value, savedY.Value, width, height, virtualScreen))
            {
                x = savedX.Value;
                y = savedY.Value;
                return;
            }

            Center(width, height, workArea, out x, out y);
        }
    }
}
