using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using Xunit;

namespace LunaImport.Tests
{
    public class PlayniteLibraryTests
    {
        private const string Original = @"{
  ""Id"": ""11111111-1111-1111-1111-111111111111"",
  ""Name"": ""モザイクの天使"",
  ""InstallDirectory"": ""D:\\Games\\Mosaic"",
  ""Playtime"": 0,
  ""PlayCount"": 0,
  ""Hidden"": false,
  ""GameActions"": [
    { ""Path"": ""C:\\LE\\LEProc.exe"", ""Arguments"": ""-runas \""{g}\"" \""D:\\Games\\Mosaic\\mosaic.exe\"""" }
  ],
  ""CustomField"": ""留著""
}";

        private static PlanEntry Entry(long seconds, int sessions, DateTime? last)
        {
            return new PlanEntry
            {
                Luna = new LunaGame(),
                Playnite = new PlayniteGame { Id = Guid.NewGuid(), Name = "x" },
                LunaSeconds = seconds,
                NewPlaytime = (ulong)seconds,
                SessionCount = sessions,
                LastSession = last,
            };
        }

        [Fact]
        public void ParseGame_reads_the_fields_and_both_action_paths()
        {
            var game = PlayniteLibrary.ParseGame(JObject.Parse(Original), "x.json");

            Assert.Equal("モザイクの天使", game.Name);
            Assert.Equal(@"D:\Games\Mosaic", game.InstallDirectory);
            Assert.Contains(@"C:\LE\LEProc.exe", game.ActionPaths);
            Assert.Contains(@"D:\Games\Mosaic\mosaic.exe", game.ActionPaths);
        }

        [Fact]
        public void ParseGame_rejects_a_file_without_an_id()
        {
            Assert.Null(PlayniteLibrary.ParseGame(JObject.Parse("{\"Name\":\"x\"}"), "x.json"));
        }

        [Fact]
        public void Patch_sets_playtime_and_leaves_everything_else_alone()
        {
            var patched = JObject.Parse(
                PlayniteLibrary.Patch(Original, Entry(7200, 3, new DateTime(2026, 1, 5, 21, 0, 0))));

            Assert.Equal(7200, (long)patched["Playtime"]);
            Assert.Equal("留著", (string)patched["CustomField"]);
            Assert.Equal("モザイクの天使", (string)patched["Name"]);
            Assert.False((bool)patched["Hidden"]);
        }

        [Fact]
        public void Patch_raises_play_count_to_the_session_count()
        {
            var patched = JObject.Parse(PlayniteLibrary.Patch(Original, Entry(60, 3, null)));
            Assert.Equal(3, (int)patched["PlayCount"]);
        }

        [Fact]
        public void Patch_never_lowers_an_existing_play_count()
        {
            var withCount = Original.Replace("\"PlayCount\": 0", "\"PlayCount\": 50");
            var patched = JObject.Parse(PlayniteLibrary.Patch(withCount, Entry(60, 3, null)));

            Assert.Equal(50, (int)patched["PlayCount"]);
        }

        [Fact]
        public void Patch_never_moves_last_activity_backwards()
        {
            var withActivity = Original.Replace(
                "\"Hidden\": false", "\"LastActivity\": \"2027-01-01T00:00:00.000Z\"");
            var patched = JObject.Parse(
                PlayniteLibrary.Patch(withActivity, Entry(60, 1, new DateTime(2026, 1, 5, 21, 0, 0))));

            Assert.StartsWith("2027-01-01", (string)patched["LastActivity"]);
        }

        [Fact]
        public void Patch_fills_in_a_missing_last_activity()
        {
            var patched = JObject.Parse(
                PlayniteLibrary.Patch(Original, Entry(60, 1, new DateTime(2026, 1, 5, 21, 0, 0, DateTimeKind.Utc))));

            Assert.StartsWith("2026-01-05T21:00:00", (string)patched["LastActivity"]);
        }
    }

    public class GameActivityWriterTests
    {
        private static PlanEntry Entry(params DateTime[] starts)
        {
            var luna = new LunaGame();
            foreach (var start in starts)
            {
                luna.Sessions.Add(new PlaySession { Start = start, End = start.AddHours(1) });
            }
            return new PlanEntry
            {
                Luna = luna,
                Playnite = new PlayniteGame
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Name = "モザイクの天使",
                },
            };
        }

        [Fact]
        public void Creates_the_file_shape_game_activity_expects()
        {
            var json = JObject.Parse(GameActivityWriter.Merge(
                null, Entry(new DateTime(2026, 1, 2, 20, 0, 0, DateTimeKind.Utc))));

            Assert.Equal("22222222-2222-2222-2222-222222222222", (string)json["Id"]);
            Assert.Equal("モザイクの天使", (string)json["Name"]);

            var item = ((JArray)json["Items"]).Single();
            Assert.Equal(3600, (long)item["ElapsedSeconds"]);
            Assert.StartsWith("2026-01-02T20:00:00", (string)item["DateSession"]);
            Assert.Equal(-1, (int)item["IdConfiguration"]);
        }

        [Fact]
        public void Appends_to_an_existing_file_without_dropping_its_sessions()
        {
            var existing = @"{""Id"":""22222222-2222-2222-2222-222222222222"",""Name"":""既存"",
                ""Items"":[{""DateSession"":""2025-12-01T10:00:00.000Z"",""ElapsedSeconds"":60}]}";

            var json = JObject.Parse(GameActivityWriter.Merge(
                existing, Entry(new DateTime(2026, 1, 2, 20, 0, 0, DateTimeKind.Utc))));

            Assert.Equal(2, ((JArray)json["Items"]).Count);
            Assert.Equal("既存", (string)json["Name"]);
        }

        [Fact]
        public void Running_the_import_twice_does_not_duplicate_sessions()
        {
            var entry = Entry(
                new DateTime(2026, 1, 2, 20, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 3, 20, 0, 0, DateTimeKind.Utc));

            var once = GameActivityWriter.Merge(null, entry);
            var twice = GameActivityWriter.Merge(once, entry);

            Assert.Equal(2, ((JArray)JObject.Parse(twice)["Items"]).Count);
        }

        [Fact]
        public void Sessions_are_written_oldest_first()
        {
            var json = JObject.Parse(GameActivityWriter.Merge(null, Entry(
                new DateTime(2026, 3, 1, 20, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 1, 20, 0, 0, DateTimeKind.Utc))));

            var items = (JArray)json["Items"];
            Assert.StartsWith("2026-01-01", (string)items[0]["DateSession"]);
        }
    }
}
