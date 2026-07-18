using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SailwindTranslator
{
    /// <summary>
    /// Активный переводчик сцены — переводит ВЕСЬ видимый текст в реальном времени.
    ///
    /// - Периодически (и на каждой загрузке сцены) перебирает все TextMesh.
    /// - Для каждой строки: если перевод уже есть в словаре (ручной или ранее
    ///   закешированный живым переводчиком) — применяет его.
    /// - Если перевода нет — отдаёт строку в LiveTranslator (он переведёт онлайн,
    ///   положит в словарь и взведёт NeedsRescan — тогда перевод подставится
    ///   следующим проходом).
    /// - Когда LiveTranslator докладывает о новых переводах (NeedsRescan=true),
    ///   запускает внеочередной проход — так текст обновляется «на лету».
    /// - EN/RU (F2) восстанавливает английские оригиналы.
    ///
    /// Динамические строки (lookText при наведении) дополнительно ловятся
    /// Harmony-патчем на TextMesh.text — он тоже ставит незнакомки в очередь.
    /// </summary>
    public class SceneTranslator : MonoBehaviour
    {
        public static SceneTranslator Instance;

        private static readonly Dictionary<TextMesh, string> _originals = new Dictionary<TextMesh, string>();
        private static readonly Dictionary<TextMesh, int> _origFontSize = new Dictionary<TextMesh, int>();

        private float _timer = 0f;
        private const float INTERVAL = 0.5f;
        private const float FIT_MIN_SCALE = 0.6f;

        private void Start()
        {
            Instance = this;
            try { SceneManager.sceneLoaded += OnSceneLoaded; } catch { }
            Invoke(nameof(ScanNow), 2f);
        }

        private void OnDestroy()
        {
            try { SceneManager.sceneLoaded -= OnSceneLoaded; } catch { }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CancelInvoke(nameof(ScanNow));
            Invoke(nameof(ScanNow), 1f);
            Invoke(nameof(ScanNow), 3f);
            Invoke(nameof(ScanNow), 6f);
        }

        private void Update()
        {
            // Живой переводчик принёс новые переводы — пересканируем сразу.
            if (LiveTranslator.NeedsRescan)
            {
                LiveTranslator.NeedsRescan = false;
                ScanNow();
                _timer = 0f;
                return;
            }
            _timer += Time.deltaTime;
            if (_timer < INTERVAL) return;
            _timer = 0f;
            ScanNow();
        }

        public static void OnLanguageChanged()
        {
            Instance?.ScanNow();
        }

        public void ScanNow()
        {
            try
            {
                bool ru = Plugin.CfgLanguage != null && Plugin.CfgLanguage.Value == "ru";
                var meshes = FindObjectsOfType<TextMesh>();
                int applied = 0, restored = 0, queued = 0;

                foreach (var tm in meshes)
                {
                    if (tm == null) continue;

                    if (ru && Plugin.FontResolver != null)
                        Plugin.FontResolver.ApplyTo(tm);

                    string cur = tm.text;
                    if (string.IsNullOrEmpty(cur)) continue;

                    if (ru)
                    {
                        if (!_originals.ContainsKey(tm) && !ContainsCyrillic(cur))
                            _originals[tm] = cur;

                        // Форматированные подписи (с табуляциями/escape) — не трогаем,
                        // перевод ломает вёрстку (literal '/t/t/t' в настройках управления).
                        if (cur.IndexOf('\t') >= 0 || cur.IndexOf("\\t", System.StringComparison.Ordinal) >= 0)
                        {
                            continue;
                        }

                        if (ContainsCyrillic(cur)) continue;

                        var ruText = Plugin.Manager?.Get(cur);
                        if (!string.IsNullOrEmpty(ruText) && ruText != cur)
                        {
                            // Запоминаем оригинальный размер шрифта, чтобы потом подгонять.
                            if (!_origFontSize.ContainsKey(tm))
                                _origFontSize[tm] = tm.fontSize;

                            tm.text = ruText;
                            FitFontSize(tm, cur, ruText);
                            applied++;
                        }
                        else
                        {
                            // Нет перевода — в очередь живому переводчику.
                            LiveTranslator.Enqueue(cur);
                            queued++;
                        }
                    }
                    else
                    {
                        if (_originals.TryGetValue(tm, out var orig) && orig != cur)
                        {
                            tm.text = orig;
                            // Возвращаем оригинальный размер шрифта.
                            if (_origFontSize.TryGetValue(tm, out var ofs))
                                tm.fontSize = ofs;
                            restored++;
                        }
                    }
                }

                Plugin.Log?.LogInfo($"[SCAN] TextMesh={meshes.Length}, применено={applied}, в очередь={queued}, восстановлено={restored}");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[SCAN] ошибка: {ex.Message}");
            }
        }

        private static bool ContainsCyrillic(string s)
        {
            if (s == null) return false;
            foreach (var c in s)
                if ((c >= '\u0400' && c <= '\u04FF') || (c >= '\u0500' && c <= '\u052F')) return true;
            return false;
        }

        /// <summary>Видимая длина самой длинной строки (по строкам, без \n).</summary>
        private static int VisibleLength(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int max = 0;
            foreach (var line in s.Split('\n'))
            {
                int len = line.Length;
                if (len > max) max = len;
            }
            return max;
        }

        /// <summary>
        /// Подгон размера шрифта под перевод. Английский компактнее русского,
        /// поэтому перевод часто шире и вылезает за UI. Пропорционально уменьшаем
        /// fontSize (но не ниже FIT_MIN_SCALE от оригинала).
        /// </summary>
        private static void FitFontSize(TextMesh tm, string original, string translated)
        {
            if (!_origFontSize.TryGetValue(tm, out int origSize)) return;
            if (origSize <= 0) return;

            int enLen = VisibleLength(original);
            int ruLen = VisibleLength(translated);
            if (enLen <= 0 || ruLen <= 0) { tm.fontSize = origSize; return; }

            if (ruLen <= enLen) { tm.fontSize = origSize; return; }

            float scale = (float)enLen / ruLen;
            if (scale < FIT_MIN_SCALE) scale = FIT_MIN_SCALE;
            int newSize = Mathf.Max(1, Mathf.RoundToInt(origSize * scale));
            tm.fontSize = newSize;
        }
    }
}
