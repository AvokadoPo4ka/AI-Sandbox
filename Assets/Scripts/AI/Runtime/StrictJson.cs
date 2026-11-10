using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// Small bounded JSON parser. Unlike JsonUtility it rejects duplicate keys and malformed input.
namespace RuntimeCreation {
    public static class StrictJson {
        public static object Parse(string text, int maxChars = 131072) {
            if (text == null || text.Length > maxChars) throw new FormatException("JSON response exceeds size limit.");
            var p = new Parser(text); var value = p.Value(0); p.White();
            if (p.i != text.Length) throw new FormatException("Unexpected trailing JSON data.");
            return value;
        }
        sealed class Parser {
            readonly string s; public int i; int nodes;
            public Parser(string text) { s = text; }
            public void White() { while (i < s.Length && (s[i] == ' ' || s[i] == '\n' || s[i] == '\r' || s[i] == '\t')) i++; }
            bool Take(char c) { White(); if (i < s.Length && s[i] == c) { i++; return true; } return false; }
            void Expect(char c) { if (!Take(c)) throw new FormatException("Expected '" + c + "' at character " + i); }
            public object Value(int depth) {
                White(); if (++nodes > 20000 || depth > 32) throw new FormatException("JSON nesting or node limit exceeded.");
                if (i >= s.Length) throw new FormatException("Unexpected end of JSON.");
                if (s[i] == '{') { i++; var d = new Dictionary<string, object>(); if (Take('}')) return d; do { White(); if (i >= s.Length || s[i] != '"') throw new FormatException("Expected object key."); string k = String(); Expect(':'); if (d.ContainsKey(k)) throw new FormatException("Duplicate key: " + k); d.Add(k, Value(depth + 1)); if (Take('}')) return d; Expect(','); } while (true); }
                if (s[i] == '[') { i++; var a = new List<object>(); if (Take(']')) return a; do { a.Add(Value(depth + 1)); if (Take(']')) return a; Expect(','); } while (true); }
                if (s[i] == '"') return String();
                if (s[i] == 't' && Literal("true")) return true;
                if (s[i] == 'f' && Literal("false")) return false;
                if (s[i] == 'n' && Literal("null")) return null;
                int start = i; if (s[i] == '-') i++; if (i >= s.Length) throw new FormatException("Invalid number.");
                if (s[i] == '0') i++; else { if (s[i] < '1' || s[i] > '9') throw new FormatException("Invalid JSON value at " + i); while (i < s.Length && char.IsDigit(s[i]) && s[i] <= '9') i++; }
                if (i < s.Length && s[i] == '.') { i++; int z = i; while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++; if (z == i) throw new FormatException("Invalid fraction."); }
                if (i < s.Length && (s[i] == 'e' || s[i] == 'E')) { i++; if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++; int z = i; while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++; if (z == i) throw new FormatException("Invalid exponent."); }
                double v; if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out v) || double.IsInfinity(v) || double.IsNaN(v)) throw new FormatException("Invalid number."); return v;
            }
            bool Literal(string t) { if (i + t.Length > s.Length || string.CompareOrdinal(s, i, t, 0, t.Length) != 0) return false; i += t.Length; return true; }
            string String() {
                Expect('"'); var b = new StringBuilder();
                while (i < s.Length) { char ch = s[i++]; if (ch == '"') return b.ToString(); if (ch < 32) throw new FormatException("Control character in string.");
                    if (ch == '\\') { if (i >= s.Length) throw new FormatException("Incomplete escape."); ch = s[i++]; switch (ch) { case '"': case '\\': case '/': b.Append(ch); break; case 'b': b.Append('\b'); break; case 'f': b.Append('\f'); break; case 'n': b.Append('\n'); break; case 'r': b.Append('\r'); break; case 't': b.Append('\t'); break; case 'u': if (i + 4 > s.Length) throw new FormatException("Incomplete Unicode escape."); ushort code; if (!ushort.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code)) throw new FormatException("Invalid Unicode escape."); b.Append((char)code); i += 4; break; default: throw new FormatException("Invalid escape."); } } else b.Append(ch);
                } throw new FormatException("Unterminated string.");
            }
        }
    }
}
