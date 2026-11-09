using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shura
{
    /// <summary>
    /// A small, allocation-light JSON reader for the online protocol (Unity's JsonUtility
    /// cannot read the mixed-type arrays the snapshots use). Objects become
    /// Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;, numbers double, plus string,
    /// bool and null. Helpers read fields with defaults so a missing value never throws.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int i = 0;
            try
            {
                return Value(json, ref i);
            }
            catch
            {
                return null;
            }
        }

        static void Skip(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\n' || s[i] == '\r' || s[i] == '\t')) i++;
        }

        static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return Str(s, ref i);
            if (c == 't') { i += 4; return true; }
            if (c == 'f') { i += 5; return false; }
            if (c == 'n') { i += 4; return null; }
            return Num(s, ref i);
        }

        static Dictionary<string, object> Obj(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;
            Skip(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Skip(s, ref i);
                string key = Str(s, ref i);
                Skip(s, ref i);
                i++; // ':'
                d[key] = Value(s, ref i);
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; // '}'
                return d;
            }
        }

        static List<object> Arr(string s, ref int i)
        {
            var list = new List<object>();
            i++;
            Skip(s, ref i);
            if (s[i] == ']') { i++; return list; }
            while (true)
            {
                list.Add(Value(s, ref i));
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; // ']'
                return list;
            }
        }

        [System.ThreadStatic] static StringBuilder sb;

        static string Str(string s, ref int i)
        {
            i++; // opening quote
            int start = i;
            // Fast path: no escapes.
            while (i < s.Length && s[i] != '"' && s[i] != '\\') i++;
            if (s[i] == '"')
            {
                string fast = s.Substring(start, i - start);
                i++;
                return fast;
            }
            if (sb == null) sb = new StringBuilder(64);
            sb.Clear();
            sb.Append(s, start, i - start);
            while (s[i] != '"')
            {
                char c = s[i++];
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            i++;
            return sb.ToString();
        }

        static object Num(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double v);
            return v;
        }

        // ── Readers ─────────────────────────────────────────────────────────────────────

        public static float F(List<object> a, int i, float def = 0f)
        {
            if (a == null || i >= a.Count) return def;
            object o = a[i];
            if (o is double d) return (float)d;
            if (o is bool b) return b ? 1f : 0f;
            return def;
        }

        public static int I(List<object> a, int i, int def = 0)
        {
            if (a == null || i >= a.Count) return def;
            object o = a[i];
            if (o is double d) return (int)System.Math.Round(d);
            if (o is bool b) return b ? 1 : 0;
            return def;
        }

        public static bool B(List<object> a, int i) => I(a, i) != 0;

        public static string S(List<object> a, int i, string def = "")
        {
            if (a == null || i >= a.Count) return def;
            return a[i] as string ?? def;
        }

        public static List<object> L(List<object> a, int i) => a != null && i < a.Count ? a[i] as List<object> : null;

        public static List<object> L(Dictionary<string, object> d, string key) => d != null && d.TryGetValue(key, out object o) ? o as List<object> : null;

        public static string S(Dictionary<string, object> d, string key, string def = "") => d != null && d.TryGetValue(key, out object o) && o is string s ? s : def;

        public static float F(Dictionary<string, object> d, string key, float def = 0f)
        {
            if (d == null || !d.TryGetValue(key, out object o)) return def;
            if (o is double v) return (float)v;
            if (o is bool b) return b ? 1f : 0f;
            return def;
        }

        public static int I(Dictionary<string, object> d, string key, int def = 0) => (int)System.Math.Round(F(d, key, def));

        public static UnityEngine.Color Color(string hex, UnityEngine.Color def)
        {
            if (string.IsNullOrEmpty(hex)) return def;
            try
            {
                return ColorUtil.Hex(hex);
            }
            catch
            {
                return def;
            }
        }
    }
}
