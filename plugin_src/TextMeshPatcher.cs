using System;
using HarmonyLib;
using UnityEngine;

namespace SailwindTranslator
{
    /// <summary>
    /// ГЛАВНЫЙ патч перевода.
    ///
    /// Из декомпиляции Assembly-CSharp.dll: ВЕСЬ видимый текст в Sailwind
    /// проходит через UnityEngine.TextMesh.text setter:
    ///   - LookUI.ShowLookText(): extraText/controlsText/hintText
    ///       (lookText, description, хардкод "pick up"/"use"/"buy"...)
    ///   - StartMenuButton.SetButtonText(): GetComponent&lt;TextMesh&gt;().text
    ///   - GPButtonKeybinding.UpdateText(): text.text = keyCode.ToString()
    ///
    /// Поэтому один патч на TextMesh.text ловит всё. (TMP_Text и UI.Text в игре
    /// отсутствуют — сканер показал 0/0, отдельные патчи на них оставлены безвредно.)
    /// </summary>
    public static class TextMeshPatcher
    {
        private static bool _fired;

        private static string TryTranslate(string text, TextMesh instance)
        {
            if (text == null) return null;
            if (!_fired)
            {
                _fired = true;
                Plugin.Log?.LogInfo("[DIAG] ПАТЧ TextMesh.text СРАБОТАЛ — переводы теперь идут в игру.");
            }
            if (Plugin.CfgEnableTranslation == null || !Plugin.CfgEnableTranslation.Value) return text;
            if (Plugin.CfgLanguage == null || Plugin.CfgLanguage.Value != "ru") return text;

            // Hot-reload переводов
            if (Time.frameCount % 60 == 0) Plugin.Manager?.CheckHotReload();

            // Пустые/числовые не трогаем (коды клавиш, цены и т.п.)
            if (string.IsNullOrWhiteSpace(text)) return text;
            if (ContainsCyrillic(text)) return text;

            // Кириллический шрифт
            if (Plugin.FontResolver != null && instance != null)
                Plugin.FontResolver.ApplyTo(instance);

            var ru = Plugin.Manager.Get(text);
            if (ru != null)
            {
                return ru;
            }

            // Нет перевода в словаре — отдаём живому переводчику (переведёт онлайн в фоне,
            // закеширует, и при следующем кадре сканер подставит результат).
            LiveTranslator.Enqueue(text);
            return text;
        }

        private static bool ContainsCyrillic(string s)
        {
            foreach (var c in s)
                if ((c >= '\u0400' && c <= '\u04FF') || (c >= '\u0500' && c <= '\u052F')) return true;
            return false;
        }

        [HarmonyPatch(typeof(TextMesh), nameof(TextMesh.text), MethodType.Setter)]
        [HarmonyPrefix]
        public static void TextMesh_SetText_Prefix(ref string value, TextMesh __instance)
        {
            try { value = TryTranslate(value, __instance); }
            catch (Exception ex) { Plugin.Log?.LogError($"TextMesh set_text prefix failed: {ex}"); }
        }
    }
}
