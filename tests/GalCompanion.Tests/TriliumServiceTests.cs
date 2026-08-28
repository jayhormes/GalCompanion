using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace GalCompanion.Tests
{
    public class TriliumServiceTests
    {
        private static readonly DateTime Ts = new DateTime(2026, 8, 28, 21, 30, 0);
        private const string Base = "http://nas:8080";

        private static TriliumService Service(FakeHttpHandler handler)
        {
            return new TriliumService(
                new TriliumClient(Base, "t", handler), "yyyy.MM.dd", "遊戲心得", "翻譯問題");
        }

        // --- 日付ノートの選別（Trilium の全文検索は曖昧一致） ---

        [Fact]
        public void PickDateNote_ignores_fuzzy_hits_and_takes_the_real_one()
        {
            var hits = new List<TriliumNote>
            {
                new TriliumNote { NoteId = "x1", Title = "28 - 週五" },
                new TriliumNote { NoteId = "x2", Title = "2016.08.22 星期一" },
                new TriliumNote { NoteId = "x3", Title = "2026.08.28 星期五 (Week35) - 晨間日記" },
                new TriliumNote { NoteId = "x4", Title = "2026.08.28 另一則" },
            };

            Assert.Equal("x3", TriliumService.PickDateNote(hits, "2026.08.28"));
        }

        [Fact]
        public void PickDateNote_returns_null_when_no_title_starts_with_the_date()
        {
            var hits = new List<TriliumNote>
            {
                new TriliumNote { NoteId = "x1", Title = "2015.08.28 星期五" },
                new TriliumNote { NoteId = "x2", Title = "關於 2026.08.28 的計畫" },
            };

            Assert.Null(TriliumService.PickDateNote(hits, "2026.08.28"));
        }

        [Fact]
        public void PickDateNote_tolerates_leading_whitespace()
        {
            var hits = new List<TriliumNote>
            {
                new TriliumNote { NoteId = "x1", Title = "  2026.08.28 星期五 - 晨間日記" }
            };

            Assert.Equal("x1", TriliumService.PickDateNote(hits, "2026.08.28"));
        }

        [Fact]
        public void PickDateNote_handles_null_input()
        {
            Assert.Null(TriliumService.PickDateNote(null, "2026.08.28"));
            Assert.Null(TriliumService.PickDateNote(new List<TriliumNote>(), "2026.08.28"));
        }

        [Fact]
        public async Task EnsureDateNote_uses_existing_diary_without_creating()
        {
            var handler = new FakeHttpHandler();
            handler.Enqueue("{\"results\":[{\"noteId\":\"diary1\",\"isProtected\":false,"
                + "\"title\":\"2026.08.28 星期五 (Week35) - 晨間日記\",\"type\":\"text\"}]}");

            using (var service = Service(handler))
            {
                var noteId = await service.EnsureDateNoteAsync(Ts, "fallbackParent");

                Assert.Equal("diary1", noteId);
                Assert.Single(handler.Requests);
                Assert.Contains("/etapi/notes?search=", handler.Requests[0].Url);
            }
        }

        [Fact]
        public async Task EnsureDateNote_creates_under_fallback_when_diary_missing()
        {
            var handler = new FakeHttpHandler();
            // 曖昧一致のみ＝当日の日記は無い
            handler.Enqueue("{\"results\":[{\"noteId\":\"old\",\"title\":\"2015.08.28 星期五\"}]}");
            handler.Enqueue("{\"note\":{\"noteId\":\"new1\"}}");

            using (var service = Service(handler))
            {
                var noteId = await service.EnsureDateNoteAsync(Ts, "fallbackParent");

                Assert.Equal("new1", noteId);
                Assert.Equal(2, handler.Requests.Count);
                Assert.Contains("\"parentNoteId\":\"fallbackParent\"", handler.Requests[1].Body);
                Assert.Contains("\"title\":\"2026.08.28\"", handler.Requests[1].Body);
            }
        }

        [Fact]
        public async Task EnsureDateNote_throws_when_missing_and_no_fallback()
        {
            var handler = new FakeHttpHandler();
            handler.Enqueue("{\"results\":[]}");

            using (var service = Service(handler))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.EnsureDateNoteAsync(Ts, ""));
            }
        }

        // --- 子ノートの解決 ---

        [Fact]
        public async Task EnsureChildNote_reuses_existing_child_with_same_title()
        {
            var handler = new FakeHttpHandler();
            handler.Enqueue("{\"noteId\":\"parent\",\"childNoteIds\":[\"c1\",\"c2\"]}");
            handler.Enqueue("{\"noteId\":\"c1\",\"title\":\"別の話題\"}");
            handler.Enqueue("{\"noteId\":\"c2\",\"title\":\"遊戲心得\"}");

            using (var service = Service(handler))
            {
                Assert.Equal("c2", await service.EnsureChildNoteAsync("parent", "遊戲心得"));
                Assert.Equal(3, handler.Requests.Count);
            }
        }

        [Fact]
        public async Task EnsureChildNote_creates_when_absent()
        {
            var handler = new FakeHttpHandler();
            handler.Enqueue("{\"noteId\":\"parent\",\"childNoteIds\":[]}");
            handler.Enqueue("{\"note\":{\"noteId\":\"created\"}}");

            using (var service = Service(handler))
            {
                Assert.Equal("created", await service.EnsureChildNoteAsync("parent", "遊戲心得"));
                Assert.Contains("\"title\":\"遊戲心得\"", handler.Requests[1].Body);
            }
        }

        // --- 目標ノートの解決 ---

        [Fact]
        public async Task ResolveTarget_impressions_stops_at_the_day_note_child()
        {
            var handler = new FakeHttpHandler();
            handler.Enqueue("{\"results\":[{\"noteId\":\"diary1\",\"title\":\"2026.08.28 星期五 - 晨間日記\"}]}");
            handler.Enqueue("{\"noteId\":\"diary1\",\"childNoteIds\":[\"imp\"]}");
            handler.Enqueue("{\"noteId\":\"imp\",\"title\":\"遊戲心得\"}");

            using (var service = Service(handler))
            {
                var noteId = await service.ResolveTargetNoteAsync(Ts, "p", TriliumTarget.Impressions);
                Assert.Equal("imp", noteId);
            }
        }

        [Fact]
        public async Task ResolveTarget_translation_goes_one_level_deeper()
        {
            var handler = new FakeHttpHandler();
            handler.Enqueue("{\"results\":[{\"noteId\":\"diary1\",\"title\":\"2026.08.28 星期五 - 晨間日記\"}]}");
            handler.Enqueue("{\"noteId\":\"diary1\",\"childNoteIds\":[\"imp\"]}");
            handler.Enqueue("{\"noteId\":\"imp\",\"title\":\"遊戲心得\"}");
            handler.Enqueue("{\"noteId\":\"imp\",\"childNoteIds\":[\"tr\"]}");
            handler.Enqueue("{\"noteId\":\"tr\",\"title\":\"翻譯問題\"}");

            using (var service = Service(handler))
            {
                var noteId = await service.ResolveTargetNoteAsync(Ts, "p", TriliumTarget.Translation);
                Assert.Equal("tr", noteId);
            }
        }

        [Fact]
        public async Task ResolveTarget_creates_the_whole_chain_on_a_fresh_day()
        {
            var handler = new FakeHttpHandler();
            handler.Enqueue("{\"results\":[]}");                       // 日記なし
            handler.Enqueue("{\"note\":{\"noteId\":\"date1\"}}");      // 日付ノート作成
            handler.Enqueue("{\"noteId\":\"date1\",\"childNoteIds\":[]}");
            handler.Enqueue("{\"note\":{\"noteId\":\"imp1\"}}");       // 心得作成
            handler.Enqueue("{\"noteId\":\"imp1\",\"childNoteIds\":[]}");
            handler.Enqueue("{\"note\":{\"noteId\":\"tr1\"}}");        // 翻譯問題作成

            using (var service = Service(handler))
            {
                var noteId = await service.ResolveTargetNoteAsync(Ts, "parent", TriliumTarget.Translation);
                Assert.Equal("tr1", noteId);
                Assert.Contains("\"parentNoteId\":\"date1\"", handler.Requests[3].Body);
                Assert.Contains("\"parentNoteId\":\"imp1\"", handler.Requests[5].Body);
            }
        }

        // --- 追記 ---

        [Fact]
        public async Task AppendEntry_with_image_uploads_then_appends()
        {
            var handler = new FakeHttpHandler();
            handler.Enqueue("{\"attachmentId\":\"att1\"}"); // POST /etapi/attachments
            handler.Enqueue("");                            // PUT attachment content
            handler.Enqueue("<p>OLD</p>");                  // GET note content
            handler.Enqueue("");                            // PUT note content

            using (var service = Service(handler))
            {
                await service.AppendEntryAsync("note1", Ts, "モザイクの天使", new byte[] { 1 }, "文字");

                Assert.Equal(4, handler.Requests.Count);
                var final = handler.Requests[3];
                Assert.Equal(Base + "/etapi/notes/note1/content", final.Url);
                Assert.StartsWith("<p>OLD</p>", final.Body);
                Assert.Contains("api/attachments/att1/image/", final.Body);
                Assert.Contains("2026-08-28 21:30:00", final.Body);
                Assert.Contains("モザイクの天使", final.Body);
                Assert.Contains("<p>文字</p>", final.Body);
            }
        }

        [Fact]
        public async Task AppendEntry_text_only_skips_attachment()
        {
            var handler = new FakeHttpHandler();
            handler.Enqueue("<p>OLD</p>");
            handler.Enqueue("");

            using (var service = Service(handler))
            {
                await service.AppendEntryAsync("note1", Ts, "ゲーム", null, "只有文字");

                Assert.Equal(2, handler.Requests.Count);
                Assert.DoesNotContain("attachments", handler.Requests[0].Url);
                Assert.Contains("只有文字", handler.Requests[1].Body);
                Assert.DoesNotContain("<figure", handler.Requests[1].Body);
            }
        }

        [Fact]
        public void FormatDate_uses_the_configured_pattern()
        {
            using (var service = Service(new FakeHttpHandler()))
            {
                Assert.Equal("2026.08.28", service.FormatDate(Ts));
            }
        }

        // --- 検索結果のパース ---

        [Fact]
        public void ParseNoteList_reads_id_and_title_pairs()
        {
            var json = "{\"results\":["
                + "{\"noteId\":\"a1\",\"isProtected\":false,\"title\":\"2026.08.28 星期五\",\"type\":\"text\"},"
                + "{\"noteId\":\"b2\",\"isProtected\":false,\"title\":\"引號\\\"測試\\\"\",\"type\":\"text\"}]}";

            var notes = TriliumClient.ParseNoteList(json);

            Assert.Equal(2, notes.Count);
            Assert.Equal("a1", notes[0].NoteId);
            Assert.Equal("2026.08.28 星期五", notes[0].Title);
            Assert.Equal("引號\"測試\"", notes[1].Title);
        }

        [Fact]
        public void ParseNoteList_returns_empty_for_no_results()
        {
            Assert.Empty(TriliumClient.ParseNoteList("{\"results\":[]}"));
            Assert.Empty(TriliumClient.ParseNoteList(null));
        }
    }
}
