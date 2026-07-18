using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SailwindTranslator
{
    /// <summary>
    /// Управление кириллическими шрифтами с поддержкой выбора и hot-reload.
    ///
    /// Скан папки плагина на .ttf/.otf → список доступных шрифтов (как выбор
    /// шейдера в OptiFine). Игрок кликает нужный в UI → применяется на лету:
    /// переинициализируется FontCyrillicResolver и перепроходятся все TextMesh.
    ///
    /// Запасной путь — динамические системные шрифты (Arial, Segoe UI, ...),
    /// тоже попадают в список, чтобы всегда был рабочий выбор.
    /// </summary>
    public class FontManager
    {
        public struct FontEntry
        {
            public string Id;          // уникальный идентификатор ("disk:filename" или "os:Arial")
            public string DisplayName; // что показывать в UI
            public string Source;      // "disk" / "os"
            public string FilePath;    // путь к файлу для disk-шрифтов
        }

        private List<FontEntry> _entries = new List<FontEntry>();
        private DateTime _lastScan = DateTime.MinValue;

        /// <summary>Список найденных шрифтов (последний Scan).</summary>
        public IReadOnlyList<FontEntry> Entries => _entries;

        /// <summary>Пересканировать папку плагина + обновить список системных.</summary>
        public void Scan()
        {
            _entries.Clear();
            try
            {
                string dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir, "*.*")
                        .Where(p => p.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                                    p.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var path in files)
                    {
                        string fileName = Path.GetFileName(path);
                        _entries.Add(new FontEntry
                        {
                            Id = "disk:" + fileName,
                            DisplayName = fileName,
                            Source = "disk",
                            FilePath = path
                        });
                    }
                }
                // Системные — небольшой набор с кириллицей, как запасные.
                string[] osCandidates = { "Arial", "Segoe UI", "Tahoma", "Verdana", "Microsoft Sans Serif" };
                foreach (var name in osCandidates)
                {
                    _entries.Add(new FontEntry
                    {
                        Id = "os:" + name,
                        DisplayName = name + "  (системный)",
                        Source = "os",
                        FilePath = null
                    });
                }

                _lastScan = DateTime.UtcNow;
                Plugin.Log?.LogInfo("[FONT-MGR] просканировано: " + _entries.Count + " шрифтов (" +
                    _entries.Count(e => e.Source == "disk") + " из папки, " +
                    _entries.Count(e => e.Source == "os") + " системных).");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("[FONT-MGR] Scan: " + ex.Message);
            }
        }

        /// <summary>Применить выбранный шрифт на лету (без перезапуска игры).</summary>
        public bool Apply(string id)
        {
            try
            {
                var entry = _entries.FirstOrDefault(e => e.Id == id);
                if (entry.Id == null)
                {
                    Plugin.Log?.LogWarning("[FONT-MGR] шрифт не найден: " + id);
                    return false;
                }

                Font font = null;
                if (entry.Source == "disk")
                {
                    // СНАЧАЛА пробуем ДИНАМИЧЕСКИЙ шрифт по имени семейства (из TTF).
                    // new Font(path) даёт СТАТИЧЕСКИЙ шрифт (предпечённый атлас), и при
                    // подмене у TextMesh текст пропадает (нет глифов в атласе). Динамический
                    // же пекарит глифы по запросу, в любом размере, с кириллицей.
                    string familyName = ReadFontFamilyName(entry.FilePath);
                    if (!string.IsNullOrEmpty(familyName))
                    {
                        try { font = Font.CreateDynamicFontFromOSFont(familyName, 16); }
                        catch { font = null; }
                    }
                    // Запасной путь — new Font(path). Может оказаться статическим, но
                    // иногда работает (если шрифт совместим).
                    if (font == null)
                    {
                        font = new Font(entry.FilePath);
                    }
                    // Проверка: если загруженный шрифт не рисует кириллицу — откат.
                    if (font != null && !CanRenderCyrillic(font))
                    {
                        Plugin.Log?.LogWarning("[FONT-MGR] '" + entry.DisplayName + "' не рисует кириллицу — откат на системный Arial.");
                        font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                    }
                }
                else
                {
                    // os:Name → берём имя после "os:"
                    font = Font.CreateDynamicFontFromOSFont(entry.Id.Substring(3), 16);
                }
                if (font == null)
                {
                    Plugin.Log?.LogWarning("[FONT-MGR] не удалось загрузить: " + id);
                    return false;
                }

                try { font.RequestCharactersInTexture(CYRILLIC_SAMPLE, 32); } catch { }

                // Устанавливаем как выбранный шрифт и перепроходим сцену.
                Plugin.FontResolver?.ReplaceFont(font);
                Plugin.CfgGameFont.Value = id;

                Plugin.Log?.LogInfo("[FONT-MGR] применён шрифт: " + entry.DisplayName + " (hot-reload).");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("[FONT-MGR] Apply: " + ex.Message);
                return false;
            }
        }

        /// <summary>True, если папка изменилась с последнего Scan.</summary>
        public bool FolderChanged()
        {
            try
            {
                string dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (!Directory.Exists(dir)) return false;
                var wt = Directory.GetLastWriteTimeUtc(dir);
                return wt > _lastScan;
            }
            catch { return false; }
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

        /// <summary>Проверяет, умеет ли шрифт рендерить кириллицу.</summary>
        private static bool CanRenderCyrillic(Font f)
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

        private const string CYRILLIC_SAMPLE =
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
    }
}
