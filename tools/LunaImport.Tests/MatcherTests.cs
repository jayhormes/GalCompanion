using System;
using System.Collections.Generic;
using Xunit;

namespace LunaImport.Tests
{
    public class MatcherTests
    {
        private static PlayniteGame Game(string name, string install, params string[] actions)
        {
            var game = new PlayniteGame
            {
                Id = Guid.NewGuid(),
                Name = name,
                InstallDirectory = install,
            };
            game.ActionPaths.AddRange(actions);
            return game;
        }

        private static LunaGame Luna(string path, string title = null)
        {
            return new LunaGame { Uid = path, GamePath = path, Title = title };
        }

        [Fact]
        public void Matches_on_the_executable_path()
        {
            var playnite = Game("モザイクの天使", @"D:\Games\Mosaic", @"D:\Games\Mosaic\mosaic.exe");
            var luna = Luna(@"d:\games\mosaic\MOSAIC.EXE", "別の名前");

            var result = Matcher.Match(new[] { luna }, new[] { playnite })[0];

            Assert.Equal(MatchKind.Path, result.Kind);
            Assert.Same(playnite, result.Playnite);
        }

        [Fact]
        public void Matches_a_relative_action_path_against_the_install_directory()
        {
            var playnite = Game("A", @"D:\Games\A", "a.exe");
            var result = Matcher.Match(new[] { Luna(@"D:\Games\A\a.exe") }, new[] { playnite })[0];

            Assert.Equal(MatchKind.Path, result.Kind);
        }

        [Fact]
        public void Matches_the_game_hiding_behind_locale_emulator()
        {
            // GalCompanion が LE 変換した後は Path が LEProc.exe になる
            var playnite = Game("モザイクの天使", @"D:\Games\Mosaic",
                @"C:\LE\LEProc.exe", @"D:\Games\Mosaic\mosaic.exe");

            var result = Matcher.Match(new[] { Luna(@"D:\Games\Mosaic\mosaic.exe") }, new[] { playnite })[0];

            Assert.Equal(MatchKind.Path, result.Kind);
        }

        [Fact]
        public void Falls_back_to_the_title_when_the_path_moved()
        {
            var playnite = Game("モザイクの天使", @"E:\NewPlace", @"E:\NewPlace\mosaic.exe");
            var result = Matcher.Match(
                new[] { Luna(@"D:\Old\mosaic.exe", "モザイクの天使") }, new[] { playnite })[0];

            Assert.Equal(MatchKind.Title, result.Kind);
            Assert.Same(playnite, result.Playnite);
        }

        [Fact]
        public void Uses_the_file_name_as_the_title_when_luna_has_none()
        {
            var playnite = Game("mosaic", @"E:\NewPlace", @"E:\NewPlace\other.exe");
            var result = Matcher.Match(new[] { Luna(@"D:\Old\mosaic.exe") }, new[] { playnite })[0];

            // DisplayName が "mosaic.exe" になるので、拡張子ぶん一致しない
            Assert.Equal(MatchKind.None, result.Kind);
        }

        [Fact]
        public void Title_matching_ignores_width_case_and_spacing()
        {
            var playnite = Game("Mosaic  no Tenshi", @"E:\x", @"E:\x\z.exe");
            var result = Matcher.Match(
                new[] { Luna(@"D:\Old\a.exe", "ＭＯＳＡＩＣ NO TENSHI") }, new[] { playnite })[0];

            Assert.Equal(MatchKind.Title, result.Kind);
        }

        [Fact]
        public void Duplicate_titles_are_not_matched_by_title()
        {
            var first = Game("同名", @"E:\a", @"E:\a\a.exe");
            var second = Game("同名", @"E:\b", @"E:\b\b.exe");

            var result = Matcher.Match(new[] { Luna(@"D:\Old\x.exe", "同名") }, new[] { first, second })[0];

            // どちらか分からないものを黙って選ばない
            Assert.Equal(MatchKind.None, result.Kind);
        }

        [Fact]
        public void Path_wins_over_title()
        {
            var byPath = Game("違う名前", @"D:\Games\A", @"D:\Games\A\a.exe");
            var byTitle = Game("モザイクの天使", @"E:\b", @"E:\b\b.exe");

            var result = Matcher.Match(
                new[] { Luna(@"D:\Games\A\a.exe", "モザイクの天使") },
                new[] { byPath, byTitle })[0];

            Assert.Equal(MatchKind.Path, result.Kind);
            Assert.Same(byPath, result.Playnite);
        }

        [Fact]
        public void Unmatched_games_are_still_reported()
        {
            var result = Matcher.Match(new[] { Luna(@"D:\x\y.exe", "無い") }, new List<PlayniteGame>())[0];

            Assert.Equal(MatchKind.None, result.Kind);
            Assert.Null(result.Playnite);
        }

        [Fact]
        public void CandidatePaths_deduplicates()
        {
            var game = Game("A", @"D:\Games\A", "a.exe", @"D:\Games\A\a.exe");
            Assert.Single(Matcher.CandidatePaths(game));
        }

        [Fact]
        public void NormalizeTitle_strips_punctuation()
        {
            Assert.Equal("abc", Matcher.NormalizeTitle(" A-B_C! "));
            Assert.Equal(string.Empty, Matcher.NormalizeTitle("   "));
        }
    }
}
