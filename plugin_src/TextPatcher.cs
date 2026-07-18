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
    /// </summary>
    public static class TextPatcher
    {
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
            if (ru != null) return ru;

            // Нет перевода — дамп
            Plugin.Manager.DumpUntranslated(text);
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
            try
            {
                // Подменяем шрифт на кириллический при первом обращении
                if (value != null && Plugin.CfgLanguage.Value == "ru" && Plugin.FontResolver != null)
                {
                    Plugin.FontResolver.ApplyTo(__instance);
                }
                value = TryTranslate(value, __instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"TMP set_text prefix failed: {ex}");
            }
        }

        // TMP_Text также имеет метод SetText(string, ...) с params — патчим и его
        [HarmonyPatch(typeof(TMP_Text), "SetText", typeof(string))]
        [HarmonyPrefix]
        public static void TMP_Text_SetTextMethod_Prefix(ref string text)
        {
            try
            {
                text = TryTranslate(text, null);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"TMP SetText method prefix failed: {ex}");
            }
        }

        // У некоторых игр через SetCharArray / set_faceColor — обычный кейс, можно не патчить.
        // При необходимости добавить ещё здесь.

        // ===== Старый UnityEngine.UI.Text =====

        [HarmonyPatch(typeof(Text), nameof(Text.text), MethodType.Setter)]
        [HarmonyPrefix]
        public static void UI_Text_SetText_Prefix(ref string value, Text __instance)
        {
            try
            {
                if (value != null && Plugin.CfgLanguage.Value == "ru" && Plugin.FontResolver != null)
                {
                    Plugin.FontResolver.ApplyTo(__instance);
                }
                value = TryTranslate(value, __instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"UI.Text set_text prefix failed: {ex}");
            }
        }
    }
}
