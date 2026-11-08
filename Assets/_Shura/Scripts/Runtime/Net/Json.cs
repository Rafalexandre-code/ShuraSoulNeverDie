using System.Globalization;
using System.Text;
using UnityEngine;

namespace Shura
{
    /// <summary>Minimal, allocation-light JSON writing for network snapshots.</summary>
    public static class Json
    {
        public static StringBuilder Num(this StringBuilder sb, float v, int decimals = 2)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) v = 0f;
            float m = Mathf.Pow(10f, decimals);
            float r = Mathf.Round(v * m) / m;
            return sb.Append(r.ToString("0.##", CultureInfo.InvariantCulture));
        }

        public static StringBuilder Int(this StringBuilder sb, int v) => sb.Append(v.ToString(CultureInfo.InvariantCulture));

        public static StringBuilder Bool(this StringBuilder sb, bool v) => sb.Append(v ? '1' : '0');

        public static StringBuilder Str(this StringBuilder sb, string s)
        {
            sb.Append('"');
            if (s != null)
            {
                foreach (char c in s)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                            else sb.Append(c);
                            break;
                    }
                }
            }
            return sb.Append('"');
        }

        public static StringBuilder Hex(this StringBuilder sb, Color c) => sb.Append('"').Append('#').Append(ColorUtility.ToHtmlStringRGB(c)).Append('"');
    }
}
