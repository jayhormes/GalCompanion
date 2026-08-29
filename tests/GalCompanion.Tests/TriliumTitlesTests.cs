using Xunit;

namespace GalCompanion.Tests
{
    public class TriliumTitlesTests
    {
        private const string Fallback = TriliumTitles.DefaultImpressions;

        // --- 既定：標題の頭にゲーム名を足すだけ。既存の設定値をそのまま活かせる ---

        [Fact]
        public void Prefixes_the_game_name()
        {
            Assert.Equal("モザイクの天使 遊戲心得",
                TriliumTitles.Format("遊戲心得", "モザイクの天使", Fallback, true));
        }

        [Fact]
        public void Without_the_prefix_option_the_title_is_shared_by_every_game()
        {
            Assert.Equal("遊戲心得", TriliumTitles.Format("遊戲心得", "モザイクの天使", Fallback, false));
        }

        [Fact]
        public void No_game_name_means_no_prefix_and_no_stray_space()
        {
            Assert.Equal("遊戲心得", TriliumTitles.Format("遊戲心得", null, Fallback, true));
            Assert.Equal("遊戲心得", TriliumTitles.Format("遊戲心得", "   ", Fallback, true));
        }

        [Fact]
        public void Trims_the_game_name_before_prefixing()
        {
            Assert.Equal("A 遊戲心得", TriliumTitles.Format("遊戲心得", "  A  ", Fallback, true));
        }

        // --- {game} を書いたときは、そちらが優先 ---

        [Fact]
        public void An_explicit_placeholder_decides_where_the_name_goes()
        {
            Assert.Equal("【A】心得", TriliumTitles.Format("【{game}】心得", "A", Fallback, true));
            Assert.Equal("【A】心得", TriliumTitles.Format("【{game}】心得", "A", Fallback, false));
        }

        [Fact]
        public void The_placeholder_never_doubles_the_prefix()
        {
            Assert.Equal("A 遊戲心得", TriliumTitles.Format("{game} 遊戲心得", "A", Fallback, true));
        }

        [Fact]
        public void Drops_the_placeholder_when_there_is_no_game()
        {
            Assert.Equal("遊戲心得", TriliumTitles.Format("{game} 遊戲心得", null, Fallback, true));
        }

        [Fact]
        public void Handles_more_than_one_placeholder()
        {
            Assert.Equal("A — A 心得", TriliumTitles.Format("{game} — {game} 心得", "A", Fallback));
        }

        [Fact]
        public void The_placeholder_is_case_insensitive()
        {
            Assert.Equal("A 心得", TriliumTitles.Format("{GAME} 心得", "A", Fallback));
            Assert.True(TriliumTitles.HasPlaceholder("{Game} 心得"));
            Assert.False(TriliumTitles.HasPlaceholder("遊戲心得"));
        }

        [Fact]
        public void Collapses_the_gap_left_by_an_empty_game_name()
        {
            Assert.Equal("前 後", TriliumTitles.Format("前 {game} 後", null, Fallback));
        }

        // --- 空標題のノートを作らせない ---

        [Fact]
        public void An_empty_template_falls_back()
        {
            Assert.Equal("A 遊戲心得", TriliumTitles.Format(null, "A", Fallback, true));
            Assert.Equal("A 遊戲心得", TriliumTitles.Format("   ", "A", Fallback, true));
        }

        [Fact]
        public void A_template_that_is_only_a_placeholder_uses_the_game_name()
        {
            Assert.Equal("A", TriliumTitles.Format("{game}", "A", Fallback));
        }

        [Fact]
        public void A_placeholder_only_template_with_no_game_falls_back()
        {
            Assert.Equal("遊戲心得", TriliumTitles.Format("{game}", null, Fallback));
        }

        // --- 見出しでゲーム名を繰り返さない判定 ---

        [Fact]
        public void CarriesGameName_covers_both_ways_of_getting_the_name_in()
        {
            Assert.True(TriliumTitles.CarriesGameName("遊戲心得", true));
            Assert.True(TriliumTitles.CarriesGameName("{game} 心得", false));
            Assert.False(TriliumTitles.CarriesGameName("遊戲心得", false));
        }
    }
}
