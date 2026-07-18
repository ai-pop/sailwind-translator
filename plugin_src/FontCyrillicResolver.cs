using System;
using System.Collections.Generic;
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
    ///   1. Найти среди ЗАГРУЖЕННЫХ в игре TMP_FontAsset тот, у которого уже есть
    ///      кириллица (HasCharacter('А')) — часто такой есть (штатный или от AutoTranslator).
    ///   2. Если нет — создать динамический TMP_FontAsset из системного шрифта с кириллицей
    ///      (пробуем несколько имён: Arial, Segoe UI, Tahoma, ...).
    ///   3. Зарегистрировать найденный шрифт как ГЛОБАЛЬНЫЙ fallback TMP_Settings
    ///      (через reflection — чтобы не зависеть от версии/вида API TMP).
    ///      Тогда ЛЮБОЙ TMP_Text в игре возьмёт кириллицу из него автоматически.
    ///   4. Дополнительно: каждому компоненту, до которого дотягивается патч,
    ///      добавляем шрифт в fallbackFontAssetTable.
    ///
    /// Ленивая регистрация: шрифты могут грузиться поздно, поэтому сканируем
    /// повторно в EnsureRegistered() при первом ApplyTo().
    /// </summary>
    public class FontCyrillicResolver
    {
        private TMP_FontAsset _cyrFont;
        private Font _cyrUiFont;
        private bool _globalRegistered;
        private bool _scanned;

        public TMP_FontAsset CurrentFont => _cyrFont;

        public void Init()
        {
            ScanAndCreate();
            EnsureGlobalFallback();
            Report();

            _cyrUiFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
        }

        private void ScanAndCreate()
        {
            // 1. Сканируем все загруженные TMP_FontAsset.
            try
            {
                var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                Plugin.Log?.LogInfo($"[FONT] {all.Length} TMP_FontAsset(s) загружено.");
                foreach (var f in all)
                {
                    if (f == null || f.name == null) continue;
                    try
                    {
                        if (f.HasCharacter('А') || f.HasCharacter('а') || f.HasCharacter('р'))
                        {
                            _cyrFont = f;
                            Plugin.Log?.LogInfo($"[FONT] Найден шрифт с кириллицей: '{f.name}'.");
                            break;
                        }
                    }
                    catch { /* ignore per-font */ }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[FONT] Ошибка сканирования: {ex.Message}");
            }

            // 2. Fallback: создаём из системного шрифта.
            if (_cyrFont == null)
            {
                string[] candidates = { "Arial", "Segoe UI", "Tahoma", "Microsoft Sans Serif", "Verdana" };
                foreach (var name in candidates)
                {
                    try
                    {
                        var osFont = Font.CreateDynamicFontFromOSFont(name, 16);
                        if (osFont == null) continue;
                        var asset = TMP_FontAsset.CreateFontAsset(osFont);
                        Plugin.Log?.LogInfo($"[FONT] CreateFontAsset('{name}') -> {(asset == null ? "null" : asset.name)}");
                        if (asset != null)
                        {
                            _cyrFont = asset;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogWarning($"[FONT] CreateFontAsset('{name}') упало: {ex.Message}");
                    }
                }
            }

            _scanned = true;
        }

        private void EnsureGlobalFallback()
        {
            if (_cyrFont == null || _globalRegistered) return;
            try
            {
                // TMP_Settings.defaultAsset.fallbackFontAssets — через reflection,
                // чтобы не зависеть от того, поле это или свойство в данной версии TMP.
                Type t = FindType("TMPro.TMP_Settings");
                if (t == null)
                {
                    Plugin.Log?.LogWarning("[FONT] TMP_Settings не найден — глобальный fallback пропущен.");
                    return;
                }

                object settings = GetStaticMember(t, "defaultAsset");
                if (settings == null)
                {
                    Plugin.Log?.LogWarning("[FONT] TMP_Settings.defaultAsset == null.");
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
                Plugin.Log?.LogInfo($"[FONT] '{_cyrFont.name}' зарегистрирован как ГЛОБАЛЬНЫЙ TMP-fallback.");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[FONT] Глобальный fallback (reflection) не удался: {ex.Message}");
            }
        }

        private void Report()
        {
            if (_cyrFont == null)
            {
                Plugin.Log?.LogWarning(
                    "[FONT] Кириллический шрифт НЕ найден. Русский текст будет квадратиками. " +
                    "Положи .ttf/.otf с кириллицей рядом с плагином или включи OverrideFont в AutoTranslator.");
            }
        }

        /// <summary>
        /// Лениво: шрифты могли догрузиться позже Init(). Проверяем при первом ApplyTo.
        /// </summary>
        private void EnsureRegistered()
        {
            if (_cyrFont == null && !_scanned)
            {
                ScanAndCreate();
            }
            if (_cyrFont != null && !_globalRegistered)
            {
                EnsureGlobalFallback();
            }
        }

        /// <summary>
        /// Подменить/дополнить шрифт на конкретном TMP-компоненте.
        /// </summary>
        public void ApplyTo(TMP_Text target)
        {
            if (target == null) return;
            EnsureRegistered();
            if (_cyrFont == null) return;

            var cur = target.font;
            if (cur == null)
            {
                target.font = _cyrFont;
                return;
            }
            if (cur == _cyrFont) return;

            try
            {
                // Добавляем наш шрифт в fallback текущего — сохраняются все его настройки.
                if (!cur.fallbackFontAssetTable.Contains(_cyrFont))
                {
                    cur.fallbackFontAssetTable.Add(_cyrFont);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[FONT] ApplyTo(TMP) не удался: {ex.Message}");
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

        // ---------- reflection-хелперы ----------

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(fullName);
                    if (t != null) return t;
                }
                catch { }
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
