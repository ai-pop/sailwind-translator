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
    /// Sailwind рисует ВЕСЬ текст через UnityEngine.TextMesh (3D-текст).
    ///
    /// ВАЖНОЕ ЗНАНИЕ (ценой двух сломанных релизов):
    /// У игры СВОЙ шрифт, и в большинстве случаев ОН УЖЕ ПОДДЕРЖИВАЕТ кириллицу
    /// (он динамический). Подменять его на наш Arsenal НАДО ТОЛЬКО когда текущий
    /// шрифт реально не рисует кириллицу. Тотальное подменение ломает рендер
    /// (пустые кнопки, «Font size and style overrides...», исчезновение текста).
    ///
    /// Поэтому:
    ///   - грузим .ttf из папки через new Font(path) (это работало в v1.2.0);
    ///   - ApplyTo подменяет шрифт ТОЛЬКО если FontHasCyrillic(cur) == false.
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
                Plugin.Log?.LogInfo("[FONT] кириллический шрифт готов: '" + _cyrFont.name + "'.");
            }
            else
            {
                Plugin.Log?.LogWarning(
                    "[FONT] Кириллический шрифт НЕ создан. Положи .ttf/.otf с кириллицей " +
                    "в BepInEx/plugins/SailwindTranslator/. Текст подменяется только если " +
                    "у текущего шрифта нет кириллицы — обычно хватает и родного шрифта игры.");
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
                Plugin.Log?.LogInfo("[FONT] файлов шрифтов в папке плагина: " + files.Count);

                foreach (var path in files)
                {
                    string fileName = Path.GetFileName(path);
                    try
                    {
                        // Грузим через new Font(path) — это работало в v1.2.0.
                        // CreateDynamicFontFromOSFont тут НЕ подходит: он ищет шрифт среди
                        // установленных в Windows, а Arsenal лежит просто файлом.
                        var font = new Font(path);
                        if (font == null) continue;
                        try { font.RequestCharactersInTexture(CYRILLIC_SAMPLE, 32); } catch { }
                        _cyrFont = font;
                        Plugin.Log?.LogInfo("[FONT] '" + fileName + "' принят как запасной кириллический шрифт. 🎉");
                        return;
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogWarning("[FONT] '" + fileName + "' не загрузился: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("[FONT] чтение с диска: " + ex.Message);
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
                    _cyrFont = font;
                    Plugin.Log?.LogInfo("[FONT] создан динамический шрифт из OS:" + name + " (запасной).");
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning("[FONT] OS:" + name + ": " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Проверяет, рисует ли шрифт кириллицу. Для динамического шрифта это
        /// надёжно (глифы пекутся по запросу и сообщаются корректно).
        /// </summary>
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
        /// Подменяем шрифт:
        /// - если пользователь явно выбрал шрифт в UI (ForcedExternal) — ВСЕГДА;
        /// - иначе только если текущий НЕ рисует кириллицу (рабочая логика v1.2.0).
        /// </summary>
        public void ApplyTo(TextMesh target)
        {
            if (target == null || _cyrFont == null) return;
            try
            {
                var cur = target.font;
                if (cur == _cyrFont) return;

                bool needsSwap = _forcedExternal || cur == null || !FontHasCyrillic(cur);
                if (!needsSwap) return;

                target.font = _cyrFont;
                try { target.GetComponent<Renderer>().sharedMaterial = _cyrMaterial; } catch { }
                try { _cyrFont.RequestCharactersInTexture(CYRILLIC_SAMPLE, target.fontSize); } catch { }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("[FONT] ApplyTo(TextMesh) не удался: " + ex.Message);
            }
        }

        public void ApplyTo(UnityEngine.UI.Text target)
        {
            // UI.Text в игре нет (0 экземпляров).
        }

        /// <summary>
        /// Hot-reload: заменить активный шрифт на новый (вызывается из UI при выборе).
        /// Сразу переприменяется ко всем TextMesh при следующем проходе сканера.
        /// </summary>
        public void ReplaceFont(Font newFont)
        {
            if (newFont == null) return;
            _cyrFont = newFont;
            try { _cyrFont.RequestCharactersInTexture(CYRILLIC_SAMPLE, 32); } catch { }
            _cyrMaterial = _cyrFont.material;
            _forcedExternal = true; // пользователь явно выбрал шрифт — применяем ко всему
            Plugin.Log?.LogInfo("[FONT] шрифт заменён на '" + newFont.name + "' (принудительно).");
        }

        /// <summary>
        /// True, если пользователь явно выбрал шрифт через UI (FontManager.Apply).
        /// В этом режиме ApplyTo подменяет ВСЕ TextMesh, а не только без-кирилличные.
        /// </summary>
        public bool ForcedExternal => _forcedExternal;
        private bool _forcedExternal;

        /// <summary>
        /// Вернуть динамический Font для UI мода (OnGUI). Загружается из того же
        /// файла, что игровой, но как динамический (IMGUI требует динамический шрифт).
        /// Если включён отдельный UI-шрифт — грузится он.
        /// </summary>
        public Font GetFontForUi()
        {
            try
            {
                // Приоритет: отдельный UI-шрифт (CfgUiFont), иначе тот же что игровой.
                string uiFontId = Plugin.CfgUiFont != null ? Plugin.CfgUiFont.Value : "";
                if (!string.IsNullOrEmpty(uiFontId))
                {
                    Font f = LoadFontForUi(uiFontId);
                    if (f != null) return f;
                }
                // Иначе — клон игрового шрифта как динамический.
                if (_cyrFont != null)
                {
                    // Имя семейства — берём из имени загруженного Font.
                    string name = _cyrFont.name;
                    var dyn = Font.CreateDynamicFontFromOSFont(name, 14);
                    if (dyn != null) return dyn;
                }
                return Font.CreateDynamicFontFromOSFont("Arial", 14);
            }
            catch
            {
                return null;
            }
        }

        private static Font LoadFontForUi(string id)
        {
            try
            {
                if (id.StartsWith("os:"))
                {
                    return Font.CreateDynamicFontFromOSFont(id.Substring(3), 14);
                }
                if (id.StartsWith("disk:"))
                {
                    string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    string path = Path.Combine(dir, id.Substring(5));
                    if (File.Exists(path))
                    {
                        // Для UI — создаём динамический по имени (если оно установлено),
                        // иначе грузим как есть (IMGUI переживёт и статический, но не идеально).
                        return new Font(path);
                    }
                }
                return null;
            }
            catch { return null; }
        }

        private const string CYRILLIC_SAMPLE =
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
    }
}
