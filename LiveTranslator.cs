using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace SailwindTranslator
{
    /// <summary>
    /// Живой переводчик: переводит ЛЮБОЙ английский текст в реальном времени
    /// и кеширует результат в translations.json.
    ///
    /// Провайдеры (по приоритету):
    ///   1. Google Translate (endpoint gtx, без ключа, без жёстких лимитов) — быстрый.
    ///   2. MyMemory (бесплатный, без ключа) — фоллбэк.
    ///
    /// Несколько фоновых потоков для скорости. Главный поток только ставит строки
    /// в очередь и читает готовый результат из словаря.
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

        /// <summary>Главный поток опрашивает это поле: true = пришли новые переводы, надо пересканировать.</summary>
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
            Plugin.Log?.LogInfo("[LIVE] фоновый переводчик запущен: " + workers + " поток(ов), Google+MyMemory.");
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

            lock (_lock)
            {
                if (_queued.Contains(english) || _inProgress.Contains(english)) return;
                if (_failed.Contains(english)) return; // уже пробовали — не получилось
                if (Plugin.Manager != null && Plugin.Manager.Has(english)) return;
                _queued.Add(english);
                Monitor.PulseAll(_lock);
            }
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
                catch (Exception ex) { Plugin.Log?.LogWarning("[LIVE] Google не ответил для '" + Trunc(item) + "': " + ex.Message); }

                if (string.IsNullOrEmpty(ru) || ru == item)
                {
                    try { ru = TranslateMyMemory(item); }
                    catch (Exception ex) { Plugin.Log?.LogWarning("[LIVE] MyMemory не ответил: " + ex.Message); }
                }

                if (!string.IsNullOrEmpty(ru) && ru != item)
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
                Thread.Sleep(80); // лёгкая пауза между запросами
            }
        }

        // Google Translate (неофициальный endpoint gtx). Без ключа. Быстро.
        private static string TranslateGoogle(string en)
        {
            string url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=ru&dt=t&q=" + Uri.EscapeDataString(en);
            using (var wc = new WebClient { Encoding = Encoding.UTF8 })
            {
                wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0";
                string json = wc.DownloadString(url);
                // Ответ: [[["перевод","оригинал",...],...],...]
                var sb = new StringBuilder();
                var matches = Regex.Matches(json, @"\[\[""(.*?)"",""(.*?)""");
                foreach (Match m in matches)
                    sb.Append(Unescape(m.Groups[1].Value));
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
