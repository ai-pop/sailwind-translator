using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TMPro;

namespace SailwindTranslator
{
    /// <summary>
    /// Подменяет/дополняет шрифты так, чтобы в TMP-тексте отображалась кириллица.
    ///
    /// Стратегия (по надёжности):
    ///   0. Загрузить .ttf/.otf из папки плагина (рядом с DLL) — самый надёжный источник кириллицы.
    ///   1. Найти среди загруженных в игре TMP_FontAsset тот, где уже есть кириллица.
    ///   2. Создать динамический TMP_FontAsset из системного шрифта (несколько имён).
    ///   3. Зарегистрировать найденный шрифт как ГЛОБАЛЬНЫЙ fallback TMP_Settings.
    ///   4. Дополнительно: каждому TMP-компоненту добавляем шрифт в fallbackFontAssetTable.
    ///
    /// Всё тяжёлое делается ЛЕНИВО (TryRegister вызывается и из ApplyTo, и из таймера),
    /// т.к. в момент Awake/Init (chainloader startup) шрифты игры ещё не загружены
    /// (лог показывает "0 TMP_FontAsset загружено" на старте — это нормально).
    /// </summary>
    public class FontCyrillicResolver
    {
        private TMP_FontAsset _cyrFont;
        private Font _cyrUiFont;
        private bool _globalRegistered;
        private bool _triedDisk;
        private bool _triedScan;

        public TMP_FontAsset CurrentFont => _cyrFont;

        public void Init()
        {
            // Не делаем ничего тяжёлого здесь — шрифты ещё не загружены.
            // Реальная работа в TryRegister(), которая зовётся лениво.
            Plugin.Log?.LogInfo("[FONT] resolver инициализирован (ленивый режим).");

            _cyrUiFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
        }

        /// <summary>
        /// Вызывать периодически и из ApplyTo. Потокобезопасно через флаги (один поток — Unity main).
        /// </summary>
        public void TryRegister()
        {
            if (_cyrFont != null)
            {
                EnsureGlobalFallback();
                return;
            }

            if (!_triedDisk) { _triedDisk = true; TryLoadFromDisk(); }
            if (_cyrFont == null && !_triedScan) { _triedScan = true; TryScanLoaded(); }
            if (_cyrFont == null) { TryCreateFromOsFonts(); }

            EnsureGlobalFallback();
        }

        private void TryLoadFromDisk()
        {
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!Directory.Exists(dir))
                {
                    Plugin.Log?.LogWarning($"[FONT] папка плагина не найдена: {dir}");
                    return;
                }
                var files = Directory.GetFiles(dir, "*.*")
                    .Where(p => p.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                                p.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                Plugin.Log?.LogInfo($"[FONT] найдено файлов шрифтов (.ttf/.otf): {files.Count} в {dir}");
                foreach (var path in files)
                {
                    if (_cyrFont != null) break;
                    TryLoadOne(path);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[FONT] ошибка чтения шрифтов с диска: {ex.Message}");
            }
        }

