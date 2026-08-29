using Xunit;

namespace GalCompanion.Tests
{
    public class ComposerPlacementTests
    {
        private static readonly ScreenRect Work = new ScreenRect(0, 0, 1920, 1040);

        [Fact]
        public void Sits_under_the_bubble_and_lines_up_on_its_left_edge()
        {
            var bubble = new ScreenRect(300, 200, 140, 60);
            double left, top;

            ComposerPlacement.Resolve(bubble, 420, 190, Work, out left, out top);

            Assert.Equal(300, left);
            Assert.Equal(260 + ComposerPlacement.Gap, top);
        }

        [Fact]
        public void Flips_above_the_bubble_when_there_is_no_room_below()
        {
            var bubble = new ScreenRect(300, 960, 140, 60);
            double left, top;

            ComposerPlacement.Resolve(bubble, 420, 190, Work, out left, out top);

            Assert.Equal(960 - ComposerPlacement.Gap - 190, top);
        }

        [Fact]
        public void Stays_inside_the_work_area_when_neither_side_fits()
        {
            var bubble = new ScreenRect(300, 0, 140, 1000);
            double left, top;

            ComposerPlacement.Resolve(bubble, 420, 190, Work, out left, out top);

            Assert.Equal(1040 - 190, top);
            Assert.True(top >= Work.Top);
        }

        [Fact]
        public void Pushes_back_from_the_right_edge()
        {
            var bubble = new ScreenRect(1850, 200, 140, 60);
            double left, top;

            ComposerPlacement.Resolve(bubble, 420, 190, Work, out left, out top);

            Assert.Equal(1920 - 420, left);
        }

        [Fact]
        public void Never_starts_left_of_the_work_area()
        {
            var bubble = new ScreenRect(-500, 200, 140, 60);
            double left, top;

            ComposerPlacement.Resolve(bubble, 420, 190, Work, out left, out top);

            Assert.Equal(0, left);
        }

        [Fact]
        public void Honours_a_secondary_monitor_offset()
        {
            var work = new ScreenRect(1920, 0, 1280, 720);
            var bubble = new ScreenRect(3100, 600, 140, 60);
            double left, top;

            ComposerPlacement.Resolve(bubble, 420, 190, work, out left, out top);

            Assert.Equal(3200 - 420, left);
            Assert.Equal(600 - ComposerPlacement.Gap - 190, top);
        }

        [Fact]
        public void Falls_back_to_the_top_when_the_composer_is_taller_than_the_screen()
        {
            var work = new ScreenRect(0, 0, 800, 150);
            var bubble = new ScreenRect(10, 10, 140, 60);
            double left, top;

            ComposerPlacement.Resolve(bubble, 420, 400, work, out left, out top);

            Assert.Equal(0, top);
        }
    }
}
