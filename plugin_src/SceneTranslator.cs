using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SailwindTranslator
{
    /// <summary>
    /// Активный переводчик сцены.
    ///
    /// Проблема, которую решает: Harmony-патч на TextMesh.text ловит только
    /// ПРОГРАММНЫЕ вызовы (LookUI.ShowLookText, UpdateText...). Но текст кнопок
    /// главного меню («New Game», «Options»...) ЗАШИТ в префабы и выставляется
    /// движком при загрузке сцены В ОБХОД C#-сеттера — сеттер-патч его не видит.
    ///
    /// Решение: периодически (и на каждой загрузке сцены) перебираем все
    /// TextMesh в сцене, читаем их текущий .text и переводим напрямую.
    /// Оригиналы сохраняем — чтобы переключение EN/RU (F2) работало корректно.
    /// </summary>
    public class SceneTranslator : MonoBehaviour
    {
        public static SceneTranslator Instance;

        // Оригинальный (английский) текст по экземпляру TextMesh — для отката на EN.
        private static readonly Dictionary<TextMesh, string> _originals = new Dictionary<TextMesh, string>();

        private float _timer = 0f;
        private const float INTERVAL = 2f;
        private int _runs = 0;
        private const int MAX_RUNS = 20; // ~40 сек активного скана, дальше — только по sceneLoaded

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
            // Сцена загрузилась — переведём чуть погодя (даём объектам инициализироваться).
            CancelInvoke(nameof(ScanNow));
            Invoke(nameof(ScanNow), 1f);
            Invoke(nameof(ScanNow), 3f);
            _runs = 0; // продолжаем активный скан после смены сцены
        }

        private void Update()
        {
            if (_runs >= MAX_RUNS) return;
            _timer += Time.deltaTime;
            if (_timer < INTERVAL) return;
            _timer = 0f;
            ScanNow();
        }

        /// <summary>
        /// Вызывается извне (LangToggle) после переключения языка.
        /// </summary>
        public static void OnLanguageChanged()
        {
            Instance?.ScanNow();
        }

        public void ScanNow()
        {
            _runs++;
            try
            {
                bool ru = Plugin.CfgLanguage != null && Plugin.CfgLanguage.Value == "ru";
                var meshes = FindObjectsOfType<TextMesh>();
                int translated = 0, restored = 0;

                foreach (var tm in meshes)
                {
                    if (tm == null) continue;

                    // Шрифт — всегда (если RU), чтобы кириллица рисовалась.
                    if (ru && Plugin.FontResolver != null)
                        Plugin.FontResolver.ApplyTo(tm);

                    string cur = tm.text;
                    if (string.IsNullOrEmpty(cur)) continue;

                    if (ru)
                    {
                        // Запоминаем оригинал (только английский — не перезаписываем русским).
                        if (!_originals.ContainsKey(tm) && !ContainsCyrillic(cur))
                            _originals[tm] = cur;

                        if (ContainsCyrillic(cur)) continue; // уже переведено

                        var ruText = Plugin.Manager?.Get(cur);
                        if (!string.IsNullOrEmpty(ruText) && ruText != cur)
                        {
                            tm.text = ruText;
                            translated++;
                        }
                        else if (Plugin.CfgDumpUntranslated != null && Plugin.CfgDumpUntranslated.Value)
                        {
                            Plugin.Manager?.DumpUntranslated(cur);
                        }
                    }
                    else
                    {
                        // EN — восстанавливаем оригинал, если переводили ранее.
                        if (_originals.TryGetValue(tm, out var orig) && orig != cur)
                        {
                            tm.text = orig;
                            restored++;
                        }
                    }
                }

                if (_runs <= 6)
                    Plugin.Log?.LogInfo(
                        $"[SCAN] проход {_runs}: TextMesh={meshes.Length}, переведено={translated}, восстановлено={restored}");
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
    }
}
