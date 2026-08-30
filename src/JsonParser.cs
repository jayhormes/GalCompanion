using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GalCompanion
{
    internal enum JsonKind
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object,
    }

    /// <summary>
    /// 最小限の JSON 木。JsonUtil の正規表現では入れ子を辿れないので、
    /// LunaTranslator の設定のような素性の分からない構造だけこちらで読む。
    /// Newtonsoft を参照しないのは、Playnite 本体が積んでいる版に縛られないため。
    /// </summary>
    internal sealed class JsonValue
    {
        public JsonKind Kind;
        public string Text;
        public double Number;
        public bool Bool;
        public List<JsonValue> Items;
        public List<KeyValuePair<string, JsonValue>> Members;

        public int Count
        {
            get
            {
                if (Items != null) { return Items.Count; }
                if (Members != null) { return Members.Count; }
                return 0;
            }
        }

        public JsonValue this[int index]
        {
            get
            {
                return Items != null && index >= 0 && index < Items.Count ? Items[index] : null;
            }
        }

        public JsonValue this[string name]
        {
            get
            {
                if (Members == null) { return null; }
                foreach (var member in Members)
                {
                    if (string.Equals(member.Key, name, StringComparison.Ordinal))
                    {
                        return member.Value;
                    }
                }
                return null;
            }
        }

        /// <summary>文字列以外や欠けている項目は null。呼び出し側で分岐したくないため。</summary>
        public string AsString()
        {
            return Kind == JsonKind.String ? Text : null;
        }
    }

    internal static class JsonValueExtensions
    {
        /// <summary>欠けている項目にも呼べるように拡張メソッドにしてある。</summary>
        public static string AsStringOrNull(this JsonValue value)
        {
            return value == null ? null : value.AsString();
        }
    }

    internal static class JsonParser
    {
        public static JsonValue Parse(string text)
        {
            if (text == null)
            {
                throw new FormatException("JSON が空です");
            }
            var pos = 0;
            var value = ParseValue(text, ref pos);
            SkipWhitespace(text, ref pos);
            if (pos != text.Length)
            {
                throw new FormatException($"{pos} 文字目に余分な文字があります");
            }
            return value;
        }

        /// <summary>壊れていたら null。設定ファイルは壊れていても落ちないほうがいい。</summary>
        public static JsonValue TryParse(string text)
        {
            try
            {
                return Parse(text);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static JsonValue ParseValue(string text, ref int pos)
        {
            SkipWhitespace(text, ref pos);
            if (pos >= text.Length)
            {
                throw new FormatException("値が来る前に終わっています");
            }

            switch (text[pos])
            {
                case '{': return ParseObject(text, ref pos);
                case '[': return ParseArray(text, ref pos);
                case '"': return new JsonValue { Kind = JsonKind.String, Text = ParseString(text, ref pos) };
                case 't': Expect(text, ref pos, "true"); return new JsonValue { Kind = JsonKind.Bool, Bool = true };
                case 'f': Expect(text, ref pos, "false"); return new JsonValue { Kind = JsonKind.Bool, Bool = false };
                case 'n': Expect(text, ref pos, "null"); return new JsonValue { Kind = JsonKind.Null };
                default: return ParseNumber(text, ref pos);
            }
        }

        private static JsonValue ParseObject(string text, ref int pos)
        {
            pos++; // {
            var value = new JsonValue
            {
                Kind = JsonKind.Object,
                Members = new List<KeyValuePair<string, JsonValue>>(),
            };
            SkipWhitespace(text, ref pos);
            if (Peek(text, pos) == '}')
            {
                pos++;
                return value;
            }

            while (true)
            {
                SkipWhitespace(text, ref pos);
                if (Peek(text, pos) != '"')
                {
                    throw new FormatException($"{pos} 文字目：キーが文字列ではありません");
                }
                var name = ParseString(text, ref pos);
                SkipWhitespace(text, ref pos);
                if (Peek(text, pos) != ':')
                {
                    throw new FormatException($"{pos} 文字目：コロンがありません");
                }
                pos++;
                value.Members.Add(new KeyValuePair<string, JsonValue>(name, ParseValue(text, ref pos)));

                SkipWhitespace(text, ref pos);
                var next = Peek(text, pos);
                if (next == ',')
                {
                    pos++;
                    continue;
                }
                if (next == '}')
                {
                    pos++;
                    return value;
                }
                throw new FormatException($"{pos} 文字目：オブジェクトが閉じていません");
            }
        }

        private static JsonValue ParseArray(string text, ref int pos)
        {
            pos++; // [
            var value = new JsonValue { Kind = JsonKind.Array, Items = new List<JsonValue>() };
            SkipWhitespace(text, ref pos);
            if (Peek(text, pos) == ']')
            {
                pos++;
                return value;
            }

            while (true)
            {
                value.Items.Add(ParseValue(text, ref pos));
                SkipWhitespace(text, ref pos);
                var next = Peek(text, pos);
                if (next == ',')
                {
                    pos++;
                    continue;
                }
                if (next == ']')
                {
                    pos++;
                    return value;
                }
                throw new FormatException($"{pos} 文字目：配列が閉じていません");
            }
        }

        private static string ParseString(string text, ref int pos)
        {
            pos++; // "
            var builder = new StringBuilder();
            while (true)
            {
                if (pos >= text.Length)
                {
                    throw new FormatException("文字列が閉じていません");
                }
                var c = text[pos++];
                if (c == '"')
                {
                    return builder.ToString();
                }
                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }

                if (pos >= text.Length)
                {
                    throw new FormatException("エスケープが途中で終わっています");
                }
                var escaped = text[pos++];
                switch (escaped)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (pos + 4 > text.Length)
                        {
                            throw new FormatException("\\u が短すぎます");
                        }
                        int code;
                        if (!int.TryParse(text.Substring(pos, 4), NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture, out code))
                        {
                            throw new FormatException($"{pos} 文字目：\\u の中身が 16 進ではありません");
                        }
                        builder.Append((char)code);
                        pos += 4;
                        break;
                    default:
                        throw new FormatException($"{pos} 文字目：知らないエスケープ \\{escaped}");
                }
            }
        }

        private static JsonValue ParseNumber(string text, ref int pos)
        {
            var start = pos;
            if (Peek(text, pos) == '-' || Peek(text, pos) == '+')
            {
                pos++;
            }
            while (pos < text.Length && (char.IsDigit(text[pos]) || text[pos] == '.'
                    || text[pos] == 'e' || text[pos] == 'E'
                    || ((text[pos] == '-' || text[pos] == '+') && (text[pos - 1] == 'e' || text[pos - 1] == 'E'))))
            {
                pos++;
            }

            double parsed;
            var slice = text.Substring(start, pos - start);
            if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                throw new FormatException($"{start} 文字目：数値として読めません（{slice}）");
            }
            return new JsonValue { Kind = JsonKind.Number, Number = parsed, Text = slice };
        }

        private static void Expect(string text, ref int pos, string word)
        {
            if (pos + word.Length > text.Length
                || string.CompareOrdinal(text, pos, word, 0, word.Length) != 0)
            {
                throw new FormatException($"{pos} 文字目：{word} が来るはずです");
            }
            pos += word.Length;
        }

        private static char Peek(string text, int pos)
        {
            return pos < text.Length ? text[pos] : '\0';
        }

        private static void SkipWhitespace(string text, ref int pos)
        {
            while (pos < text.Length && (text[pos] == ' ' || text[pos] == '\t'
                    || text[pos] == '\r' || text[pos] == '\n' || text[pos] == '\uFEFF'))
            {
                pos++;
            }
        }
    }
}
