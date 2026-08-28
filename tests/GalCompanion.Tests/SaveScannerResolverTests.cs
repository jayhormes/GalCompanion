using System;
using System.Collections.Generic;
using Xunit;

namespace GalCompanion.Tests
{
    public class SaveScannerResolverTests
    {
        [Fact]
        public void Scanner_returns_latest_mtime_across_paths()
        {
            using (var dir = new TempDir())
            {
                var older = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
                var newer = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc);
                dir.WriteFile("saves/a.sav", "x", older);
                var file = dir.WriteFile("b.dat", "y", newer);

                var latest = SaveScanner.GetLatestWriteUtc(new List<string> { dir.Sub("saves"), file });
                Assert.Equal(newer, latest);
            }
        }

        [Fact]
        public void Scanner_returns_null_when_nothing_exists()
        {
            using (var dir = new TempDir())
            {
                Assert.Null(SaveScanner.GetLatestWriteUtc(new List<string> { dir.Sub("nope") }));
            }
        }

        [Fact]
        public void Resolver_replaces_gamedir_token()
        {
            var result = SavePathResolver.Resolve(new[] { @"{GameDir}\savedata" }, @"D:\Games\Foo");
            Assert.Equal(@"D:\Games\Foo\savedata", Assert.Single(result));
        }

        [Fact]
        public void Resolver_expands_environment_variables()
        {
            Environment.SetEnvironmentVariable("GALCOMP_TEST", "expanded");
            var result = SavePathResolver.Resolve(new[] { @"%GALCOMP_TEST%\x" }, null);
            Assert.Equal(@"expanded\x", Assert.Single(result));
        }

        [Fact]
        public void Resolver_filters_blank_entries()
        {
            var result = SavePathResolver.Resolve(new[] { "", "  ", null, @"C:\ok" }, null);
            Assert.Equal(@"C:\ok", Assert.Single(result));
        }
    }
}
