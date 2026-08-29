using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace LunaImport
{
    internal static class Json
    {
        /// <summary>
        /// 日付を DateTime に変換させずに読む。既定のままだと ISO 文字列が Date トークンになり、
        /// 書き戻すときに Newtonsoft 自前の書式・タイムゾーンに変わってしまう。
        /// 触っていない項目は原文のまま残さないといけないので、ここは必ずこの入口を通す。
        /// </summary>
        public static JObject Parse(string text)
        {
            using (var reader = new JsonTextReader(new StringReader(text)))
            {
                reader.DateParseHandling = DateParseHandling.None;
                return JObject.Load(reader);
            }
        }
    }
}
