using TMPro;
using UnityEngine;

namespace SailwindTranslator
{
    /// <summary>
    /// Авто-подгон размера шрифта TMP_Text.
    /// Если русский перевод длиннее английского оригинала, плагин уменьшает fontSize,
    /// чтобы текст влез в тот же UI-элемент без обрезки.
    /// </summary>
    public class FontAutoFit : MonoBehaviour
    {
        private float _checkTimer = 0f;

        private void Update()
        {
            if (!Plugin.CfgAutoFit.Value) return;
            if (Plugin.CfgLanguage.Value != "ru") return;

            // Проверяем не каждый кадр — каждые 0.5 сек
            _checkTimer += Time.deltaTime;
            if (_checkTimer < 0.5f) return;
            _checkTimer = 0f;

            var all = FindObjectsOfType<TMP_Text>();
            foreach (var t in all)
            {
                ProcessOne(t);
            }
        }

        private void ProcessOne(TMP_Text t)
        {
            if (t == null) return;
            if (t.fontSize <= 0) return;

            // Если у TMP уже включён AutoSize — не вмешиваемся
            if (t.enableAutoSizing) return;

            // ЕслиRectTransform нулевой, пропускаем
            var rt = t.rectTransform;
            if (rt == null || rt.rect.width <= 0 || rt.rect.height <= 0) return;

            // Форсируем обновление, чтобы получить актуальные метрики
            t.ForceMeshUpdate();

            // Если текст вылез за границы — уменьшаем
            var bounds = t.textBounds;
            var rect = rt.rect;
            if (bounds.size.x > rect.width * 0.98f || bounds.size.y > rect.height * 0.98f)
            {
                var scale = Plugin.CfgAutoFitMinScale.Value;
                var newSize = Mathf.Max(t.fontSize * 0.9f, t.fontSizeDefault * scale);
                if (newSize < t.fontSize)
                {
                    t.fontSize = newSize;
                }
            }
        }
    }
}
