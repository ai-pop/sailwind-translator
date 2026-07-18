using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SailwindTranslator
{
    /// <summary>
    /// Кириллический шрифт ДЛЯ UnityEngine.TextMesh (НЕ TextMeshPro).
    ///
    /// Открытие из декомпиляции Assembly-CSharp.dll: Sailwind рисует ВЕСЬ текст
    /// через UnityEngine.TextMesh (3D-текст). TextMeshPro и UnityEngine.UI.Text
    /// в сцене отсутствуют (сканер показал 0/0). Поэтому:
    ///   - нужен обычный UnityEngine.Font с кириллицей;
    ///   - материал этого шрифта надо ставить на MeshRenderer компонента TextMesh.
    ///
    /// Источники кириллического шрифта (по приоритету):
    ///   0. .ttf/.otf из папки плагина.
    ///   1. Динамический шрифт из системного (Arial, Segoe UI, ...).
    /// </summary>
    public class FontCyrillicResolver
    {
        private Font _cyrFont;
        private Material _cyrMaterial;
        private bool _initialized;

        public Font CurrentFont => _cyrFont;

        public void Init()
        {
            if (_initialized) return;
            _initialized = true;

            TryLoadFromDisk();
            if (_cyrFont == null) TryCreateFromOs();

            if (_cyrFont != null)
            {
                try { _cyrFont.RequestCharactersInTexture(CYRILLIC_SAMPLE, 32); } catch { }
                _cyrMaterial = _cyrFont.material;
                Plugin.Log?.LogInfo($"[FONT] кириллический шрифт готов: '{_cyrFont.name}'. " +
                                    $"(для TextMesh: font + material установятся на компоненты)");
            }
            else
            {
                Plugin.Log?.LogWarning(
                    "[FONT] Кириллический шрифт НЕ создан. Положи .ttf/.otf с кириллицей " +
                    "в BepInEx/plugins/SailwindTranslator/. Русский будет квадратиками.");
            }
        }

        private void TryLoadFromDisk()
        {
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!Directory.Exists(dir)) return;
                var files = Directory.GetFiles(dir, "*.*")
                    .Where(p => p.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                                p.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                Plugin.Log?.LogInfo($"[FONT] файлов шрифтов в папке плагина: {files.Count}");
                foreach (var path in files)
                {
                    string name = Path.GetFileName(path);
                    try
                    {
                        var font = new Font(path);
                        if (font == null) continue;
                        try { font.RequestCharactersInTexture(CYRILLIC_SAMPLE, 32); } catch { }
                        if (FontHasCyrillic(font))
                        {
                            _cyrFont = font;
                            Plugin.Log?.LogInfo($"[FONT] '{name}' загружен как кириллический TextMesh-шрифт. 🎉");
                            return;
                        }
                        else
                        {
                            Plugin.Log?.LogWarning($"[FONT] '{name}': кириллица в нём не обнаружена.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogWarning($"[FONT] '{name}' не загрузился: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[FONT] чтение с диска: {ex.Message}");
            }
        }

        private void TryCreateFromOs()
        {
            string[] candidates = { "Arial", "Segoe UI", "Tahoma", "Microsoft Sans Serif", "Verdana", "DejaVu Sans" };
            foreach (var name in candidates)
            {
                if (_cyrFont != null) break;
                try
                {
                    var font = Font.CreateDynamicFontFromOSFont(name, 16);
                    if (font == null) continue;
                    try { font.RequestCharactersInTexture(CYRILLIC_SAMPLE, 32); } catch { }
                    if (FontHasCyrillic(font))
                    {
                        _cyrFont = font;
                        Plugin.Log?.LogInfo($"[FONT] создан динамический кириллический шрифт из OS:{name}. 🎉");
                    }
                    else
                    {
                        Plugin.Log?.LogInfo($"[FONT] OS:{name} не дал кириллицу.");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"[FONT] OS:{name}: {ex.Message}");
                }
            }
        }

        private static bool FontHasCyrillic(Font f)
        {
            if (f == null) return false;
            try
            {
                f.RequestCharactersInTexture("Ая0", 32);
                CharacterInfo info;
                return f.GetCharacterInfo('А', out info, 32) &&
                       f.GetCharacterInfo('я', out info, 32);
            }
            catch { return false; }
        }

        /// <summary>
        /// Применить кириллический шрифт к TextMesh. Важно: для TextMesh при смене
        /// font надо ещё перезаписать sharedMaterial у MeshRenderer, иначе текст
        /// не перерисуется.
        /// </summary>
        public void ApplyTo(TextMesh target)
        {
            if (target == null || _cyrFont == null) return;
            try
            {
                var cur = target.font;
                // Подменяем только если текущий шрифт реально не рисует кириллицу
                // (чтобы минимально ломать исходный вид текста).
                bool needsSwap = cur == null || !FontHasCyrillic(cur);
                if (!needsSwap) return;

                target.font = _cyrFont;
                try { target.GetComponent<Renderer>().sharedMaterial = _cyrMaterial; } catch { }
                try { _cyrFont.RequestCharactersInTexture(CYRILLIC_SAMPLE, target.fontSize); } catch { }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[FONT] ApplyTo(TextMesh) не удался: {ex.Message}");
            }
        }

        // Совместимость: старый UI.Text-путь (если где-то всё же попадётся).
        public void ApplyTo(UnityEngine.UI.Text target)
        {
            // UI.Text не используется в игре (0 экземпляров), пропускаем.
        }

        private const string CYRILLIC_SAMPLE =
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
    }
}
