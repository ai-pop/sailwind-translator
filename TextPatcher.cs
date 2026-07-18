using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SailwindTranslator
{
    /// <summary>
    /// Harmony-патчи на set_text у TMP_Text и UnityEngine.UI.Text.
    /// Любой вызов setText перехватывается, английская строка ищется в словаре,
    /// при совпадении подменяется на русский перевод.
    ///
    /// Диагностика: при первом срабатывании каждого патча пишем в лог —
    /// это подтверждает, что Harmony работает (в т.ч. при Supports SRE: False).
    /// </summary>
    public static class TextPatcher
    {
        // Одноразовые флаги — чтобы не спамить лог, но видеть, что патч сработал.
        private static bool _tmpSetterFired;
        private static bool _tmpSetTextFired;
        private static bool _uiSetterFired;
        private static int _matchedCount;
        private static int _unmatchedCount;

        private static string Trunc(string s)
        {
            if (s == null) return "<null>";
            return s.Length <= 60 ? s : s.Substring(0, 60) + "…";
        }

        private static string TryTranslate(string text, object instance)
        {
            if (text == null) return null;
            if (Plugin.CfgEnableTranslation == null || !Plugin.CfgEnableTranslation.Value) return text;
            if (Plugin.CfgLanguage == null || Plugin.CfgLanguage.Value != "ru") return text;

            // Hot-reload каждые ~1 сек
            if (Time.frameCount % 60 == 0)
            {
                Plugin.Manager?.CheckHotReload();
            }

            // Игнорируем чисто числовые/пустые
            if (string.IsNullOrWhiteSpace(text)) return text;

            // Если уже русский (есть кириллица) — не трогаем
            if (ContainsCyrillic(text)) return text;

            var ru = Plugin.Manager.Get(text);
            if (ru != null)
            {
                _matchedCount++;
                if (_matchedCount <= 3)
                    Plugin.Log?.LogInfo($"[DIAG] перевод применён: '{Trunc(text)}' -> '{Trunc(ru)}'");
                return ru;
            }

            // Нет перевода — дамп для последующего наполнения словаря
            _unmatchedCount++;
            Plugin.Manager?.DumpUntranslated(text);
            return text;
        }

        private static bool ContainsCyrillic(string s)
        {
            foreach (var c in s)
            {
                if ((c >= '\u0400' && c <= '\u04FF') || (c >= '\u0500' && c <= '\u052F'))
                    return true;
            }
            return false;
        }

        // ===== TextMeshPro (UGUI + 3D) =====

        [HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.text), MethodType.Setter)]
        [HarmonyPrefix]
        public static void TMP_Text_SetText_Prefix(ref string value, TMP_Text __instance)
        {
            if (!_tmpSetterFired)
            {
                _tmpSetterFired = true;
                Plugin.Log?.LogInfo("[DIAG] ПАТЧ СРАБОТАЛ: TMP_Text.text setter. Значит Harmony работает.");
            }
            try
            {
                // Подменяем шрифт на кириллический при первом обращении
                if (value != null && Plugin.CfgLanguage != null && Plugin.CfgLanguage.Value == "ru" && Plugin.FontResolver != null)
                {
                    Plugin.FontResolver.ApplyTo(__instance);
                }
                value = TryTranslate(value, __instance);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"TMP set_text prefix failed: {ex}");
            }
        }

        // TMP_Text также имеет метод SetText(string, ...) с params — патчим и его
        [HarmonyPatch(typeof(TMP_Text), "SetText", typeof(string))]
        [HarmonyPrefix]
        public static void TMP_Text_SetTextMethod_Prefix(ref string text)
        {
            if (!_tmpSetTextFired)
            {
                _tmpSetTextFired = true;
                Plugin.Log?.LogInfo("[DIAG] ПАТЧ СРАБОТАЛ: TMP_Text.SetText(string).");
            }
            try
            {
                text = TryTranslate(text, null);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"TMP SetText method prefix failed: {ex}");
            }
        }

        // ===== Старый UnityEngine.UI.Text =====

        [HarmonyPatch(typeof(Text), nameof(Text.text), MethodType.Setter)]
        [HarmonyPrefix]
        public static void UI_Text_SetText_Prefix(ref string value, Text __instance)
        {
            if (!_uiSetterFired)
            {
                _uiSetterFired = true;
                Plugin.Log?.LogInfo("[DIAG] ПАТЧ СРАБОТАЛ: UI.Text.text setter.");
            }
            try
            {
                if (value != null && Plugin.CfgLanguage != null && Plugin.CfgLanguage.Value == "ru" && Plugin.FontResolver != null)
                {
                    Plugin.FontResolver.ApplyTo(__instance);
                }
                value = TryTranslate(value, __instance);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"UI.Text set_text prefix failed: {ex}");
            }
        }
    }
}
