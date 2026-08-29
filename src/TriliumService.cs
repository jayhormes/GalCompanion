using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GalCompanion
{
    internal enum TriliumTarget
    {
        // 📷 截圖 → 當天的「遊戲名 遊戲心得」
        Impressions,
        // 📝 文字 → 心得底下的翻譯問題
        Translation
    }

    internal sealed class TriliumService : IDisposable
    {
        private readonly TriliumClient client;
        private readonly string dateFormat;
        private readonly string impressionsTemplate;
        private readonly string translationTemplate;
        private readonly bool prefixWithGame;

        public TriliumService(TriliumClient client, string dateFormat,
            string impressionsTitle, string translationTitle, bool notePerGame = true)
        {
            this.prefixWithGame = notePerGame;
            this.client = client;
            this.dateFormat = string.IsNullOrWhiteSpace(dateFormat) ? "yyyy.MM.dd" : dateFormat;
            this.impressionsTemplate = string.IsNullOrWhiteSpace(impressionsTitle)
                ? TriliumTitles.DefaultImpressions : impressionsTitle;
            this.translationTemplate = string.IsNullOrWhiteSpace(translationTitle)
                ? TriliumTitles.DefaultTranslation : translationTitle;
        }

        /// <summary>心得ノートの標題にゲーム名が入るか。エントリ見出しの重複を避けるのに使う。</summary>
        public bool ImpressionsArePerGame
            => TriliumTitles.CarriesGameName(impressionsTemplate, prefixWithGame);

        public string ImpressionsTitleFor(string gameName)
        {
            return TriliumTitles.Format(
                impressionsTemplate, gameName, TriliumTitles.DefaultImpressions, prefixWithGame);
        }

        // 翻譯問題は心得ノートの子なので、ゲーム名を重ねない（{game} と自分で書いたときだけ入る）
        public string TranslationTitleFor(string gameName)
        {
            return TriliumTitles.Format(
                translationTemplate, gameName, TriliumTitles.DefaultTranslation);
        }

        public string FormatDate(DateTime date)
        {
            return date.ToString(dateFormat);
        }

        /// <summary>
        /// 既存の日記ノート（例「2026.08.28 星期五 (Week35) - 晨間日記」）を探す。
        /// Trilium の全文検索は曖昧一致なので、日付で始まるタイトルだけを採用する。
        /// 見つからなければ fallbackParentNoteId の下に日付ノートを作る。
        /// </summary>
        public async Task<string> EnsureDateNoteAsync(DateTime date, string fallbackParentNoteId)
        {
            // 1. Trilium 内蔵の日付ノート。ユーザーの Journal 構造（年/月/日）にそのまま乗る
            var dayNoteId = await client.GetDayNoteIdAsync(date).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(dayNoteId))
            {
                return dayNoteId;
            }

            // 2. Journal 未設定などで使えないときだけ、タイトル検索へ退避
            var dateKey = FormatDate(date);

            var hits = await client.SearchNotesAsync("\"" + dateKey + "\"", 30).ConfigureAwait(false);
            var match = PickDateNote(hits, dateKey);
            if (match != null)
            {
                return match;
            }

            if (string.IsNullOrWhiteSpace(fallbackParentNoteId))
            {
                throw new InvalidOperationException(
                    $"Trilium 沒有回傳 {dateKey} 的日期筆記，標題也搜不到，"
                    + "且未設定「找不到當天日記時的父 note id」可供建立");
            }
            return await client.CreateNoteAsync(fallbackParentNoteId, dateKey).ConfigureAwait(false);
        }

        // 検索結果は曖昧一致を含むため、日付で始まるものだけを正解とする
        internal static string PickDateNote(List<TriliumNote> hits, string dateKey)
        {
            if (hits == null || string.IsNullOrEmpty(dateKey))
            {
                return null;
            }
            foreach (var note in hits)
            {
                var title = (note.Title ?? string.Empty).TrimStart();
                if (title.StartsWith(dateKey, StringComparison.Ordinal))
                {
                    return note.NoteId;
                }
            }
            return null;
        }

        public async Task<string> EnsureChildNoteAsync(string parentNoteId, string title)
        {
            var childIds = await client.GetChildNoteIdsAsync(parentNoteId).ConfigureAwait(false);
            foreach (var childId in childIds)
            {
                var childTitle = await client.GetNoteTitleAsync(childId).ConfigureAwait(false);
                if (string.Equals(childTitle, title, StringComparison.Ordinal))
                {
                    return childId;
                }
            }
            return await client.CreateNoteAsync(parentNoteId, title).ConfigureAwait(false);
        }

        /// <summary>
        /// 日期 →「ゲーム名 遊戲心得」→「翻譯問題」を解決して、書き込み先の noteId を返す。
        /// </summary>
        public async Task<string> ResolveTargetNoteAsync(
            DateTime date, string fallbackParentNoteId, TriliumTarget target, string gameName)
        {
            var dateNoteId = await EnsureDateNoteAsync(date, fallbackParentNoteId).ConfigureAwait(false);
            var impressionsId = await EnsureChildNoteAsync(
                dateNoteId, ImpressionsTitleFor(gameName)).ConfigureAwait(false);

            if (target == TriliumTarget.Impressions)
            {
                return impressionsId;
            }
            return await EnsureChildNoteAsync(
                impressionsId, TranslationTitleFor(gameName)).ConfigureAwait(false);
        }

        // pngBytes 或 text 至少一個；圖先上傳成 attachment，再把整段 entry 接到 note 尾端
        public async Task AppendEntryAsync(
            string noteId, DateTime timestamp, string gameName, byte[] pngBytes, string text)
        {
            string attachmentId = null;
            string imageTitle = null;
            if (pngBytes != null)
            {
                imageTitle = timestamp.ToString("yyyyMMdd_HHmmss_fff") + ".png";
                attachmentId = await client.CreateAttachmentAsync(noteId, imageTitle, "image/png", pngBytes).ConfigureAwait(false);
            }

            var entry = TriliumHtml.BuildEntry(timestamp, gameName, attachmentId, imageTitle, text);
            var content = await client.GetNoteContentAsync(noteId).ConfigureAwait(false) ?? string.Empty;
            await client.SetNoteContentAsync(noteId, content + entry).ConfigureAwait(false);
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}
