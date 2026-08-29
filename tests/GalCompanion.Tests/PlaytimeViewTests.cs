using Xunit;

namespace GalCompanion.Tests
{
    public class PlaytimeViewTests
    {
        [Theory]
        [InlineData(0, "0h")]
        [InlineData(-1, "0h")]
        [InlineData(59, "0m")]
        [InlineData(90, "1m")]
        [InlineData(3599, "59m")]
        [InlineData(3600, "1.0h")]
        [InlineData(5400, "1.5h")]
        [InlineData(360000, "100.0h")]
        public void FormatHours_switches_to_minutes_under_an_hour(long seconds, string expected)
        {
            Assert.Equal(expected, PlaytimeView.FormatHours(seconds));
        }

        [Fact]
        public void Shade_gets_stronger_with_the_level()
        {
            var empty = PlaytimeView.Shade(0);
            var light = PlaytimeView.Shade(1);
            var heavy = PlaytimeView.Shade(4);

            Assert.True(light.A < heavy.A);
            Assert.NotEqual(empty.R, light.R);
        }

        [Fact]
        public void Shade_clamps_above_the_top_level()
        {
            Assert.Equal(PlaytimeView.Shade(4).A, PlaytimeView.Shade(9).A);
        }
    }
}
