using System;
using System.Collections;
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
    /// - FIT-TO-BOUNDS: если перевод шире оригинала, равномерно сжимаем
    ///   transform.localScale, чтобы он влез в РЕАЛЬНЫЙ прямоугольник оригинала
    ///   (Renderer.bounds — мировой bounding box, который движок уже посчитал).
    ///   Только сжатие, никогда растяжение. Якорь TextMesh работает за нас:
    ///   текст остаётся прижатым к своей точке привязки (кнопке).
    /// - Замер newBounds делается ОТЛОЖЕННО (корутина ждёт пару кадров), потому
    ///   что bounds пересчитывается лениво после смены текста.
    /// - EN/RU (F2) восстанавливает английский оригинал + оригинальный scale.
    /// </summary>
    public class SceneTranslator : MonoBehaviour
    {
        public static SceneTranslator Instance;

        // Английские оригиналы (для восстановления при EN).
        private static readonly Dictionary<TextMesh, string> _originals = new Dictionary<TextMesh, string>();
        // Оригинальный localScale (до нашего сжатия) — восстанавливаем при EN.
        private static readonly Dictionary<TextMesh, Vector3> _origScale = new Dictionary<TextMesh, Vector3>();
        // Оригинальная ширина bounds (world units) — целевая для фит-ту-баундс.
        private static readonly Dictionary<TextMesh, float> _origBoundsWidth = new Dictionary<TextMesh, float>();
        // TextMesh, для которых перевод только что применён и нужен фит-чек.
        private static readonly HashSet<TextMesh> _pendingFit = new HashSet<TextMesh>();

        private float _timer = 0f;
        private const float INTERVAL = 0.5f;
        private const float MIN_FIT = 0.5f;   // не сжимать сильнее, чем до 50%
        private const float EPSILON = 0.0001f;

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
                        // Запоминаем английский оригинал + оригинальный масштаб/ширину,
                        // но ТОЛЬКО пока ещё не переводили (текст ещё английский).
                        if (!_originals.ContainsKey(tm) && !ContainsCyrillic(cur))
                        {
                            _originals[tm] = cur;
                            CaptureOriginalMetrics(tm);
                        }

                        bool full, any;
                        string result = RichTextTranslator.Translate(cur, out full, out any);

                        if (any && result != cur)
                        {
                            tm.text = result;
                            // Планируем фит-ту-баундс на след. кадр (bounds пересчитается).
                            _pendingFit.Add(tm);
                            applied++;
                        }
                        if (!full) queued++;
                    }
                    else
                    {
                        if (_originals.TryGetValue(tm, out var orig) && orig != cur)
                        {
                            tm.text = orig;
                            // Возвращаем оригинальный масштаб.
                            if (_origScale.TryGetValue(tm, out var os))
                                tm.transform.localScale = os;
                            restored++;
                        }
                    }
                }

                // Запускаем фит для всех, кому он нужен (отложенный замер bounds).
                if (_pendingFit.Count > 0)
                    StartCoroutine(FitPendingNextFrame());

                Plugin.Log?.LogInfo("[SCAN] TextMesh=" + meshes.Length + ", применено=" + applied + ", в очередь=" + queued + ", восстановлено=" + restored);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("[SCAN] ошибка: " + ex.Message);
            }
        }

        /// <summary>
        /// Снимаем оригинальные метрики TextMesh (до перевода): localScale и ширину
        /// bounds в world units. Это «целевой прямоугольник» для последующего сжатия.
        /// </summary>
        private void CaptureOriginalMetrics(TextMesh tm)
        {
            try
            {
                _origScale[tm] = tm.transform.localScale;
                var b = GetBounds(tm);
                if (b.HasValue && b.Value.size.x > EPSILON)
                    _origBoundsWidth[tm] = b.Value.size.x;
            }
            catch { }
        }

        /// <summary>
        /// Корутина: ждём пару кадров, чтобы MeshRenderer пересчитал bounds под новый
        /// текст, затем фиттуем localScale так, чтобы ширина сравнялась с оригинальной.
        /// </summary>
        private IEnumerator FitPendingNextFrame()
        {
            // 2 кадра — с запасом на ленивый пересчёт TextMesh.
            yield return null;
            yield return null;

            var done = new List<TextMesh>();
            foreach (var tm in _pendingFit)
            {
                try
                {
                    if (tm == null) { done.Add(tm); continue; }
                    FitToBounds(tm);
                    done.Add(tm);
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError("[FIT] ошибка: " + ex.Message);
                    done.Add(tm);
                }
            }
            foreach (var d in done) _pendingFit.Remove(d);
        }

        /// <summary>
        /// Fit-to-bounds: если ширина перевода больше оригинальной — равномерно
        /// сжимаем localScale, чтобы новая ширина = оригинальной. Только сжатие,
        /// никогда растяжение. Якорь TextMesh сохраняет позицию точки привязки.
        /// </summary>
        private void FitToBounds(TextMesh tm)
        {
            if (!_origBoundsWidth.TryGetValue(tm, out float origW)) return;
            if (origW <= EPSILON) return;
            if (!_origScale.TryGetValue(tm, out Vector3 origScale)) return;

            var nb = GetBounds(tm);
            if (!nb.HasValue) return;
            float newW = nb.Value.size.x;
            if (newW <= EPSILON) return;

            // Сжимать нужно, только если перевод шире оригинала.
            if (newW <= origW * 1.02f) return; // 2% допуск — не дёргаем мелкие расхождения

            float fit = origW / newW;
            if (fit < MIN_FIT) fit = MIN_FIT; // не мельче 50%

            // Равномерный scale: форма букв сохраняется, позиция якоря — тоже.
            tm.transform.localScale = origScale * fit;
        }

        /// <summary>
        /// Безопасно получить Renderer.bounds у TextMesh (world-space bounding box).
        /// </summary>
        private static Bounds? GetBounds(TextMesh tm)
        {
            try
            {
                var r = tm.GetComponent<Renderer>();
                if (r == null) return null;
                Bounds b = r.bounds;
                if (b.size.x <= 0 || b.size.y <= 0) return null;
                return b;
            }
            catch { return null; }
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
