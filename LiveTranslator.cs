using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace SailwindTranslator
{
    /// <summary>
    /// Живой переводчик: переводит английский текст в реальном времени и кеширует
    /// результат в translations.json.
    ///
    /// Провайдеры: Google Translate (gtx, без ключа, быстро) -> MyMemory (фоллбэк).
    /// Несколько фоновых потоков для скорости.
    ///
    /// ВАЖНО: технический текст (коды клавиш, одиночные символы, чистые числа,
    /// строки без латиницы) НЕ переводится — иначе получается 'F1'->'Ф1',
    /// 'W'->'Вт', 'Tab'->'Вкладка', мусор.
    /// </summary>
    public static class LiveTranslator
    {
        private static readonly HashSet<string> _queued = new HashSet<string>();
        private static readonly HashSet<string> _inProgress = new HashSet<string>();
        private static readonly HashSet<string> _failed = new HashSet<string>();
        private static readonly object _lock = new object();
        private static readonly List<Thread> _threads = new List<Thread>();
        private static volatile bool _running;
        private static DateTime _lastSave = DateTime.MinValue;

        public static volatile bool NeedsRescan;

        public static void Start()
        {
            if (_running) return;
            _running = true;
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }
            try { ServicePointManager.DefaultConnectionLimit = 8; } catch { }

            int workers = Plugin.CfgLiveWorkers != null ? Plugin.CfgLiveWorkers.Value : 3;
            for (int i = 0; i < workers; i++)
            {
                var t = new Thread(Worker) { IsBackground = true, Name = "SailwindTranslator.Live#" + i };
                _threads.Add(t);
                t.Start();
            }
            Plugin.Log?.LogInfo("[LIVE] фоновый переводчик запущен: " + workers + " поток(ов).");
        }

        public static void Stop()
        {
            _running = false;
            lock (_lock) Monitor.PulseAll(_lock);
        }

        public static void Enqueue(string english)
        {
            if (Plugin.CfgLiveTranslate == null || !Plugin.CfgLiveTranslate.Value) return;
            if (string.IsNullOrWhiteSpace(english)) return;
            if (ContainsCyrillic(english)) return;
            if (english.Length > 500) return;

            if (ShouldSkip(english)) return;

            lock (_lock)
            {
                if (_queued.Contains(english) || _inProgress.Contains(english)) return;
                if (_failed.Contains(english)) return;
                if (Plugin.Manager != null && Plugin.Manager.Has(english)) return;
                _queued.Add(english);
                Monitor.PulseAll(_lock);
            }
        }

        /// <summary>
        /// Пропустить «технический» текст, который нельзя осмысленно перевести
        /// и который даёт мусор вроде 'F1'->'Ф1', 'W'->'Вт', 'Tab'->'Вкладка'.
        /// </summary>
        private static bool ShouldSkip(string s)
        {
            string t = s.Trim();
            if (t.Length == 0) return true;
            if (t.Length <= 2) return true; // одиночные символы/клавиши: W, A, S, D, F1...

            // Чистые числа/дроби/даты.
            bool allDigits = true;
            foreach (var c in t)
            {
                if (!char.IsDigit(c) && c != '.' && c != ',' && c != ' ' && c != ':' && c != '/') { allDigits = false; break; }
            }
            if (allDigits) return true;

            if (IsKeyCode(t)) return true;

            // Нет ни одной латинской буквы — переводить нечего.
            bool hasLetter = false;
            foreach (var c in t)
            {
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) { hasLetter = true; break; }
            }
            if (!hasLetter) return true;

            return false;
        }

        private static bool IsKeyCode(string s)
        {
            switch (s)
            {
                case "Space": case "Tab": case "CapsLock":
                case "LeftShift": case "RightShift":
                case "LeftControl": case "RightControl":
                case "LeftAlt": case "RightAlt":
                case "UpArrow": case "DownArrow": case "LeftArrow": case "RightArrow":
                case "Return": case "Escape": case "Backspace": case "Delete": case "Insert":
                case "Home": case "End": case "PageUp": case "PageDown":
                case "KeypadEnter": case "Numlock":
                    return true;
            }
            if (s.Length >= 2 && s[0] == 'F' && char.IsDigit(s[1])) return true;            // F1..F15
            if (s.StartsWith("Alpha", StringComparison.Ordinal) && s.Length > 5) return true;  // Alpha0..9
            if (s.StartsWith("Keypad", StringComparison.Ordinal) && s.Length > 6) return true; // Keypad0..9
            if (s.StartsWith("Joystick", StringComparison.Ordinal)) return true;
            return false;
        }

        private static void Worker()
        {
            while (_running)
            {
                string item = null;
                lock (_lock)
                {
                    while (_queued.Count == 0 && _running)
                        Monitor.Wait(_lock, 5000);
                    if (!_running) return;
                    foreach (var s in _queued) { item = s; break; }
                    if (item != null) { _queued.Remove(item); _inProgress.Add(item); }
                }
                if (item == null) continue;

                string ru = null;
                try { ru = TranslateGoogle(item); }
                catch (Exception ex) { Plugin.Log?.LogWarning("[LIVE] Google не ответил: " + ex.Message); }

                if (string.IsNullOrEmpty(ru) || ru == item)
                {
                    try { ru = TranslateMyMemory(item); }
                    catch (Exception ex) { Plugin.Log?.LogWarning("[LIVE] MyMemory не ответил: " + ex.Message); }
                }

                // Финальная очистка: убрать хеши/tracking-ID, которые Google иногда дописывает.
                ru = Cleanup(ru);

                if (!string.IsNullOrEmpty(ru) && ru != item && !IsGarbage(ru))
                {
                    Plugin.Manager?.Set(item, ru);
                    NeedsRescan = true;
                    if ((DateTime.UtcNow - _lastSave).TotalSeconds > 3)
                    {
                        Plugin.Manager?.Save();
                        _lastSave = DateTime.UtcNow;
                    }
                    Plugin.Log?.LogInfo("[LIVE] переведено: '" + Trunc(item) + "' -> '" + Trunc(ru) + "'");
                }
                else
                {
                    lock (_lock) _failed.Add(item);
                }

                lock (_lock) _inProgress.Remove(item);
                Thread.Sleep(80);
            }
        }

        /// <summary>
        /// Убирает мусор из перевода: длинные hex-строки (tracking-ID Google вида
        /// '8e6adaf1f9ae06bcb9663531e5521abb'), который прицеплялся в конец.
        /// </summary>
        private static string Cleanup(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            // Срезаем любой хвост, начинающийся с длинной hex-строки (>=16 hex-символов подряд).
            s = Regex.Replace(s, "[0-9a-fA-F]{16,}.*$", "", RegexOptions.Singleline);
            return s.Trim();
        }

        /// <summary>true, если результат выглядит как мусор (цифры+хеш и т.п.).</summary>
        private static bool IsGarbage(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return true;
            // Если после очистки остались только hex-символы и пробелы — мусор.
            bool allHex = true;
            foreach (var c in s)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F') || char.IsWhiteSpace(c)))
                { allHex = false; break; }
            }
            return allHex;
        }

        private static string TranslateGoogle(string en)
        {
            string url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=ru&dt=t&q=" + Uri.EscapeDataString(en);
            using (var wc = new WebClient { Encoding = Encoding.UTF8 })
            {
                wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0";
                string json = wc.DownloadString(url);
                var sb = new StringBuilder();
                // Формат: [[["перевод","оригинал",...],...],...]
                var matches = Regex.Matches(json, @"\[\[""(?<tr>(?:[^""\\]|\\.)*)"",""(?<src>(?:[^""\\]|\\.)*)""");
                foreach (Match m in matches)
                    sb.Append(Unescape(m.Groups["tr"].Value));
                return sb.Length == 0 ? null : sb.ToString();
            }
        }

        private static string TranslateMyMemory(string en)
        {
            string url = "https://api.mymemory.translated.net/get?q=" + Uri.EscapeDataString(en) + "&langpair=en|ru";
            using (var wc = new WebClient { Encoding = Encoding.UTF8 })
            {
                string json = wc.DownloadString(url);
                var m = Regex.Match(json, @"""translatedText""\s*:\s*""((?:[^""\\]|\\.)*)""");
                if (!m.Success) return null;
                string txt = Unescape(m.Groups[1].Value);
                if (txt.IndexOf("MYMEMORY WARNING", StringComparison.OrdinalIgnoreCase) >= 0) return null;
                return txt;
            }
        }

        private static string Unescape(string s)
        {
            s = Regex.Replace(s, @"\\u([0-9a-fA-F]{4})", me => ((char)Convert.ToInt32(me.Groups[1].Value, 16)).ToString());
            s = s.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\/", "/");
            return s;
        }

        private static bool ContainsCyrillic(string s)
        {
            foreach (var c in s)
                if ((c >= '\u0400' && c <= '\u04FF') || (c >= '\u0500' && c <= '\u052F')) return true;
            return false;
        }

        private static string Trunc(string s)
        {
            if (s == null) return "<null>";
            return s.Length <= 60 ? s : s.Substring(0, 60) + "…";
        }
    }
}
