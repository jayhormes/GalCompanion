using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace GalCompanion.Tests
{
    public class TriliumClientTests
    {
        private const string BaseUrl = "http://nas:8080";
        private const string Token = "test-token";

        [Fact]
        public async Task CreateNote_posts_payload_and_parses_noteId()
        {
            var handler = new FakeHttpHandler();
            handler.Enqueue("{\"note\":{\"noteId\":\"abc123\",\"title\":\"t\"},\"branch\":{}}");
            using (var client = new TriliumClient(BaseUrl + "/", Token, handler))
            {
                var noteId = await client.CreateNoteAsync("root1", "遊戲 \"標題\"");

                Assert.Equal("abc123", noteId);
                var req = Assert.Single(handler.Requests);
                Assert.Equal("POST", req.Method);
                Assert.Equal("http://nas:8080/etapi/create-note", req.Url);
                Assert.Equal(Token, req.Authorization);
                Assert.Contains("\"parentNoteId\":\"root1\"", req.Body);
                Assert.Contains("\\\"標題\\\"", req.Body);
                Assert.Contains("\"type\":\"text\"", req.Body);
            }
        }

        [Fact]
        public async Task CreateNote_throws_when_noteId_missing()
        {
            var handler = new FakeHttpHandler();
            handler.Enqueue("{\"status\":\"weird\"}");
            using (var client = new TriliumClient(BaseUrl, Token, handler))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => client.CreateNoteAsync("root1", "x"));
            }
        }

        [Fact]
        public async Task CreateAttachment_creates_then_uploads_binary()
        {
            var handler = new FakeHttpHandler();
            handler.Enqueue("{\"attachmentId\":\"att9\",\"role\":\"image\"}");
            handler.Enqueue("");
            using (var client = new TriliumClient(BaseUrl, Token, handler))
            {
                var id = await client.CreateAttachmentAsync("note1", "shot.png", "image/png", new byte[] { 1, 2, 3 });

                Assert.Equal("att9", id);
                Assert.Equal(2, handler.Requests.Count);
                Assert.Equal("POST", handler.Requests[0].Method);
                Assert.Equal("http://nas:8080/etapi/attachments", handler.Requests[0].Url);
                Assert.Contains("\"ownerId\":\"note1\"", handler.Requests[0].Body);
                Assert.Contains("\"mime\":\"image/png\"", handler.Requests[0].Body);
                Assert.Equal("PUT", handler.Requests[1].Method);
                Assert.Equal("http://nas:8080/etapi/attachments/att9/content", handler.Requests[1].Url);
            }
        }

        [Fact]
        public async Task GetNoteContent_uses_content_endpoint()
        {
            var handler = new FakeHttpHandler();
            handler.Enqueue("<p>hi</p>");
            using (var client = new TriliumClient(BaseUrl, Token, handler))
            {
                var content = await client.GetNoteContentAsync("n1");

                Assert.Equal("<p>hi</p>", content);
                Assert.Equal("http://nas:8080/etapi/notes/n1/content", handler.Requests[0].Url);
                Assert.Equal("GET", handler.Requests[0].Method);
            }
        }

        [Fact]
        public async Task SetNoteContent_puts_body()
        {
            var handler = new FakeHttpHandler();
            handler.Enqueue("");
            using (var client = new TriliumClient(BaseUrl, Token, handler))
            {
                await client.SetNoteContentAsync("n1", "<p>new</p>");

                var req = Assert.Single(handler.Requests);
                Assert.Equal("PUT", req.Method);
                Assert.Equal("http://nas:8080/etapi/notes/n1/content", req.Url);
                Assert.Equal("<p>new</p>", req.Body);
            }
        }

        [Fact]
        public async Task Error_status_throws_with_status_code()
        {
            var handler = new FakeHttpHandler();
            handler.Enqueue("{\"message\":\"bad token\"}", HttpStatusCode.Unauthorized);
            using (var client = new TriliumClient(BaseUrl, Token, handler))
            {
                var ex = await Assert.ThrowsAsync<HttpRequestException>(
                    () => client.GetNoteContentAsync("n1"));
                Assert.Contains("401", ex.Message);
            }
        }
    }
}
