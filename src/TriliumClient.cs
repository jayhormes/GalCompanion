using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace GalCompanion
{
    internal sealed class TriliumClient : IDisposable
    {
        private readonly HttpClient http;
        private readonly string baseUrl;

        public TriliumClient(string baseUrl, string token, HttpMessageHandler handler = null)
        {
            this.baseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
            http = handler == null ? new HttpClient() : new HttpClient(handler);
            http.Timeout = TimeSpan.FromSeconds(15);
            http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", token);
        }

        // Trilium の全文検索は曖昧一致（"2026.08.28" で 2015 年のノートも返る）。
        // 呼び出し側でタイトルを検証すること。
        public async Task<List<TriliumNote>> SearchNotesAsync(string query, int limit = 20)
        {
            var url = $"{baseUrl}/etapi/notes?search={Uri.EscapeDataString(query)}&limit={limit}";
            var resp = await http.GetAsync(url).ConfigureAwait(false);
            await EnsureSuccess(resp).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return ParseNoteList(body);
        }

        public async Task<List<string>> GetChildNoteIdsAsync(string noteId)
        {
            var resp = await http.GetAsync($"{baseUrl}/etapi/notes/{noteId}").ConfigureAwait(false);
            await EnsureSuccess(resp).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonUtil.ExtractStringArray(body, "childNoteIds");
        }

        public async Task<string> GetNoteTitleAsync(string noteId)
        {
            var resp = await http.GetAsync($"{baseUrl}/etapi/notes/{noteId}").ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonUtil.ExtractString(body, "title");
        }

        // results 配列から noteId/title の組を取り出す（順序は API の返す順）
        internal static List<TriliumNote> ParseNoteList(string json)
        {
            var notes = new List<TriliumNote>();
            foreach (System.Text.RegularExpressions.Match m in
                System.Text.RegularExpressions.Regex.Matches(json ?? string.Empty,
                    "\"noteId\"\\s*:\\s*\"([^\"]+)\"(?:(?!\"noteId\").)*?\"title\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"",
                    System.Text.RegularExpressions.RegexOptions.Singleline))
            {
                notes.Add(new TriliumNote
                {
                    NoteId = m.Groups[1].Value,
                    Title = JsonUtil.Unescape(m.Groups[2].Value)
                });
            }
            return notes;
        }

        public async Task<string> CreateNoteAsync(string parentNoteId, string title)
        {
            var payload = "{\"parentNoteId\":\"" + JsonUtil.Escape(parentNoteId) + "\"," +
                          "\"title\":\"" + JsonUtil.Escape(title) + "\"," +
                          "\"type\":\"text\",\"content\":\"\"}";
            var body = await PostJsonAsync("/etapi/create-note", payload).ConfigureAwait(false);
            var noteId = JsonUtil.ExtractString(body, "noteId");
            if (string.IsNullOrEmpty(noteId))
            {
                throw new InvalidOperationException($"create-note 回應解析不到 noteId：{Truncate(body)}");
            }
            return noteId;
        }

        public async Task<string> GetNoteContentAsync(string noteId)
        {
            var resp = await http.GetAsync($"{baseUrl}/etapi/notes/{noteId}/content").ConfigureAwait(false);
            await EnsureSuccess(resp).ConfigureAwait(false);
            return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        public async Task SetNoteContentAsync(string noteId, string html)
        {
            var content = new StringContent(html, Encoding.UTF8, "text/plain");
            var resp = await http.PutAsync($"{baseUrl}/etapi/notes/{noteId}/content", content).ConfigureAwait(false);
            await EnsureSuccess(resp).ConfigureAwait(false);
        }

        public async Task<string> CreateAttachmentAsync(string ownerNoteId, string title, string mime, byte[] data)
        {
            var payload = "{\"ownerId\":\"" + JsonUtil.Escape(ownerNoteId) + "\"," +
                          "\"role\":\"image\"," +
                          "\"mime\":\"" + JsonUtil.Escape(mime) + "\"," +
                          "\"title\":\"" + JsonUtil.Escape(title) + "\"," +
                          "\"content\":\"\"}";
            var body = await PostJsonAsync("/etapi/attachments", payload).ConfigureAwait(false);
            var attachmentId = JsonUtil.ExtractString(body, "attachmentId");
            if (string.IsNullOrEmpty(attachmentId))
            {
                throw new InvalidOperationException($"attachments 回應解析不到 attachmentId：{Truncate(body)}");
            }

            var bin = new ByteArrayContent(data);
            bin.Headers.TryAddWithoutValidation("Content-Type", "application/octet-stream");
            var putResp = await http.PutAsync($"{baseUrl}/etapi/attachments/{attachmentId}/content", bin).ConfigureAwait(false);
            await EnsureSuccess(putResp).ConfigureAwait(false);
            return attachmentId;
        }

        private async Task<string> PostJsonAsync(string path, string payload)
        {
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var resp = await http.PostAsync(baseUrl + path, content).ConfigureAwait(false);
            await EnsureSuccess(resp).ConfigureAwait(false);
            return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private static async Task EnsureSuccess(HttpResponseMessage resp)
        {
            if (resp.IsSuccessStatusCode)
            {
                return;
            }
            var body = resp.Content == null
                ? string.Empty
                : await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new HttpRequestException($"ETAPI {(int)resp.StatusCode}：{Truncate(body)}");
        }

        private static string Truncate(string s)
        {
            return string.IsNullOrEmpty(s) || s.Length <= 200 ? s : s.Substring(0, 200);
        }

        public void Dispose()
        {
            http.Dispose();
        }
    }

    internal sealed class TriliumNote
    {
        public string NoteId { get; set; }
        public string Title { get; set; }
    }
}
