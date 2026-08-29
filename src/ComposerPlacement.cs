using System;

namespace GalCompanion
{
    /// <summary>
    /// 入力欄を気泡ウィンドウの隣に置く。気泡は画面端に寄せて使うことが多いので、
    /// 下に入らなければ上、それも無理なら作業領域に収まるところまで押し戻す。
    /// </summary>
    internal static class ComposerPlacement
    {
        internal const double Gap = 8;

        // SizeToContent なので Show 前は実寸が取れない。その時の概算
        internal const double NominalHeight = 190;

        public static void Resolve(
            ScreenRect bubble, double width, double height, ScreenRect workArea,
            out double left, out double top)
        {
            var below = bubble.Bottom + Gap;
            if (below + height <= workArea.Bottom)
            {
                top = below;
            }
            else
            {
                var above = bubble.Top - Gap - height;
                top = above >= workArea.Top ? above : workArea.Bottom - height;
            }
            top = Clamp(top, workArea.Top, workArea.Bottom - height);

            left = Clamp(bubble.Left, workArea.Left, workArea.Right - width);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (max < min)
            {
                return min;
            }
            return value < min ? min : (value > max ? max : value);
        }
    }
}
