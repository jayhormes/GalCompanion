using System;
using System.Threading.Tasks;
using Xunit;

namespace GalCompanion.Tests
{
    public class TriliumServiceTests
    {
        private static readonly DateTime Ts = new DateTime(2026, 8, 28, 21, 30, 0);

        [Fact]
        public async Task AppendEntry_with_image_uploads_then_appends()
        {
            var handler = new FakeHttpHandler();
            handler.Enqueue("{\"attachmentId\":\"att1\"}"); // POST /etapi/attachments
            handler.Enqueue("");                            // PUT attachment content
            handler.Enqueue("<p>OLD</p>");                  // GET note content
            handler.Enqueue("");                            // PUT note content
            using (var service = new TriliumService(new TriliumClient("http://nas:8080", "t", handler)))
            {
                await service.AppendEntryAsync("note1", Ts, new byte[] { 1 }, "文字");

                Assert.Equal(4, handler.Requests.Count);
                var final = handler.Requests[3];
                Assert.Equal("http://nas:8080/etapi/notes/note1/content", final.Url);
                Assert.StartsWith("<p>OLD</p>", final.Body);
                Assert.Contains("api/attachments/att1/image/", final.Body);
                Assert.Contains("2026-08-28 21:30:00", final.Body);
                Assert.Contains("<p>文字</p>", final.Body);
            }
        }

        [Fact]
        public async Task AppendEntry_text_only_skips_attachment()
        {
            var handler = new FakeHttpHandler();
            handler.Enqueue("<p>OLD</p>"); // GET note content
            handler.Enqueue("");           // PUT note content
            using (var service = new TriliumService(new TriliumClient("http://nas:8080", "t", handler)))
            {
                await service.AppendEntryAsync("note1", Ts, null, "只有文字");

                Assert.Equal(2, handler.Requests.Count);
                Assert.DoesNotContain("attachments", handler.Requests[0].Url);
                Assert.Contains("只有文字", handler.Requests[1].Body);
                Assert.DoesNotContain("<figure", handler.Requests[1].Body);
            }
        }

        [Fact]
        public async Task EnsureGameNote_returns_existing_binding_without_calls()
        {
            var handler = new FakeHttpHandler();
            using (var service = new TriliumService(new TriliumClient("http://nas:8080", "t", handler)))
            {
                var noteId = await service.EnsureGameNoteAsync("parent", "Game", "bound1");

                Assert.Equal("bound1", noteId);
                Assert.Empty(handler.Requests);
            }
        }

        [Fact]
        public async Task EnsureGameNote_creates_when_unbound()
        {
            var handler = new FakeHttpHandler();
            handler.Enqueue("{\"note\":{\"noteId\":\"new1\"}}");
            using (var service = new TriliumService(new TriliumClient("http://nas:8080", "t", handler)))
            {
                var noteId = await service.EnsureGameNoteAsync("parent", "Game", null);

                Assert.Equal("new1", noteId);
                Assert.Contains("\"parentNoteId\":\"parent\"", handler.Requests[0].Body);
            }
        }

        [Fact]
        public async Task EnsureGameNote_throws_without_parent()
        {
            var handler = new FakeHttpHandler();
            using (var service = new TriliumService(new TriliumClient("http://nas:8080", "t", handler)))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.EnsureGameNoteAsync("", "Game", null));
            }
        }
    }
}
