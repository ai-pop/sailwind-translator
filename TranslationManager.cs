using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SailwindTranslator
{
    /// <summary>
    /// Простой JSON-парсер для Dictionary&lt;string,string&gt;.
    /// Без System.Reflection.Emit, без Newtonsoft — работает везде,
    /// включая Unity Mono с отключённым SRE.
    /// Формат: { "key1": "value1", "key2": "value2", ... }
    /// </summary>
    internal static class SimpleJson
    {
        public static Dictionary<string, string> Parse(string text)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(text)) return result;

            var p = new Parser(text);
            p.SkipWs();
            if (!p.Match('{')) return result;

            p.SkipWs();
            if (p.Match('}')) return result;

            while (true)
            {
                p.SkipWs();
                var key = p.ReadString();
                p.SkipWs();
                if (!p.Match(':'))
                    throw new FormatException($"Expected ':' at pos {p.i}");
                p.SkipWs();
                var val = p.ReadString();
                result[key] = val;
                p.SkipWs();
                if (p.Match(',')) continue;
                if (p.Match('}')) break;
                throw new FormatException($"Expected ',' or '}}' at pos {p.i}");
            }
            return result;
        }

        public static string Serialize(IReadOnlyDictionary<string, string> dict)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            int i = 0, n = dict.Count;
            // Сортируем по ключу для читаемости
            foreach (var kv in dict.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                sb.Append("  ");
                AppendEscaped(sb, kv.Key);
                sb.Append(": ");
                AppendEscaped(sb, kv.Value ?? "");
                if (++i < n) sb.Append(',');
                sb.Append('\n');
            }
            sb.Append("}\n");
            return sb.ToString();
        }

        private static void AppendEscaped(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20)
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private struct Parser
        {
            public readonly string s;
            public int i;
            public Parser(string s) { this.s = s; this.i = 0; }

            public void SkipWs()
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            public bool Match(char c)
            {
                if (i < s.Length && s[i] == c) { i++; return true; }
                return false;
            }

            public string ReadString()
            {
                SkipWs();
                if (i >= s.Length || s[i] != '"')
                    throw new FormatException($"Expected string at pos {i}");
                i++;
                var sb = new StringBuilder();
                while (i < s.Length)
                {
                    var c = s[i];
                    if (c == '"') { i++; return sb.ToString(); }
                    if (c == '\\')
                    {
                        i++;
                        if (i >= s.Length) throw new FormatException("Trailing backslash");
                        var e = s[i++];
                        switch (e)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'u':
                                if (i + 4 > s.Length) throw new FormatException("Bad \\u");
                                var hex = s.Substring(i, 4);
                                sb.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                                i += 4;
                                break;
                            default: throw new FormatException($"Unknown escape \\{e}");
                        }
                    }
                    else
                    {
                        sb.Append(c);
                        i++;
                    }
                }
                throw new FormatException("Unterminated string");
            }
        }
    }

    /// <summary>
    /// Управляет словарём переводов translations.json рядом с плагином.
    /// Hot-reload: если файл меняется на диске, перезагружается автоматически.
    /// Без внешних зависимостей — свой JSON-парсер.
    /// </summary>
    public class TranslationManager
    {
        private static readonly string PluginDir =
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        private static readonly string TranslationsPath = Path.Combine(PluginDir, "translations.json");
        private static readonly string UntranslatedPath = Path.Combine(PluginDir, "untranslated.csv");

        private Dictionary<string, string> _dict = new Dictionary<string, string>(StringComparer.Ordinal);
        private DateTime _lastWrite = DateTime.MinValue;
        private readonly HashSet<string> _untranslatedDumped = new HashSet<string>();
        private readonly object _lock = new object();

        public int Count
        {
            get { lock (_lock) return _dict.Count; }
        }

        public string PluginDirectory => PluginDir;

        public void Load()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(TranslationsPath))
                    {
                        _dict = new Dictionary<string, string>(StringComparer.Ordinal);
                        Save();
                        Plugin.Log.LogInfo($"translations.json не найден — создан пустой: {TranslationsPath}");
                    }
                    else
                    {
                        var text = File.ReadAllText(TranslationsPath, Encoding.UTF8);
                        _dict = SimpleJson.Parse(text);

                        // Очистка от мусора, накопившегося от старых версий плагина:
                        // записи с hex-хвостом (tracking-ID Google) или пустые значения.
                        int before = _dict.Count;
                        var keys = _dict.Keys.ToList();
                        foreach (var k in keys)
                        {
                            var v = _dict[k];
                            if (v == null || v.Length == 0 ||
                                System.Text.RegularExpressions.Regex.IsMatch(v, "[0-9a-fA-F]{16,}"))
                            {
                                _dict.Remove(k);
                                Plugin.Log?.LogInfo("[CLEAN] удалён мусорный перевод: '" + (k.Length <= 40 ? k : k.Substring(0, 40) + "…") + "'");
                            }
                        }
                        if (_dict.Count != before)
                        {
                            Plugin.Log?.LogInfo("[CLEAN] удалено " + (before - _dict.Count) + " мусорных переводов. Сохраняю очищенный словарь.");
                            Save();
                        }

                        _lastWrite = File.GetLastWriteTime(TranslationsPath);
                        Plugin.Log.LogInfo($"Загружено {_dict.Count} переводов из {TranslationsPath}");
                    }

                    if (File.Exists(UntranslatedPath))
                    {
                        foreach (var line in File.ReadAllLines(UntranslatedPath, Encoding.UTF8))
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                var trimmed = line.Trim();
                                if (trimmed.StartsWith("\"") && trimmed.EndsWith("\"") && trimmed.Length >= 2)
                                    trimmed = trimmed.Substring(1, trimmed.Length - 2);
                                _untranslatedDumped.Add(trimmed);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Ошибка загрузки translations.json: {ex}");
                    _dict = new Dictionary<string, string>(StringComparer.Ordinal);
                }
            }
        }

        public void Save()
        {
            lock (_lock)
            {
                try
                {
                    var text = SimpleJson.Serialize(_dict);
                    File.WriteAllText(TranslationsPath, text, Encoding.UTF8);
                    _lastWrite = File.GetLastWriteTime(TranslationsPath);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Ошибка сохранения translations.json: {ex}");
                }
            }
        }

        public void CheckHotReload()
        {
            try
            {
                if (!File.Exists(TranslationsPath)) return;
                var wt = File.GetLastWriteTime(TranslationsPath);
                if (wt != _lastWrite)
                {
                    Load();
                }
            }
            catch { /* ignore */ }
        }

        public string Get(string english)
        {
            if (string.IsNullOrEmpty(english)) return null;
            lock (_lock)
            {
                if (_dict.TryGetValue(english, out var ru)) return ru;
                var trimmed = english.Trim();
                if (_dict.TryGetValue(trimmed, out ru)) return ru;
                return null;
            }
        }

        public bool Has(string english)
        {
            if (string.IsNullOrEmpty(english)) return true;
            lock (_lock)
            {
                return _dict.ContainsKey(english) || _dict.ContainsKey(english.Trim());
            }
        }

        public void Set(string english, string russian)
        {
            if (string.IsNullOrEmpty(english)) return;
            lock (_lock) _dict[english] = russian ?? "";
        }

        public bool Remove(string english)
        {
            lock (_lock) return _dict.Remove(english);
        }

        public void DumpUntranslated(string english)
        {
            if (!Plugin.CfgDumpUntranslated.Value) return;
            if (string.IsNullOrWhiteSpace(english)) return;
            lock (_lock)
            {
                if (_untranslatedDumped.Contains(english)) return;
                _untranslatedDumped.Add(english);
                try
                {
                    File.AppendAllText(UntranslatedPath,
                        "\"" + english.Replace("\"", "\"\"") + "\"\n",
                        Encoding.UTF8);
                }
                catch { /* ignore */ }
            }
        }

        public List<KeyValuePair<string, string>> All()
        {
            lock (_lock) return _dict.OrderBy(p => p.Key).ToList();
        }

        public List<KeyValuePair<string, string>> Search(string query, int max = 200)
        {
            lock (_lock)
            {
                var q = (query ?? "").ToLowerInvariant();
                var src = string.IsNullOrEmpty(q)
                    ? _dict
                    : _dict.Where(p => p.Key.ToLowerInvariant().Contains(q) ||
                                       (p.Value ?? "").ToLowerInvariant().Contains(q));
                return src.Take(max).ToList();
            }
        }
    }
}
