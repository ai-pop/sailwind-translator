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

            if (string.IsNullOrWhiteSpace(text)) return text;

            // Кириллический шрифт
            if (Plugin.FontResolver != null && instance != null)
                Plugin.FontResolver.ApplyTo(instance);

            // Переводим через RichTextTranslator — он разбивает по разделителям
            // (\t \n ...) и переводит сегменты по отдельности, сохраняя вёрстку.
            // Динамические строки вроде "pick up\nuse" переводятся как две фразы.
            bool full, any;
            string result = RichTextTranslator.Translate(text, out full, out any);
            return any ? result : text;
        }

        [HarmonyPatch(typeof(TextMesh), nameof(TextMesh.text), MethodType.Setter)]
        [HarmonyPrefix]
        public static void TextMesh_SetText_Prefix(ref string value, TextMesh __instance)
        {
            try { value = TryTranslate(value, __instance); }
            catch (Exception ex) { Plugin.Log?.LogError("TextMesh set_text prefix failed: " + ex); }
        }
    }
}
