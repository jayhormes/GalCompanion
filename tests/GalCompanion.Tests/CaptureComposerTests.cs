using Xunit;

namespace GalCompanion.Tests
{
    public class CaptureComposerTests
    {
        [Fact]
        public void Disabled_captures_on_every_press()
        {
            var composer = new CaptureComposer(() => false);

            Assert.Equal(CapturePress.CaptureNow, composer.Press());
            Assert.Equal(CapturePress.CaptureNow, composer.Press());
            Assert.False(composer.IsComposing);
        }

        [Fact]
        public void First_press_opens_the_composer_and_the_second_commits()
        {
            var composer = new CaptureComposer(() => true);

            Assert.Equal(CapturePress.OpenComposer, composer.Press());
            Assert.True(composer.IsComposing);

            Assert.Equal(CapturePress.Commit, composer.Press());
            Assert.False(composer.IsComposing);
        }

        [Fact]
        public void A_third_press_starts_a_new_round()
        {
            var composer = new CaptureComposer(() => true);

            composer.Press();
            composer.Press();

            Assert.Equal(CapturePress.OpenComposer, composer.Press());
            Assert.True(composer.IsComposing);
        }

        [Fact]
        public void Cancel_drops_the_open_composer()
        {
            var composer = new CaptureComposer(() => true);
            composer.Press();

            Assert.True(composer.Cancel());
            Assert.False(composer.IsComposing);

            // 取り消した直後の押下はまた入力欄から
            Assert.Equal(CapturePress.OpenComposer, composer.Press());
        }

        [Fact]
        public void Cancel_without_an_open_composer_reports_nothing_to_close()
        {
            var composer = new CaptureComposer(() => true);

            Assert.False(composer.Cancel());
        }

        [Fact]
        public void Turning_the_option_off_mid_edit_still_commits_what_is_open()
        {
            var enabled = true;
            var composer = new CaptureComposer(() => enabled);

            Assert.Equal(CapturePress.OpenComposer, composer.Press());
            enabled = false;

            Assert.Equal(CapturePress.Commit, composer.Press());
            Assert.Equal(CapturePress.CaptureNow, composer.Press());
        }

        [Fact]
        public void A_null_predicate_behaves_as_disabled()
        {
            var composer = new CaptureComposer(null);

            Assert.Equal(CapturePress.CaptureNow, composer.Press());
        }
    }
}
