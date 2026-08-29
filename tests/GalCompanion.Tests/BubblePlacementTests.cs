using Xunit;

namespace GalCompanion.Tests
{
    // 解像度変更やモニタ構成の変化で保存座標が画面外に出るケースの回復
    public class BubblePlacementTests
    {
        private static readonly ScreenRect Single = new ScreenRect(0, 0, 1920, 1080);
        private static readonly ScreenRect WorkArea = new ScreenRect(0, 0, 1920, 1040);

        // 左に 2 枚目のモニタがある構成
        private static readonly ScreenRect DualScreen = new ScreenRect(-1920, 0, 3840, 1080);

        [Theory]
        [InlineData(100, 100, true)]     // 画面ど真ん中
        [InlineData(0, 0, true)]         // 左上ぴったり
        [InlineData(1780, 1020, true)]   // 右下ぎりぎりだが掴める
        [InlineData(1900, 500, false)]   // 右にはみ出して 20px しか見えない
        [InlineData(-130, 500, false)]   // 左にはみ出し
        [InlineData(500, -55, false)]    // 上にはみ出し
        [InlineData(3000, 500, false)]   // 消えた 2 枚目のモニタに置き去り
        [InlineData(500, 2000, false)]   // 縦に置き去り
        public void IsReachable_checks_that_enough_of_the_window_is_on_screen(double x, double y, bool expected)
        {
            Assert.Equal(expected, BubblePlacement.IsReachable(x, y, 140, 60, Single));
        }

        [Fact]
        public void IsReachable_accepts_a_position_on_a_second_monitor()
        {
            Assert.True(BubblePlacement.IsReachable(-1800, 300, 140, 60, DualScreen));
            Assert.False(BubblePlacement.IsReachable(-1800, 300, 140, 60, Single));
        }

        [Fact]
        public void Center_puts_the_window_in_the_middle_of_the_work_area()
        {
            double x, y;
            BubblePlacement.Center(140, 60, WorkArea, out x, out y);

            Assert.Equal(890, x);
            Assert.Equal(490, y);
        }

        [Fact]
        public void Center_uses_nominal_size_before_the_window_is_measured()
        {
            double x, y;
            BubblePlacement.Center(0, 0, WorkArea, out x, out y);

            Assert.Equal((1920 - BubblePlacement.NominalWidth) / 2, x);
            Assert.Equal((1040 - BubblePlacement.NominalHeight) / 2, y);
        }

        [Fact]
        public void Resolve_keeps_a_saved_position_that_is_still_visible()
        {
            double x, y;
            BubblePlacement.Resolve(300, 200, 140, 60, Single, WorkArea, out x, out y);

            Assert.Equal(300, x);
            Assert.Equal(200, y);
        }

        [Fact]
        public void Resolve_recovers_a_position_left_on_a_disconnected_monitor()
        {
            double x, y;
            BubblePlacement.Resolve(3000, 500, 140, 60, Single, WorkArea, out x, out y);

            Assert.Equal(890, x);
            Assert.Equal(490, y);
        }

        [Fact]
        public void Resolve_centers_when_nothing_was_saved()
        {
            double x, y;
            BubblePlacement.Resolve(null, null, 140, 60, Single, WorkArea, out x, out y);

            Assert.Equal(890, x);
            Assert.Equal(490, y);
        }

        [Theory]
        [InlineData(300d, null)]
        [InlineData(null, 200d)]
        public void Resolve_centers_when_only_one_axis_was_saved(double? savedX, double? savedY)
        {
            double x, y;
            BubblePlacement.Resolve(savedX, savedY, 140, 60, Single, WorkArea, out x, out y);

            Assert.Equal(890, x);
            Assert.Equal(490, y);
        }
    }
}
