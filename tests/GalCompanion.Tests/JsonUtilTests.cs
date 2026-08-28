using Xunit;

namespace GalCompanion.Tests
{
    public class JsonUtilTests
    {
        [Theory]
        [InlineData("plain", "plain")]
        [InlineData("a\"b", "a\\\"b")]
        [InlineData("a\\b", "a\\\\b")]
        [InlineData("a\nb", "a\\nb")]
        [InlineData("a\tb", "a\\tb")]
        [InlineData("中文標題", "中文標題")]
        [InlineData(null, "")]
        public void Escape_handles_special_characters(string input, string expected)
        {
            Assert.Equal(expected, JsonUtil.Escape(input));
        }

        [Fact]
        public void Escape_control_char_uses_unicode_form()
        {
            Assert.Equal("a\\u0001b", JsonUtil.Escape("a\u0001b"));
        }

        [Fact]
        public void ExtractString_finds_nested_field()
        {
            var json = "{\"note\":{\"noteId\":\"abc123\",\"title\":\"x\"}}";
            Assert.Equal("abc123", JsonUtil.ExtractString(json, "noteId"));
        }

        // Escape 產出的值必須能被 ExtractString 還原（manifest 的 device 名等）
        [Theory]
        [InlineData("a\"b")]
        [InlineData("a\\b")]
        [InlineData("兩行\n文字")]
        [InlineData("tab\there")]
        public void ExtractString_roundtrips_escaped_values(string value)
        {
            var json = "{\"device\":\"" + JsonUtil.Escape(value) + "\"}";
            Assert.Equal(value, JsonUtil.ExtractString(json, "device"));
        }

        [Fact]
        public void ExtractString_unescapes_unicode_form()
        {
            Assert.Equal("a\u0001b", JsonUtil.ExtractString("{\"x\":\"a\\u0001b\"}", "x"));
        }

        [Theory]
        [InlineData("{}")]
        [InlineData(null)]
        [InlineData("{\"noteId\":123}")]
        public void ExtractString_returns_null_when_missing(string json)
        {
            Assert.Null(JsonUtil.ExtractString(json, "noteId"));
        }
    }
}
