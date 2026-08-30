using Xunit;

namespace GalCompanion.Tests
{
    public class JsonParserTests
    {
        [Fact]
        public void Reads_a_nested_object()
        {
            var root = JsonParser.Parse("{\"a\":{\"b\":\"c\"}}");

            Assert.Equal(JsonKind.Object, root.Kind);
            Assert.Equal("c", root["a"]["b"].AsString());
        }

        [Fact]
        public void Reads_a_mixed_array()
        {
            var root = JsonParser.Parse("[[\"uid\"],{\"uid\":{\"title\":\"名\"}},1]");

            Assert.Equal(3, root.Count);
            Assert.Equal("uid", root[0][0].AsString());
            Assert.Equal("名", root[1]["uid"]["title"].AsString());
            Assert.Equal(1, root[2].Number);
        }

        [Fact]
        public void Missing_members_read_as_null_instead_of_throwing()
        {
            var root = JsonParser.Parse("{\"a\":1}");

            Assert.Null(root["b"]);
            Assert.Null(root["b"].AsStringOrNull());
            Assert.Null(root["a"].AsString());   // 数値は文字列として返さない
            Assert.Null(root[5]);
        }

        [Fact]
        public void Unescapes_the_usual_sequences()
        {
            var root = JsonParser.Parse("{\"p\":\"D:\\\\a\\\\b.exe\",\"n\":\"1\\n2\",\"u\":\"\\u3042\"}");

            Assert.Equal("D:\\a\\b.exe", root["p"].AsString());
            Assert.Equal("1\n2", root["n"].AsString());
            Assert.Equal("あ", root["u"].AsString());
        }

        [Fact]
        public void Keeps_keys_in_file_order()
        {
            var root = JsonParser.Parse("{\"z\":1,\"a\":2}");

            Assert.Equal("z", root.Members[0].Key);
            Assert.Equal("a", root.Members[1].Key);
        }

        [Fact]
        public void Reads_literals_and_numbers()
        {
            var root = JsonParser.Parse("{\"t\":true,\"f\":false,\"n\":null,\"d\":-1.5e2}");

            Assert.True(root["t"].Bool);
            Assert.False(root["f"].Bool);
            Assert.Equal(JsonKind.Null, root["n"].Kind);
            Assert.Equal(-150.0, root["d"].Number, 3);
        }

        [Fact]
        public void Empty_containers_are_fine()
        {
            Assert.Equal(0, JsonParser.Parse("{}").Count);
            Assert.Equal(0, JsonParser.Parse("[]").Count);
            Assert.Equal(0, JsonParser.Parse(" [ ] ").Count);
        }

        [Fact]
        public void A_byte_order_mark_does_not_break_it()
        {
            Assert.Equal(1, JsonParser.Parse("\uFEFF{\"a\":1}").Count);
        }

        [Theory]
        [InlineData("{\"a\":1")]
        [InlineData("[1,")]
        [InlineData("{a:1}")]
        [InlineData("\"unterminated")]
        [InlineData("{} trailing")]
        [InlineData("")]
        [InlineData("nul")]
        public void Broken_input_returns_null_from_TryParse(string text)
        {
            Assert.Null(JsonParser.TryParse(text));
        }
    }
}
