using System;

namespace GalCompanion
{
    internal enum CapturePress
    {
        /// <summary>下書きモードが無効。ボタン本来の動作をそのまま行う。</summary>
        CaptureNow,

        /// <summary>入力欄を開いて、記録は次の押下まで待つ。</summary>
        OpenComposer,

        /// <summary>入力欄の内容を添えて記録する。</summary>
        Commit,
    }

    internal struct ComposerPress
    {
        public CapturePress Action;

        /// <summary>Commit のときは「入力欄を開いた側」の宛先。書いている本人が見出しで見ているのはこちら。</summary>
        public TriliumTarget Target;
    }

    /// <summary>
    /// 📷 / 📝 の押下を「開く → 記録する」の 2 段に振り分ける状態機械。
    /// 遊んでいる最中にその場で一言書けるようにするのが目的で、
    /// 何も書かなければ同じボタンを 2 回押しただけと同じ結果になる。
    /// WPF に触らないのでそのままテストできる。
    /// </summary>
    internal sealed class CaptureComposer
    {
        private readonly Func<TriliumTarget, bool> enabled;

        public CaptureComposer(Func<TriliumTarget, bool> enabled)
        {
            this.enabled = enabled ?? (_ => false);
        }

        public bool IsComposing { get; private set; }

        /// <summary>入力欄を開いた側の宛先。開いていないときの値に意味はない。</summary>
        public TriliumTarget Target { get; private set; }

        public ComposerPress Press(TriliumTarget target)
        {
            // 書きかけの途中で設定を切られても、開いた分は必ず送り切る。
            // もう一方のボタンで送っても宛先は開いた側のまま
            if (IsComposing)
            {
                IsComposing = false;
                return new ComposerPress { Action = CapturePress.Commit, Target = Target };
            }

            if (!enabled(target))
            {
                return new ComposerPress { Action = CapturePress.CaptureNow, Target = target };
            }

            IsComposing = true;
            Target = target;
            return new ComposerPress { Action = CapturePress.OpenComposer, Target = target };
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
