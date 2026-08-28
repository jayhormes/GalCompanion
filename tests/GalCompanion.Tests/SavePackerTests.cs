using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace GalCompanion.Tests
{
    public class SavePackerTests
    {
        [Fact]
        public void Pack_and_unpack_roundtrip_preserves_content_and_mtime()
        {
            using (var src = new TempDir())
            using (var work = new TempDir())
            using (var dst = new TempDir())
            {
                var mtime = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
                src.WriteFile("saves/data1.sav", "AAA", mtime);
                src.WriteFile("saves/sub/data2.sav", "BBB", mtime);
                var singleFile = src.WriteFile("global.dat", "CCC", mtime);

                var paths = new List<string> { src.Sub("saves"), singleFile };
                var zip = work.Sub("out.zip");
                var count = SavePacker.Pack(paths, zip);
                Assert.Equal(3, count);

                var restorePaths = new List<string> { dst.Sub("saves"), dst.Sub("global.dat") };
                SavePacker.Unpack(zip, restorePaths);

                Assert.Equal("AAA", File.ReadAllText(dst.Sub("saves", "data1.sav")));
                Assert.Equal("BBB", File.ReadAllText(dst.Sub("saves", "sub", "data2.sav")));
                Assert.Equal("CCC", File.ReadAllText(dst.Sub("global.dat")));

                // zip 時間戳解析度 2 秒
                var restoredMtime = File.GetLastWriteTimeUtc(dst.Sub("saves", "data1.sav"));
                Assert.True((restoredMtime - mtime).Duration() <= TimeSpan.FromSeconds(2));
            }
        }

        [Fact]
        public void Pack_skips_missing_paths()
        {
            using (var src = new TempDir())
            using (var work = new TempDir())
            {
                var file = src.WriteFile("a.sav", "X");
                var count = SavePacker.Pack(
                    new List<string> { file, src.Sub("does-not-exist") },
                    work.Sub("out.zip"));
                Assert.Equal(1, count);
            }
        }

        [Theory]
        [InlineData("p0/../evil.txt")]
        [InlineData("p0/sub/../../evil.txt")]
        [InlineData("q0/file.txt")]
        [InlineData("p9/file.txt")]
        [InlineData("p0/")]
        [InlineData("nofolder")]
        public void MapEntry_rejects_invalid_entries(string entry)
        {
            var paths = new List<string> { @"C:\game\saves" };
            Assert.Null(SavePacker.MapEntry(entry, paths));
        }

        [Fact]
        public void MapEntry_maps_directory_rule()
        {
            var paths = new List<string> { @"C:\game\saves" };
            Assert.Equal(@"C:\game\saves\sub\a.sav", SavePacker.MapEntry("p0/sub/a.sav", paths));
        }

        [Fact]
        public void MapEntry_maps_file_rule_to_original_file()
        {
            var paths = new List<string> { @"C:\game\global.dat" };
            Assert.Equal(@"C:\game\global.dat", SavePacker.MapEntry("p0/global.dat", paths));
        }
    }
}
