using Playnite.SDK;
using System;
using System.Collections.Generic;

namespace GalCompanion
{
    /// <summary>
    /// 設定画面のバインド先。ISettings は IEditableObject を継承するので、
    /// BeginEdit/CancelEdit/EndEdit で「キャンセルしたら元に戻す」を実装する。
    /// Playnite に依存しないよう load/save は委譲で受け取る（テスト用）。
    /// </summary>
    public class ConfigViewModel : ObservableObject, ISettings
    {
        private readonly Action<GalCompanionConfig> save;
        private GalCompanionConfig backup;
        private GalCompanionConfig settings;

        public GalCompanionConfig Settings
        {
            get => settings;
            set => SetValue(ref settings, value);
        }

        public ConfigViewModel(Func<GalCompanionConfig> load, Action<GalCompanionConfig> save)
        {
            this.save = save;
            Settings = load?.Invoke() ?? new GalCompanionConfig();
            if (Settings.SaveRules == null)
            {
                Settings.SaveRules = new Dictionary<string, SaveRule>();
            }
        }

        public void BeginEdit()
        {
            backup = Settings.Clone();
        }

        public void CancelEdit()
        {
            if (backup != null)
            {
                Settings = backup;
                backup = null;
            }
        }

        public void EndEdit()
        {
            backup = null;
            save?.Invoke(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = Settings.Validate();
            return errors.Count == 0;
        }

        /// <summary>
        /// 気泡ウィンドウの座標を捨てる。次に表示するとき作業領域の中央に出る。
        /// </summary>
        public void ResetBubblePosition()
        {
            Settings.BubbleX = null;
            Settings.BubbleY = null;
        }
    }
}
