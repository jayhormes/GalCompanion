using System;
using Xunit;

namespace GalCompanion.Tests
{
    public class SyncManifestStateTests
    {
        [Fact]
        public void Manifest_roundtrips_through_json()
        {
            var original = new SyncManifest
            {
                TimestampUtc = new DateTime(2026, 8, 28, 13, 45, 30, DateTimeKind.Utc),
                Device = "ROG-Ally \"主機\"",
                FileCount = 12
            };
            var parsed = SyncManifest.FromJson(original.ToJson());

            Assert.Equal(original.TimestampUtc, parsed.TimestampUtc);
            Assert.Equal(original.Device, parsed.Device);
            Assert.Equal(12, parsed.FileCount);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("{}")]
        [InlineData("{\"timestamp\":\"not-a-date\"}")]
        public void Manifest_returns_null_on_bad_json(string json)
        {
            Assert.Null(SyncManifest.FromJson(json));
        }

        [Fact]
        public void StateStore_roundtrips_last_synced()
        {
            using (var dir = new TempDir())
            {
                var store = new SyncStateStore(dir.Sub("state"));
                var gameId = "11111111-2222-3333-4444-555555555555";

                Assert.Null(store.GetLastSynced(gameId));

                var ts = new DateTime(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc);
                store.SetLastSynced(gameId, ts);
                Assert.Equal(ts, store.GetLastSynced(gameId));
            }
        }
    }
}
