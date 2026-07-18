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
    /// Нужен ДИНАМИЧЕСКИЙ UnityEngine.Font с кириллицей: статический (предпечённый
    /// атлас) при подмене даёт "Font size and style overrides are only supported
    /// for dynamic fonts" и пустые кнопки. Динамический сам печёт глифы по запросу.
    ///
    /// Стратегия:
    ///   0. .ttf/.otf из папки плагина — создаём ДИНАМИЧЕСКИЙ шрифт по имени
    ///      семейства (читаем имя прямо из TTF).
    ///   1. Если не вышло — динамический шрифт из системного Arial/Segoe UI.
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
                    "в BepInEx/plugins/SailwindTranslator/.");
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
                        string fontName = ReadFontFamilyName(path) ?? Path.GetFileNameWithoutExtension(path);
                        Plugin.Log?.LogInfo("[FONT] внутреннее имя шрифта '" + fileName + "': '" + fontName + "'");

                        // Создаём ДИНАМИЧЕСКИЙ шрифт по имени семейства. Это критично:
                        // new Font(path) даёт статический шрифт (предпечённый атлас), который
                        // ломает TextMesh (пустые кнопки, warning про dynamic fonts).
                        Font font = Font.CreateDynamicFontFromOSFont(fontName, 16);
                        if (font != null)
                        {
                            try { font.RequestCharactersInTexture(CYRILLIC_SAMPLE, 32); } catch { }
                            _cyrFont = font;
                            Plugin.Log?.LogInfo("[FONT] '" + fileName + "' загружен как ДИНАМИЧЕСКИЙ шрифт. 🎉");
                            return;
                        }
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

        /// <summary>Читает Family Name (nameID=1) из TTF/OTF таблицы 'name'.</summary>
        private static string ReadFontFamilyName(string path)
        {
            try
            {
                byte[] data = File.ReadAllBytes(path);
                if (data.Length < 12) return null;
                int numTables = (data[4] << 8) | data[5];
                int nameOffset = -1;
                for (int i = 0; i < numTables; i++)
                {
                    int rec = 12 + i * 16;
                    if (rec + 12 > data.Length) break;
                    string tag = System.Text.Encoding.ASCII.GetString(data, rec, 4);
                    if (tag == "name")
                    {
                        nameOffset = (data[rec + 8] << 24) | (data[rec + 9] << 16) | (data[rec + 10] << 8) | data[rec + 11];
                        break;
                    }
                }
                if (nameOffset < 0 || nameOffset + 6 > data.Length) return null;

                int count = (data[nameOffset + 2] << 8) | data[nameOffset + 3];
                int stringOffset = (data[nameOffset + 4] << 8) | data[nameOffset + 5];

                for (int i = 0; i < count; i++)
                {
                    int rec = nameOffset + 6 + i * 12;
                    if (rec + 12 > data.Length) break;
                    int platformID = (data[rec] << 8) | data[rec + 1];
                    int nameID = (data[rec + 6] << 8) | data[rec + 7];
                    int length = (data[rec + 8] << 8) | data[rec + 9];
                    int offset = (data[rec + 10] << 8) | data[rec + 11];
                    if (nameID != 1) continue;

                    int strPos = nameOffset + stringOffset + offset;
                    if (strPos + length > data.Length) continue;
                    if (platformID == 3)
                        return System.Text.Encoding.BigEndianUnicode.GetString(data, strPos, length);
                    return System.Text.Encoding.ASCII.GetString(data, strPos, length);
                }
                return null;
            }
            catch { return null; }
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
                    Plugin.Log?.LogInfo("[FONT] создан динамический шрифт из OS:" + name + ". 🎉");
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning("[FONT] OS:" + name + ": " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Применить кириллический шрифт к TextMesh. Для TextMesh при смене font
        /// надо ещё перезаписать sharedMaterial у MeshRenderer, иначе текст не
        /// перерисуется.
        /// </summary>
        public void ApplyTo(TextMesh target)
        {
            if (target == null || _cyrFont == null) return;
            try
            {
                if (target.font != _cyrFont)
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

        private const string CYRILLIC_SAMPLE =
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
    }
}
