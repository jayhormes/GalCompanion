using Xunit;

namespace GalCompanion.Tests
{
    public class TriliumTitlesTests
    {
        private const string Fallback = TriliumTitles.DefaultImpressions;

        [Fact]
        public void Substitutes_the_game_name()
        {
            Assert.Equal("モザイクの天使 遊戲心得",
                TriliumTitles.Format("{game} 遊戲心得", "モザイクの天使", Fallback));
        }

        [Fact]
        public void Drops_the_placeholder_when_there_is_no_game()
        {
            Assert.Equal("遊戲心得", TriliumTitles.Format("{game} 遊戲心得", null, Fallback));
            Assert.Equal("遊戲心得", TriliumTitles.Format("{game} 遊戲心得", "   ", Fallback));
        }

        [Fact]
        public void Trims_the_game_name_before_substituting()
        {
            Assert.Equal("A 遊戲心得", TriliumTitles.Format("{game} 遊戲心得", "  A  ", Fallback));
        }

        [Fact]
        public void A_template_without_the_placeholder_is_shared_by_every_game()
        {
            Assert.Equal("遊戲筆記", TriliumTitles.Format("遊戲筆記", "モザイクの天使", Fallback));
            Assert.False(TriliumTitles.IsPerGame("遊戲筆記"));
            Assert.True(TriliumTitles.IsPerGame("{game} 遊戲心得"));
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
            Assert.True(TriliumTitles.IsPerGame("{Game} 心得"));
        }

        [Fact]
        public void Collapses_the_gap_left_by_an_empty_game_name()
        {
            Assert.Equal("前 後", TriliumTitles.Format("前 {game} 後", null, Fallback));
        }

        [Fact]
        public void An_empty_template_falls_back()
        {
            Assert.Equal("A 遊戲心得", TriliumTitles.Format(null, "A", Fallback));
            Assert.Equal("A 遊戲心得", TriliumTitles.Format("   ", "A", Fallback));
        }

        [Fact]
        public void A_template_that_is_only_a_placeholder_uses_the_game_name()
        {
            Assert.Equal("A", TriliumTitles.Format("{game}", "A", Fallback));
        }

        [Fact]
        public void A_placeholder_only_template_with_no_game_falls_back()
        {
            // 空標題のノートを作らせない
            Assert.Equal("遊戲心得", TriliumTitles.Format("{game}", null, Fallback));
        }
    }
}
