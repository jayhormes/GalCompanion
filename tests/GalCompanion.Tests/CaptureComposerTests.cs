using Xunit;

namespace GalCompanion.Tests
{
    public class CaptureComposerTests
    {
        private static CaptureComposer Enabled() => new CaptureComposer(_ => true);

        [Fact]
        public void Disabled_falls_back_on_every_press()
        {
            var composer = new CaptureComposer(_ => false);

            Assert.Equal(CapturePress.CaptureNow, composer.Press(TriliumTarget.Impressions).Action);
            Assert.Equal(CapturePress.CaptureNow, composer.Press(TriliumTarget.Translation).Action);
            Assert.False(composer.IsComposing);
        }

        [Fact]
        public void First_press_opens_the_composer_and_the_second_commits()
        {
            var composer = Enabled();

            var opened = composer.Press(TriliumTarget.Impressions);
            Assert.Equal(CapturePress.OpenComposer, opened.Action);
            Assert.Equal(TriliumTarget.Impressions, opened.Target);
            Assert.True(composer.IsComposing);

            var committed = composer.Press(TriliumTarget.Impressions);
            Assert.Equal(CapturePress.Commit, committed.Action);
            Assert.Equal(TriliumTarget.Impressions, committed.Target);
            Assert.False(composer.IsComposing);
        }

        [Fact]
        public void The_note_button_opens_the_composer_for_the_translation_note()
        {
            var composer = Enabled();

            var opened = composer.Press(TriliumTarget.Translation);

            Assert.Equal(CapturePress.OpenComposer, opened.Action);
            Assert.Equal(TriliumTarget.Translation, opened.Target);
            Assert.Equal(TriliumTarget.Translation, composer.Target);
        }

        [Fact]
        public void Committing_with_the_other_button_keeps_the_destination_that_opened_it()
        {
            var composer = Enabled();
            composer.Press(TriliumTarget.Translation);

            // 見出しには「翻譯問題」と出ているので、送り先はそちらのまま
            var committed = composer.Press(TriliumTarget.Impressions);

            Assert.Equal(CapturePress.Commit, committed.Action);
            Assert.Equal(TriliumTarget.Translation, committed.Target);
        }

        [Fact]
        public void A_third_press_starts_a_new_round_on_the_button_that_was_pressed()
        {
            var composer = Enabled();
            composer.Press(TriliumTarget.Translation);
            composer.Press(TriliumTarget.Translation);

            var opened = composer.Press(TriliumTarget.Impressions);

            Assert.Equal(CapturePress.OpenComposer, opened.Action);
            Assert.Equal(TriliumTarget.Impressions, opened.Target);
        }

        [Fact]
        public void Cancel_drops_the_open_composer()
        {
            var composer = Enabled();
            composer.Press(TriliumTarget.Impressions);

            Assert.True(composer.Cancel());
            Assert.False(composer.IsComposing);
            Assert.Equal(CapturePress.OpenComposer, composer.Press(TriliumTarget.Impressions).Action);
        }

        [Fact]
        public void Cancel_without_an_open_composer_reports_nothing_to_close()
        {
            Assert.False(Enabled().Cancel());
        }

        [Fact]
        public void Turning_the_option_off_mid_edit_still_commits_what_is_open()
        {
            var enabled = true;
            var composer = new CaptureComposer(_ => enabled);

            Assert.Equal(CapturePress.OpenComposer, composer.Press(TriliumTarget.Impressions).Action);
            enabled = false;

            Assert.Equal(CapturePress.Commit, composer.Press(TriliumTarget.Impressions).Action);
            Assert.Equal(CapturePress.CaptureNow, composer.Press(TriliumTarget.Impressions).Action);
        }

        [Fact]
        public void The_gate_is_asked_per_destination()
        {
            // 「截圖自動送 Trilium」を切っても 📝 は自分で押した記録なので生きる
            var composer = new CaptureComposer(t => t == TriliumTarget.Translation);

            Assert.Equal(CapturePress.CaptureNow, composer.Press(TriliumTarget.Impressions).Action);
            Assert.False(composer.IsComposing);

            Assert.Equal(CapturePress.OpenComposer, composer.Press(TriliumTarget.Translation).Action);
        }

        [Fact]
        public void A_null_predicate_behaves_as_disabled()
        {
            var composer = new CaptureComposer(null);

            Assert.Equal(CapturePress.CaptureNow, composer.Press(TriliumTarget.Impressions).Action);
        }
    }
}
