using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace GalCompanion.Tests
{
    public class ConfigViewModelTests
    {
        private static ConfigViewModel Create(
            GalCompanionConfig loaded = null, List<GalCompanionConfig> saved = null)
        {
            var sink = saved ?? new List<GalCompanionConfig>();
            return new ConfigViewModel(() => loaded, config => sink.Add(config));
        }

        [Fact]
        public void Uses_defaults_when_nothing_is_saved()
        {
            var vm = Create();

            Assert.NotNull(vm.Settings);
            Assert.Equal("Shift+F12", vm.Settings.Hotkey);
            Assert.NotNull(vm.Settings.SaveRules);
        }

        [Fact]
        public void Repairs_a_config_with_null_save_rules()
        {
            var vm = Create(new GalCompanionConfig { SaveRules = null });

            Assert.NotNull(vm.Settings.SaveRules);
        }

        [Fact]
        public void CancelEdit_restores_the_values_from_before_editing()
        {
            var vm = Create(new GalCompanionConfig { TriliumUrl = "https://before.example.com" });

            vm.BeginEdit();
            vm.Settings.TriliumUrl = "https://typo.example.com";
            vm.Settings.BubbleOpacity = 0.9;
            vm.CancelEdit();

            Assert.Equal("https://before.example.com", vm.Settings.TriliumUrl);
            Assert.Equal(0.55, vm.Settings.BubbleOpacity);
        }

        [Fact]
        public void EndEdit_saves_the_current_settings()
        {
            var saved = new List<GalCompanionConfig>();
            var vm = Create(new GalCompanionConfig(), saved);

            vm.BeginEdit();
            vm.Settings.TriliumUrl = "https://kept.example.com";
            vm.EndEdit();

            Assert.Single(saved);
            Assert.Equal("https://kept.example.com", saved[0].TriliumUrl);
        }

        [Fact]
        public void CancelEdit_after_EndEdit_does_not_roll_back()
        {
            var vm = Create(new GalCompanionConfig());

            vm.BeginEdit();
            vm.Settings.TriliumUrl = "https://kept.example.com";
            vm.EndEdit();
            vm.CancelEdit();

            Assert.Equal("https://kept.example.com", vm.Settings.TriliumUrl);
        }

        [Fact]
        public void ResetBubblePosition_clears_the_saved_coordinates()
        {
            var vm = Create(new GalCompanionConfig { BubbleX = 3000, BubbleY = 500 });

            vm.ResetBubblePosition();

            Assert.Null(vm.Settings.BubbleX);
            Assert.Null(vm.Settings.BubbleY);
        }

        [Fact]
        public void VerifySettings_passes_for_an_untouched_config()
        {
            var vm = Create();

            List<string> errors;
            Assert.True(vm.VerifySettings(out errors));
            Assert.Empty(errors);
        }

        [Fact]
        public void VerifySettings_reports_an_incomplete_trilium_setup()
        {
            var vm = Create(new GalCompanionConfig { TriliumEnabled = true });

            List<string> errors;
            Assert.False(vm.VerifySettings(out errors));
            Assert.Equal(2, errors.Count);
        }
    }

    public class ConfigValidationTests
    {
        [Fact]
        public void Untouched_config_is_valid()
        {
            Assert.Empty(new GalCompanionConfig().Validate());
        }

        [Fact]
        public void Bad_hotkey_is_reported()
        {
            var errors = new GalCompanionConfig
            {
                HotkeyEnabled = true,
                Hotkey = "Ctrl+NotAKey",
            }.Validate();

            Assert.Contains(errors, e => e.Contains("熱鍵"));
        }

        [Fact]
        public void Empty_hotkey_is_allowed()
        {
            Assert.Empty(new GalCompanionConfig { HotkeyEnabled = true, Hotkey = "" }.Validate());
        }

        // 使わない設定でエラーを出さない。切ってあるなら中身が壊れていても構わない
        [Fact]
        public void A_broken_hotkey_is_not_reported_while_it_is_switched_off()
        {
            Assert.Empty(new GalCompanionConfig { Hotkey = "Ctrl+NotAKey" }.Validate());
        }

        [Fact]
        public void UsesHotkey_needs_both_the_switch_and_a_key()
        {
            Assert.False(new GalCompanionConfig { Hotkey = "Shift+F12" }.UsesHotkey);
            Assert.False(new GalCompanionConfig { HotkeyEnabled = true, Hotkey = " " }.UsesHotkey);
            Assert.True(new GalCompanionConfig { HotkeyEnabled = true, Hotkey = "Shift+F12" }.UsesHotkey);
        }

        [Theory]
        [InlineData("auto")]
        [InlineData("printwindow")]
        [InlineData("screencrop")]
        public void Known_capture_modes_pass(string mode)
        {
            Assert.Empty(new GalCompanionConfig { CaptureMode = mode }.Validate());
        }

        [Fact]
        public void Unknown_capture_mode_is_reported()
        {
            var errors = new GalCompanionConfig { CaptureMode = "magic" }.Validate();
            Assert.Contains(errors, e => e.Contains("截圖模式"));
        }

        [Theory]
        [InlineData(0.05)]
        [InlineData(1.5)]
        public void Opacity_out_of_range_is_reported(double opacity)
        {
            var errors = new GalCompanionConfig { BubbleOpacity = opacity }.Validate();
            Assert.Contains(errors, e => e.Contains("透明度"));
        }

        [Fact]
        public void Trilium_enabled_without_url_or_token_is_reported()
        {
            var errors = new GalCompanionConfig { TriliumEnabled = true }.Validate();

            Assert.Contains(errors, e => e.Contains("伺服器網址"));
            Assert.Contains(errors, e => e.Contains("token"));
        }

        [Fact]
        public void Trilium_url_must_be_http()
        {
            var errors = new GalCompanionConfig
            {
                TriliumEnabled = true,
                TriliumUrl = "nas:8080",
                TriliumToken = "t"
            }.Validate();

            Assert.Contains(errors, e => e.Contains("http://"));
        }

        [Fact]
        public void Complete_trilium_setup_is_valid()
        {
            Assert.Empty(new GalCompanionConfig
            {
                TriliumEnabled = true,
                TriliumUrl = "https://trilium.example.com",
                TriliumToken = "token"
            }.Validate());
        }

        [Fact]
        public void Save_sync_without_remote_is_reported()
        {
            var errors = new GalCompanionConfig { SaveSyncEnabled = true }.Validate();
            Assert.Contains(errors, e => e.Contains("rclone remote"));
        }

        [Fact]
        public void Tolerance_below_three_seconds_is_reported()
        {
            var errors = new GalCompanionConfig { SaveSyncToleranceSeconds = 2 }.Validate();
            Assert.Contains(errors, e => e.Contains("容差"));
        }

        [Fact]
        public void Clone_copies_everything_and_is_independent()
        {
            var original = new GalCompanionConfig
            {
                Hotkey = "Ctrl+F1",
                HotkeyEnabled = true,
                BubbleX = 10,
                BubbleY = 20,
                TriliumEnabled = true,
                TriliumUrl = "https://a.example.com",
                TriliumToken = "tok",
                SaveSyncEnabled = true,
                RcloneRemote = "nas:saves",
                LocaleEmulatorPath = @"C:\LE\LEProc.exe"
            };
            original.SaveRules["g1"] = new SaveRule();

            var clone = original.Clone();

            Assert.Equal("Ctrl+F1", clone.Hotkey);
            Assert.True(clone.HotkeyEnabled);
            Assert.Equal(10, clone.BubbleX);
            Assert.Equal("https://a.example.com", clone.TriliumUrl);
            Assert.Equal("nas:saves", clone.RcloneRemote);
            Assert.Equal(@"C:\LE\LEProc.exe", clone.LocaleEmulatorPath);
            Assert.Single(clone.SaveRules);

            clone.TriliumUrl = "https://changed.example.com";
            clone.SaveRules.Clear();
            Assert.Equal("https://a.example.com", original.TriliumUrl);
            Assert.Single(original.SaveRules);
        }

        // Clone に足し忘れると設定 UI で「保存したのに戻る」項目が出るので全プロパティを機械的に確認する
        [Fact]
        public void Clone_covers_every_property()
        {
            var original = new GalCompanionConfig();
            var props = typeof(GalCompanionConfig)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .ToList();

            Assert.NotEmpty(props);
            foreach (var prop in props)
            {
                prop.SetValue(original, DistinctValue(prop.PropertyType, prop.Name));
            }

            var clone = original.Clone();

            foreach (var prop in props)
            {
                var expected = prop.GetValue(original);
                var actual = prop.GetValue(clone);
                if (expected is Dictionary<string, SaveRule> dict)
                {
                    Assert.Equal(dict.Count, ((Dictionary<string, SaveRule>)actual).Count);
                    continue;
                }
                Assert.True(Equals(expected, actual), $"Clone 沒有複製 {prop.Name}");
            }
        }

        private static object DistinctValue(Type type, string name)
        {
            if (type == typeof(string))
            {
                return "v-" + name;
            }
            if (type == typeof(bool))
            {
                return true;
            }
            if (type == typeof(int))
            {
                return 17;
            }
            if (type == typeof(double))
            {
                return 0.42;
            }
            if (type == typeof(double?))
            {
                return (double?)12.5;
            }
            if (type == typeof(Dictionary<string, SaveRule>))
            {
                return new Dictionary<string, SaveRule> { { "g-" + name, new SaveRule() } };
            }
            throw new InvalidOperationException(
                $"{name} は {type} 型。DistinctValue に対応を足すこと");
        }

        [Fact]
        public void Setting_a_property_raises_property_changed()
        {
            var config = new GalCompanionConfig();
            var raised = new List<string>();
            config.PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            config.TriliumUrl = "https://x.example.com";
            config.ShowBubble = false;

            Assert.Contains("TriliumUrl", raised);
            Assert.Contains("ShowBubble", raised);
        }
    }
}
