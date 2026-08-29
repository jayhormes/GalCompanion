using System;
using System.IO;
using System.Linq;
using Xunit;

namespace LunaImport.Tests
{
    public class LunaReaderTests
    {
        private static string Fixtures =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures");

        [Fact]
        public void PickGameListFile_prefers_the_newest_current_format()
        {
            var picked = LunaReader.PickGameListFile(new[]
            {
                "savehook_new_1.39.4.json",
                "savegamedata_5.2.0.json",
                "savegamedata_5.3.1.json",
                "config.json",
            });

            Assert.Equal("savegamedata_5.3.1.json", picked);
        }

        [Fact]
        public void PickGameListFile_falls_back_to_the_legacy_name()
        {
            Assert.Equal("savehook_new_1.39.4.json",
                LunaReader.PickGameListFile(new[] { "savehook_new_1.39.4.json", "config.json" }));
        }

        [Fact]
        public void PickGameListFile_returns_null_when_there_is_nothing()
        {
            Assert.Null(LunaReader.PickGameListFile(new[] { "config.json" }));
        }

        [Fact]
        public void ParseGameList_reads_uid_path_and_title()
        {
            var games = LunaReader.ParseGameList(
                "[[\"u1\"],{\"u1\":{\"gamepath\":\"D:\\\\a\\\\a.exe\",\"title\":\"タイトル\"}}]");

            Assert.Equal(@"D:\a\a.exe", games["u1"].GamePath);
            Assert.Equal("タイトル", games["u1"].Title);
        }

        [Fact]
        public void ParseGameList_handles_the_legacy_shape_where_the_key_is_the_path()
        {
            var games = LunaReader.ParseGameList("[[\"D:\\\\a\\\\a.exe\"],{\"D:\\\\a\\\\a.exe\":{}}]");

            Assert.Equal(@"D:\a\a.exe", games[@"D:\a\a.exe"].GamePath);
        }

        [Fact]
        public void ParseGameList_survives_an_unexpected_shape()
        {
            Assert.Empty(LunaReader.ParseGameList("{}"));
            Assert.Empty(LunaReader.ParseGameList("[]"));
        }

        [Fact]
        public void FromUnix_converts_to_local_time()
        {
            var utc = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
            var seconds = (utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

            Assert.Equal(utc, LunaReader.FromUnix(seconds).ToUniversalTime());
        }

        // --- 実物と同じ形の sqlite を読む ---

        [Fact]
        public void Load_joins_the_sessions_onto_the_games()
        {
            var games = LunaReader.Load(Fixtures);

            var mosaic = games.Single(g => g.Uid == "uid-a");
            Assert.Equal("モザイクの天使", mosaic.Title);
            // 7200 + 1800、長さ 0 のセッションは捨てる
            Assert.Equal(2, mosaic.Sessions.Count);
            Assert.Equal(9000, mosaic.TotalSeconds);
        }

        [Fact]
        public void Load_uses_the_file_name_when_luna_has_no_title()
        {
            var kotone = LunaReader.Load(Fixtures).Single(g => g.Uid == "uid-b");

            Assert.Equal("kotone.exe", kotone.DisplayName);
            Assert.Equal(2700, kotone.TotalSeconds);
        }

        [Fact]
        public void Load_ignores_sessions_whose_game_is_not_in_the_list()
        {
            var games = LunaReader.Load(Fixtures);

            Assert.Equal(2, games.Count);
            Assert.DoesNotContain(games, g => g.Uid == "uid-orphan");
        }

        [Fact]
        public void Load_returns_the_longest_played_first()
        {
            var games = LunaReader.Load(Fixtures);
            Assert.Equal("uid-a", games[0].Uid);
        }
    }
}
