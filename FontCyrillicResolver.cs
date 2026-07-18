using System.IO;
using TMPro;
using UnityEngine;

namespace SailwindTranslator
{
    /// <summary>
    /// Подменяет шрифты TMP_Text на кириллические.
    /// В Unity по умолчанию TMP шрифт LiberationSans не содержит кириллицы —
    /// русские символы превращаются в квадратики. Плагин ищет рядом с собой
    /// TMP_FontAsset "NotoSansSC" или "CyrillicDefault" и подменяет.
    /// </summary>
    public class FontCyrillicResolver
    {
        private TMP_FontAsset _cyrFont;
        private Font _cyrUiFont;

        public void Init()
        {
            // Пытаемся найти кириллический TMP шрифт в ресурсах игры
            var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var f in all)
            {
                if (f == null || f.name == null) continue;
                var n = f.name.ToLowerInvariant();
                if (n.Contains("cyrillic") || n.Contains("noto") || n.Contains("inter") ||
                    n.Contains("roboto") || n.Contains("arial"))
                {
                    _cyrFont = f;
                    Plugin.Log.LogInfo($"Cyrillic TMP font found: {f.name}");
                    break;
                }
            }

            // Fallback — создаём из системного Arial (есть на Windows всегда)
            if (_cyrFont == null)
            {
                var arial = Font.CreateDynamicFontFromOSFont("Arial", 16);
                if (arial != null)
                {
                    _cyrFont = TMP_FontAsset.CreateFontAsset(arial);
                    Plugin.Log.LogInfo($"Created fallback TMP font from OS Arial.");
                }
            }

            // UI.Text fallback
            _cyrUiFont = Font.CreateDynamicFontFromOSFont("Arial", 16);

            if (_cyrFont == null)
            {
                Plugin.Log.LogWarning("No cyrillic TMP font available. Russian text may show as squares.");
            }
        }

        /// <summary>
        /// Подменить шрифт на конкретном TMP-компоненте, если текущий не содержит кириллицу.
        /// </summary>
        public void ApplyTo(TMP_Text target)
        {
            if (_cyrFont == null || target == null) return;
            if (target.font == _cyrFont) return;

            // Проверяем, есть ли в текущем шрифте кириллица
            var cur = target.font;
            if (cur == null)
            {
                target.font = _cyrFont;
                return;
            }

            // У TMP_FontAsset есть fallbackFontAssetTable — добавим наш в fallback
            // (это лучше, чем полная замена — сохраняются настройки размера)
            bool has = cur.HasCharacter('А');
            if (!has)
            {
                if (!cur.fallbackFontAssetTable.Contains(_cyrFont))
                {
                    cur.fallbackFontAssetTable.Add(_cyrFont);
                }
            }
        }

        /// <summary>
        /// То же для старого UI.Text.
        /// </summary>
        public void ApplyTo(UnityEngine.UI.Text target)
        {
            if (_cyrUiFont == null || target == null) return;
            if (target.font == _cyrUiFont) return;
            target.font = _cyrUiFont;
        }
    }
}
