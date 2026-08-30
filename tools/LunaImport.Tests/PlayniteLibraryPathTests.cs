using System.IO;
using Xunit;

namespace LunaImport.Tests
{
    public class PlayniteLibraryPathTests
    {
        [Fact]
        public void Default_layout_is_found()
        {
            using (var temp = new TempDir())
            {
                var expected = Path.Combine(temp.Path, "library", "games");
                Directory.CreateDirectory(expected);

                System.Collections.Generic.List<string> tried;
                Assert.Equal(expected, PlayniteLibrary.FindGamesDir(temp.Path, out tried));
            }
        }

        [Fact]
        public void Database_path_from_config_wins_when_default_is_missing()
        {
            using (var temp = new TempDir())
            {
                var moved = Path.Combine(temp.Path, "elsewhere", "library", "games");
                Directory.CreateDirectory(moved);
                File.WriteAllText(Path.Combine(temp.Path, "config.json"),
                    "{\"DatabasePath\": \"" + Path.Combine(temp.Path, "elsewhere", "library").Replace("\\", "\\\\") + "\"}");

                System.Collections.Generic.List<string> tried;
                Assert.Equal(moved, PlayniteLibrary.FindGamesDir(temp.Path, out tried));
            }
        }

        [Fact]
        public void Portable_variable_resolves_against_the_playnite_folder()
        {
            using (var temp = new TempDir())
            {
                var portable = Path.Combine(temp.Path, "library", "games");
                Directory.CreateDirectory(portable);
                File.WriteAllText(Path.Combine(temp.Path, "config.json"),
                    "{\"DatabasePath\": \"{PlayniteDir}\\\\library\"}");

                Assert.Equal(Path.Combine(temp.Path, "library"),
                    PlayniteLibrary.ExpandPath("{PlayniteDir}\\library", temp.Path));

                System.Collections.Generic.List<string> tried;
                Assert.Equal(portable, PlayniteLibrary.FindGamesDir(temp.Path, out tried));
            }
        }

        [Fact]
        public void Relative_database_path_is_resolved_against_the_playnite_folder()
        {
            using (var temp = new TempDir())
            {
                Assert.Equal(Path.Combine(temp.Path, "library"),
                    PlayniteLibrary.ExpandPath("library", temp.Path));
            }
        }

        [Fact]
        public void Environment_variables_are_expanded()
        {
            var expanded = PlayniteLibrary.ExpandPath("%TEMP%", @"C:\Playnite");

            Assert.DoesNotContain("%TEMP%", expanded);
            Assert.True(Path.IsPathRooted(expanded));
        }

        [Fact]
        public void Library_folder_passed_directly_is_accepted()
        {
            using (var temp = new TempDir())
            {
                var library = Path.Combine(temp.Path, "library");
                var games = Path.Combine(library, "games");
                Directory.CreateDirectory(games);

                System.Collections.Generic.List<string> tried;
                Assert.Equal(games, PlayniteLibrary.FindGamesDir(library, out tried));
            }
        }

        [Fact]
        public void Games_folder_passed_directly_is_accepted()
        {
            using (var temp = new TempDir())
            {
                var games = Path.Combine(temp.Path, "games");
                Directory.CreateDirectory(games);

                System.Collections.Generic.List<string> tried;
                Assert.Equal(games, PlayniteLibrary.FindGamesDir(games, out tried));
            }
        }

        [Fact]
        public void Missing_library_reports_every_path_it_looked_at()
        {
            using (var temp = new TempDir())
            {
                System.Collections.Generic.List<string> tried;
                Assert.Null(PlayniteLibrary.FindGamesDir(temp.Path, out tried));
                Assert.Contains(Path.Combine(temp.Path, "library", "games"), tried);
                Assert.NotEmpty(tried);
            }
        }

        [Fact]
        public void Broken_config_is_ignored_instead_of_throwing()
        {
            using (var temp = new TempDir())
            {
                File.WriteAllText(Path.Combine(temp.Path, "config.json"), "{ not json");

                Assert.Null(PlayniteLibrary.ReadConfiguredDatabasePath(temp.Path));
            }
        }

        [Fact]
        public void Config_without_database_path_is_ignored()
        {
            using (var temp = new TempDir())
            {
                File.WriteAllText(Path.Combine(temp.Path, "config.json"), "{\"DatabasePath\": \"  \"}");

                Assert.Null(PlayniteLibrary.ReadConfiguredDatabasePath(temp.Path));
            }
        }
    }
}
