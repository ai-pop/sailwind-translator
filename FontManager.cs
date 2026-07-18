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
                    font = new Font(entry.FilePath);
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

        private const string CYRILLIC_SAMPLE =
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
    }
}
