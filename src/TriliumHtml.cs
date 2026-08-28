using System;
using System.Text;

namespace GalCompanion
{
    internal static class TriliumHtml
    {
        // 心得ノートは 1 日 1 枚で複数タイトルが混ざるので、見出しにゲーム名を入れる
        public static string BuildEntry(
            DateTime timestamp, string gameName, string attachmentId, string imageTitle, string text)
        {
            var sb = new StringBuilder();
            sb.Append("<p><strong>")
              .Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            if (!string.IsNullOrWhiteSpace(gameName))
            {
                sb.Append(" · ").Append(EscapeHtml(gameName.Trim()));
            }
            sb.Append("</strong></p>");

            if (!string.IsNullOrEmpty(attachmentId))
            {
                sb.Append("<figure class=\"image\"><img src=\"api/attachments/")
                  .Append(attachmentId)
                  .Append("/image/")
                  .Append(Uri.EscapeDataString(imageTitle ?? "screenshot.png"))
                  .Append("\"></figure>");
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.Append("<p>")
                  .Append(EscapeHtml(text).Replace("\r", string.Empty).Replace("\n", "<br>"))
                  .Append("</p>");
            }
            return sb.ToString();
        }

        public static string EscapeHtml(string s)
        {
            return (s ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}
