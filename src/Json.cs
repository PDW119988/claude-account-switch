using System;
using System.Text;

namespace ClaudeAccountSwitch
{
    // Minimal JSON helper that edits a single top-level member in place.
    // The rest of the file is left byte-for-byte untouched, so Claude Code's
    // own settings are never re-formatted or lost.
    static class Json
    {
        static int SkipWs(string s, int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            return i;
        }

        static int SkipString(string s, int i)
        {
            i++;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\\') i += 2;
                else if (c == '"') return i + 1;
                else i++;
            }
            throw new FormatException("Unterminated JSON string");
        }

        static int SkipValue(string s, int i)
        {
            i = SkipWs(s, i);
            if (i >= s.Length) throw new FormatException("Unexpected end of JSON");
            char c = s[i];
            if (c == '"') return SkipString(s, i);
            if (c == '{' || c == '[')
            {
                int depth = 0;
                while (i < s.Length)
                {
                    char ch = s[i];
                    if (ch == '"') { i = SkipString(s, i); continue; }
                    if (ch == '{' || ch == '[') depth++;
                    else if (ch == '}' || ch == ']')
                    {
                        depth--;
                        if (depth == 0) return i + 1;
                    }
                    i++;
                }
                throw new FormatException("Unterminated JSON object");
            }
            while (i < s.Length && ",}] \t\r\n".IndexOf(s[i]) < 0) i++;
            return i;
        }

        static bool Find(string s, string key, out int start, out int end)
        {
            start = end = -1;
            int i = SkipWs(s, 0);
            if (i >= s.Length || s[i] != '{') throw new FormatException("JSON root is not an object");
            i++;
            while (true)
            {
                i = SkipWs(s, i);
                if (i >= s.Length) throw new FormatException("Unexpected end of JSON");
                if (s[i] == '}') return false;
                int ks = i;
                int ke = SkipString(s, i);
                string k = Unescape(s.Substring(ks + 1, ke - ks - 2));
                i = SkipWs(s, ke);
                if (s[i] != ':') throw new FormatException("Expected ':' in JSON");
                i = SkipWs(s, i + 1);
                int vs = i;
                int ve = SkipValue(s, i);
                if (k == key) { start = vs; end = ve; return true; }
                i = SkipWs(s, ve);
                if (i < s.Length && s[i] == ',') i++;
            }
        }

        // Raw JSON text of a top-level member, or null.
        public static string Get(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int a, b;
            if (!Find(json, key, out a, out b)) return null;
            string raw = json.Substring(a, b - a);
            return raw == "null" ? null : raw;
        }

        public static string GetString(string json, string key)
        {
            string raw = Get(json, key);
            if (raw == null || raw.Length < 2 || raw[0] != '"') return null;
            return Unescape(raw.Substring(1, raw.Length - 2));
        }

        // Replace (or add) a top-level member with the given raw JSON value.
        public static string Set(string json, string key, string rawValue)
        {
            if (string.IsNullOrWhiteSpace(json)) json = "{}";
            int a, b;
            if (Find(json, key, out a, out b))
                return json.Substring(0, a) + rawValue + json.Substring(b);

            int close = json.LastIndexOf('}');
            int open = SkipWs(json, 0);
            bool empty = SkipWs(json, open + 1) == close;
            string member = Quote(key) + ": " + rawValue;
            string before = json.Substring(0, close).TrimEnd();
            return before + (empty ? "\n  " : ",\n  ") + member + "\n" + json.Substring(close);
        }

        public static string Quote(string v)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in v)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '\\' || i + 1 >= s.Length) { sb.Append(c); continue; }
                char n = s[++i];
                switch (n)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16));
                        i += 4;
                        break;
                    default: sb.Append(n); break;
                }
            }
            return sb.ToString();
        }
    }
}
