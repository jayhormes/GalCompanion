using System;

namespace GalCompanion
{
    /// <summary>
    /// 1 回の起動から終了まで。二台で遊んだぶんを突き合わせるときの単位なので、
    /// 累計ではなくこれを正とする。
    /// </summary>
    internal sealed class PlaySession
    {
        public Guid GameId;

        /// <summary>開始時刻（UTC・秒精度）。同一セッションの判定に使うので丸めを揃える。</summary>
        public DateTime StartUtc;

        public int Seconds;

        /// <summary>どの機械で遊んだか。合流したあとで由来が分かるように残す。</summary>
        public string Device;

        public string GameName;

        public DateTime EndUtc => StartUtc.AddSeconds(Seconds);

        /// <summary>同一セッションの判定キー。ゲーム＋開始時刻（秒）で一意とみなす。</summary>
        public string Key => GameId.ToString("N") + "|" + StartUtc.ToString("yyyyMMddHHmmss");
    }
}