        private void TryLoadOne(string path)
        {
            string name = Path.GetFileName(path);
            try
            {
                var font = new Font(path);
                if (font == null)
                {
                    Plugin.Log?.LogWarning($"[FONT] '{name}': new Font() -> null");
                    return;
                }
                // Попросим движок загрузить кириллицу в текстуру, иначе CreateFontAsset может дать null
                try { font.RequestCharactersInTexture(CYRILLIC_SAMPLE, 32); } catch { }

                var asset = CreateCyrillicFontAsset(font, name);
                if (asset != null)
                {
                    _cyrFont = asset;
                    Plugin.Log?.LogInfo($"[FONT] '{name}' ЗАГРУЖЕН как кириллический шрифт.");
                }
                else
                {
                    Plugin.Log?.LogWarning($"[FONT] '{name}': CreateFontAsset вернул null (все оверлоады).");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[FONT] '{name}' не загрузился: {ex.Message}");
            }
        }

        private void TryScanLoaded()
        {
            try
            {
                var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                Plugin.Log?.LogInfo($"[FONT] загружено TMP_FontAsset: {all.Length}. Ищем с кириллицей...");
                foreach (var f in all)
                {
                    if (f == null || f.name == null) continue;
                    try
                    {
                        if (f.HasCharacter('А') || f.HasCharacter('а') || f.HasCharacter('р'))
                        {
                            _cyrFont = f;
                            Plugin.Log?.LogInfo($"[FONT] Найден шрифт с кириллицей: '{f.name}'.");
                            return;
                        }
                    }
                    catch { }
                }
                // Шрифтов с кириллицей нет, но хотя бы запомним имена для диагностики
                var names = all.Where(x => x != null && x.name != null).Select(x => x.name).Take(10).ToList();
                Plugin.Log?.LogInfo($"[FONT] ни один не содержит кириллицу. Имена: " +
                                    (names.Count == 0 ? "(пусто)" : string.Join(", ", names)));
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[FONT] ошибка сканирования: {ex.Message}");
            }
        }

        private void TryCreateFromOsFonts()
        {
            string[] candidates = { "Arial", "Segoe UI", "Tahoma", "Microsoft Sans Serif", "Verdana", "DejaVu Sans" };
            foreach (var name in candidates)
            {
                if (_cyrFont != null) break;
                try
                {
                    var osFont = Font.CreateDynamicFontFromOSFont(name, 16);
                    if (osFont == null) continue;
                    try { osFont.RequestCharactersInTexture(CYRILLIC_SAMPLE, 32); } catch { }
                    var asset = CreateCyrillicFontAsset(osFont, "OS:" + name);
                    Plugin.Log?.LogInfo($"[FONT] CreateFontAsset(OS:{name}) -> {(asset == null ? "null" : asset.name)}");
                    if (asset != null) _cyrFont = asset;
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"[FONT] OS:{name} упало: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Пробует несколько оверлоадов CreateFontAsset (разные версии TMP).
        /// </summary>
        private TMP_FontAsset CreateCyrillicFontAsset(Font font, string label)
        {
            Type t = typeof(TMP_FontAsset);
            // 1) CreateFontAsset(Font)
            try
            {
                var m = t.GetMethod("CreateFontAsset", new[] { typeof(Font) });
                if (m != null)
                {
                    var r = m.Invoke(null, new object[] { font }) as TMP_FontAsset;
                    if (r != null) return r;
                }
            }
            catch { }
            // 2) CreateFontAsset(Font, int, int)  [samplingPointSize, atlasPadding]
            try
            {
                var m = t.GetMethod("CreateFontAsset", new[] { typeof(Font), typeof(int), typeof(int) });
                if (m != null)
                {
                    var r = m.Invoke(null, new object[] { font, 90, 9 }) as TMP_FontAsset;
                    if (r != null) return r;
                }
            }
            catch { }
            // 3) CreateFontAsset(Font, int, int, int, int) и подобные с 4-мя интовыми
            try
            {
                var m = t.GetMethod("CreateFontAsset", new[] { typeof(Font), typeof(int), typeof(int), typeof(int), typeof(int) });
                if (m != null)
                {
                    var r = m.Invoke(null, new object[] { font, 90, 9, 0, 0 }) as TMP_FontAsset;
                    if (r != null) return r;
                }
            }
            catch { }
            return null;
        }

        private void EnsureGlobalFallback()
        {
            if (_cyrFont == null || _globalRegistered) return;
            try
            {
                Type t = FindType("TMPro.TMP_Settings");
                if (t == null)
                {
                    Plugin.Log?.LogWarning("[FONT] TMP_Settings не найден — глобальный fallback пропущен.");
                    return;
                }
                object settings = GetStaticMember(t, "defaultAsset");
                if (settings == null)
                {
                    Plugin.Log?.LogWarning("[FONT] TMP_Settings.defaultAsset == null (ещё не готово?).");
                    return;
                }
                object fbList = GetInstanceMember(settings, "fallbackFontAssets");
                if (fbList == null)
                {
                    Plugin.Log?.LogWarning("[FONT] fallbackFontAssets == null.");
                    return;
                }
                bool contains = (bool)fbList.GetType().GetMethod("Contains").Invoke(fbList, new object[] { _cyrFont });
                if (!contains)
                {
                    fbList.GetType().GetMethod("Add").Invoke(fbList, new object[] { _cyrFont });
                }
                _globalRegistered = true;
                Plugin.Log?.LogInfo($"[FONT] '{_cyrFont.name}' зарегистрирован как ГЛОБАЛЬНЫЙ TMP-fallback. 🎉");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[FONT] глобальный fallback не удался: {ex.Message}");
            }
        }

        public void ApplyTo(TMP_Text target)
        {
            if (target == null) return;
            TryRegister();
            if (_cyrFont == null) return;

            var cur = target.font;
            if (cur == null) { target.font = _cyrFont; return; }
            if (cur == _cyrFont) return;
            try
            {
                if (!cur.fallbackFontAssetTable.Contains(_cyrFont))
                    cur.fallbackFontAssetTable.Add(_cyrFont);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[FONT] ApplyTo(TMP) не удался: {ex.Message}");
            }
        }

        public void ApplyTo(UnityEngine.UI.Text target)
        {
            if (_cyrUiFont == null || target == null) return;
            if (target.font == _cyrUiFont) return;
            target.font = _cyrUiFont;
        }

        private const string CYRILLIC_SAMPLE =
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";

        // ---------- reflection-хелперы ----------

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { var t = asm.GetType(fullName); if (t != null) return t; } catch { }
            }
            return null;
        }

        private static object GetStaticMember(Type t, string name)
        {
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            if (p != null) return p.GetValue(null, null);
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            return f?.GetValue(null);
        }

        private static object GetInstanceMember(object obj, string name)
        {
            var t = obj.GetType();
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (p != null) return p.GetValue(obj, null);
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            return f?.GetValue(obj);
        }
    }
}
