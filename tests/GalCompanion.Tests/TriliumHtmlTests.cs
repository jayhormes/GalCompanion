using System;
using Xunit;

namespace GalCompanion.Tests
{
    public class TriliumHtmlTests
    {
        private static readonly DateTime Ts = new DateTime(2026, 8, 28, 21, 30, 5);

        [Fact]
        public void Entry_with_image_and_text()
        {
            var html = TriliumHtml.BuildEntry(Ts, "att1", "shot 1.png", "跑到 <選項> 會斷行");

            Assert.Contains("<strong>2026-08-28 21:30:05</strong>", html);
            Assert.Contains("api/attachments/att1/image/shot%201.png", html);
            Assert.Contains("&lt;選項&gt;", html);
            Assert.DoesNotContain("<選項>", html);
        }

        [Fact]
        public void Entry_without_image_has_no_figure()
        {
            var html = TriliumHtml.BuildEntry(Ts, null, null, "純文字");
            Assert.DoesNotContain("<figure", html);
            Assert.Contains("純文字", html);
        }

        [Fact]
        public void Entry_without_text_has_no_extra_paragraph()
        {
            var html = TriliumHtml.BuildEntry(Ts, "att1", "a.png", null);
            Assert.Contains("<figure", html);
            Assert.EndsWith("</figure>", html);
        }

        [Fact]
        public void Multiline_text_becomes_br()
        {
            var html = TriliumHtml.BuildEntry(Ts, null, null, "第一行\r\n第二行");
            Assert.Contains("第一行<br>第二行", html);
        }
    }
}
