using System;

namespace GalCompanion
{
    internal enum CapturePress
    {
        /// <summary>下書きモードが無効。そのまま撮る。</summary>
        CaptureNow,

        /// <summary>入力欄を開いて、撮影は次の押下まで待つ。</summary>
        OpenComposer,

        /// <summary>入力欄の内容を添えて撮る。</summary>
        Commit,
    }

    /// <summary>
    /// 📷 の押下を「開く → 撮る」の 2 段に振り分ける状態機械。
    /// 遊んでいる最中にその場で一言書けるようにするのが目的で、
    /// 何も書かなければ 📷 を 2 回押しただけと同じ結果になる。
    /// WPF に触らないのでそのままテストできる。
    /// </summary>
    internal sealed class CaptureComposer
    {
        private readonly Func<bool> enabled;

        public CaptureComposer(Func<bool> enabled)
        {
            this.enabled = enabled ?? (() => false);
        }

        public bool IsComposing { get; private set; }

        public CapturePress Press()
        {
            // 書きかけの途中で設定を切られても、開いた分は必ず送り切る
            if (IsComposing)
            {
                IsComposing = false;
                return CapturePress.Commit;
            }

            if (!enabled())
            {
                return CapturePress.CaptureNow;
            }

            IsComposing = true;
            return CapturePress.OpenComposer;
        }

        /// <summary>入力欄を閉じた／取り消した。開いていたなら true。</summary>
        public bool Cancel()
        {
            var was = IsComposing;
            IsComposing = false;
            return was;
        }
    }
}
