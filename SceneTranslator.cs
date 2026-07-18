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
    /// - Текст переводится через RichTextTranslator (сохраняет разделители \t \n ...).
    /// - Шрифт и размер НЕ трогаем — игра сама выставила их под свой лейаут,
    ///   и позиционно-чувствительные места (настройки с колонками) ломаются от
    ///   любого масштабирования (fontSize или localScale сдвигают якорь).
    ///   Русский местами чуть шире английского — это допустимо, вёрстка целая.
    /// - EN/RU (F2) восстанавливает английский оригинал.
    /// </summary>
    public class SceneTranslator : MonoBehaviour
    {
        public static SceneTranslator Instance;

        // Английские оригиналы (для восстановления при EN).
        private static readonly Dictionary<TextMesh, string> _originals = new Dictionary<TextMesh, string>();

        private float _timer = 0f;
        private const float INTERVAL = 0.5f;

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
                        // Запоминаем английский оригинал, пока ещё не переводили.
                        if (!_originals.ContainsKey(tm) && !ContainsCyrillic(cur))
                            _originals[tm] = cur;

                        // Переводим через RichTextTranslator — он сохраняет разделители
                        // (\t \n ...), так что вёрстка колонок не ломается.
                        bool full, any;
                        string result = RichTextTranslator.Translate(cur, out full, out any);

                        if (any && result != cur)
                        {
                            tm.text = result;
                            applied++;
                        }
                        if (!full) queued++;
                    }
                    else
                    {
                        if (_originals.TryGetValue(tm, out var orig) && orig != cur)
                        {
                            tm.text = orig;
                            restored++;
                        }
                    }
                }

                Plugin.Log?.LogInfo("[SCAN] TextMesh=" + meshes.Length + ", применено=" + applied + ", в очередь=" + queued + ", восстановлено=" + restored);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("[SCAN] ошибка: " + ex.Message);
            }
        }

        private static bool ContainsCyrillic(string s)
        {
            if (s == null) return false;
            foreach (var c in s)
                if ((c >= '\u0400' && c <= '\u04FF') || (c >= '\u0500' && c <= '\u052F')) return true;
            return false;
        }
    }
}
