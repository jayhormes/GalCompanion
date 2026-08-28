using System;
using System.Threading.Tasks;

namespace GalCompanion
{
    internal sealed class TriliumService : IDisposable
    {
        private readonly TriliumClient client;

        public TriliumService(TriliumClient client)
        {
            this.client = client;
        }

        public async Task<string> EnsureGameNoteAsync(string parentNoteId, string gameTitle, string boundNoteId)
        {
            if (!string.IsNullOrEmpty(boundNoteId))
            {
                return boundNoteId;
            }
            if (string.IsNullOrWhiteSpace(parentNoteId))
            {
                throw new InvalidOperationException("未設定 TriliumParentNoteId，無法自動建立遊戲筆記");
            }
            return await client.CreateNoteAsync(parentNoteId, gameTitle).ConfigureAwait(false);
        }

        // pngBytes 或 text 至少一個；圖先上傳成 attachment，再把整段 entry 接到 note 尾端
        public async Task AppendEntryAsync(string noteId, DateTime timestamp, byte[] pngBytes, string text)
        {
            string attachmentId = null;
            string imageTitle = null;
            if (pngBytes != null)
            {
                imageTitle = timestamp.ToString("yyyyMMdd_HHmmss_fff") + ".png";
                attachmentId = await client.CreateAttachmentAsync(noteId, imageTitle, "image/png", pngBytes).ConfigureAwait(false);
            }

            var entry = TriliumHtml.BuildEntry(timestamp, attachmentId, imageTitle, text);
            var content = await client.GetNoteContentAsync(noteId).ConfigureAwait(false) ?? string.Empty;
            await client.SetNoteContentAsync(noteId, content + entry).ConfigureAwait(false);
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}
