using Xunit;

namespace GalCompanion.Tests
{
    public class PathUtilTests
    {
        [Theory]
        [InlineData(@"D:\Games\A\a.exe", @"d:\games\a\a.exe")]
        [InlineData(@"D:/Games/A/a.exe", @"d:\games\a\a.exe")]
        [InlineData("\"D:\\Games\\A\\a.exe\"", @"d:\games\a\a.exe")]
        [InlineData(@"D:\Games\A\", @"d:\games\a")]
        [InlineData("  ", "")]
        [InlineData(null, "")]
        public void Normalize_ignores_case_separators_quotes_and_trailing_slash(string input, string expected)
        {
            Assert.Equal(expected, PathUtil.Normalize(input));
        }

        [Fact]
        public void Normalize_keeps_a_bare_drive_root()
        {
            Assert.Equal(@"d:\", PathUtil.Normalize(@"D:\"));
        }

        [Fact]
        public void FileName_takes_the_last_segment()
        {
            Assert.Equal("a.exe", PathUtil.FileName(@"D:\Games\A\a.exe"));
            Assert.Equal("A", PathUtil.FileName(@"D:\Games\A\"));
            Assert.Equal(string.Empty, PathUtil.FileName(null));
        }

        [Fact]
        public void Resolve_prefixes_relative_paths_with_the_install_directory()
        {
            Assert.Equal(@"D:\Games\A\a.exe", PathUtil.Resolve("a.exe", @"D:\Games\A"));
            Assert.Equal(@"D:\Games\A\a.exe", PathUtil.Resolve(@"\a.exe", @"D:\Games\A\"));
        }

        [Fact]
        public void Resolve_leaves_absolute_and_unc_paths_alone()
        {
            Assert.Equal(@"C:\x\a.exe", PathUtil.Resolve(@"C:\x\a.exe", @"D:\Games\A"));
            Assert.Equal(@"\\nas\x\a.exe", PathUtil.Resolve(@"\\nas\x\a.exe", @"D:\Games\A"));
        }

        [Fact]
        public void Resolve_without_an_install_directory_returns_the_input()
        {
            Assert.Equal("a.exe", PathUtil.Resolve("a.exe", null));
        }

        [Fact]
        public void ExtractExecutables_finds_the_game_behind_locale_emulator()
        {
            var exes = PathUtil.ExtractExecutables(
                "-runas \"{2ea51d5c}\" \"D:\\Games\\Mosaic\\mosaic.exe\"");

            Assert.Single(exes);
            Assert.Equal(@"D:\Games\Mosaic\mosaic.exe", exes[0]);
        }

        [Fact]
        public void ExtractExecutables_handles_unquoted_arguments()
        {
            var exes = PathUtil.ExtractExecutables(@"C:\a\b.exe --flag");
            Assert.Equal(new[] { @"C:\a\b.exe" }, exes);
        }

        [Fact]
        public void ExtractExecutables_returns_nothing_for_plain_arguments()
        {
            Assert.Empty(PathUtil.ExtractExecutables("--windowed --lang ja"));
            Assert.Empty(PathUtil.ExtractExecutables(null));
        }

        [Fact]
        public void Tokenize_keeps_quoted_paths_with_spaces_together()
        {
            var tokens = PathUtil.Tokenize("\"C:\\Program Files\\a b.exe\" x");
            Assert.Equal(new[] { @"C:\Program Files\a b.exe", "x" }, tokens);
        }
    }
}
