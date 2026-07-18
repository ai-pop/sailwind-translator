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
    /// через бесплатный онлайн-API (MyMemory, без ключа) и кеширует результат
    /// в translations.json. Второй запуск — мгновенно из кеша.
    ///
    /// Никакого ручного составления словаря: что увидел в сцене — то и перевёл.
    /// Ручные правки (через F3) по-прежнему имеют приоритет (читаются первыми).
    ///
    /// Перевод идёт в фоновом потоке, чтобы не фризить игру. Главный поток только
    /// ставит строки в очередь и читает готовый результат из словаря.
    /// </summary>
    public static class LiveTranslator
    {
        private static readonly HashSet<string> _queued = new HashSet<string>();
        private static readonly HashSet<string> _inProgress = new HashSet<string>();
        private static readonly object _lock = new object();
        private static Thread _thread;
        private static volatile bool _running;
        private static DateTime _lastSave = DateTime.MinValue;

        /// <summary>Главный поток опрашивает это поле: true = пришли новые переводы, надо пересканировать.</summary>
        public static volatile bool NeedsRescan;

        public static void Start()
        {
            if (_running) return;
            _running = true;
            // Включаем TLS 1.2 для HTTPS на старом Mono (число 3072 = Tls12).
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }
            _thread = new Thread(Worker) { IsBackground = true, Name = "SailwindTranslator.Live" };
            _thread.Start();
            Plugin.Log?.LogInfo("[LIVE] фоновый переводчик запущен (MyMemory API).");
        }

        public static void Stop()
        {
            _running = false;
            lock (_lock) Monitor.Pulse(_lock);
        }

        /// <summary>Поставить английскую строку в очередь на перевод (если ещё не переведена).</summary>
        public static void Enqueue(string english)
        {
            if (!Plugin.CfgLiveTranslate) return;
            if (string.IsNullOrWhiteSpace(english)) return;
            if (ContainsCyrillic(english)) return; // уже не английский
            if (english.Length > 500) return;       // лимит API

            lock (_lock)
            {
                if (_queued.Contains(english) || _inProgress.Contains(english)) return;
                if (Plugin.Manager != null && Plugin.Manager.Has(english)) return; // уже переведено/в словаре
                _queued.Add(english);
                Monitor.Pulse(_lock);
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
                try { ru = TranslateOnline(item); }
                catch (Exception ex) { Plugin.Log?.LogWarning("[LIVE] ошибка перевода '" + Trunc(item) + "': " + ex.Message); }

                if (!string.IsNullOrEmpty(ru) && ru != item)
                {
                    Plugin.Manager?.Set(item, ru);
                    NeedsRescan = true;
                    // дебаунс сохранения: не чаще раза в 3 сек
                    if ((DateTime.UtcNow - _lastSave).TotalSeconds > 3)
                    {
                        Plugin.Manager?.Save();
                        _lastSave = DateTime.UtcNow;
                    }
                    Plugin.Log?.LogInfo("[LIVE] переведено: '" + Trunc(item) + "' -> '" + Trunc(ru) + "'");
                }

                lock (_lock) _inProgress.Remove(item);
                Thread.Sleep(400); // бережём лимиты API
            }
        }

        private static string TranslateOnline(string en)
        {
            string url = "https://api.mymemory.translated.net/get?q=" + Uri.EscapeDataString(en) + "&langpair=en|ru";
            using (var wc = new WebClient { Encoding = Encoding.UTF8 })
            {
                string json = wc.DownloadString(url);
                // MyMemory: { "responseData": { "translatedText": "..." }, ... }
                var m = Regex.Match(json, @"""translatedText""\s*:\s*""((?:[^""\\]|\\.)*)""");
                if (!m.Success) return null;
                string txt = m.Groups[1].Value;
                txt = Regex.Replace(txt, @"\\u([0-9a-fA-F]{4})", me => ((char)Convert.ToInt32(me.Groups[1].Value, 16)).ToString());
                txt = txt.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
                if (txt.IndexOf("MYMEMORY WARNING", StringComparison.OrdinalIgnoreCase) >= 0) return null;
                return txt;
            }
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
