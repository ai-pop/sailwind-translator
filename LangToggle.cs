using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SailwindTranslator
{
    /// <summary>
    /// Переключатель языка на F2.
    /// Меняет значение CfgLanguage между "ru" и "en",
    /// затем перечитывает все тексты, чтобы UI обновился.
    /// </summary>
    public class LangToggle : MonoBehaviour
    {
        private float _lastToggle = 0f;
        private const float TOGGLE_COOLDOWN = 0.3f;

        private void Update()
        {
            // Используем IMGUI Event вместо Input (работает без InputLegacyModule)
            if (Event.current != null && Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.F2 && Time.realtimeSinceStartup - _lastToggle > TOGGLE_COOLDOWN)
                {
                    ToggleLanguage();
                    _lastToggle = Time.realtimeSinceStartup;
                }
            }
        }

        private void ToggleLanguage()
        {
            if (Plugin.CfgLanguage == null) return;
            
            var cur = Plugin.CfgLanguage.Value;
            var next = cur == "ru" ? "en" : "ru";
            Plugin.CfgLanguage.Value = next;
            Plugin.Log?.LogInfo($"Language toggled: {cur} → {next}");

            RefreshAllText();
        }

        private void RefreshAllText()
        {
            // TMP
            var tmp = FindObjectsOfType<TMP_Text>();
            foreach (var t in tmp)
            {
                if (t == null) continue;
                var cur = t.text;
                // Сначала пустая строка, потом обратно — форсирует set_text через наш патч
                t.text = "";
                t.text = cur;
            }

            // UI.Text
            var ui = FindObjectsOfType<Text>();
            foreach (var t in ui)
            {
                if (t == null) continue;
                var cur = t.text;
                t.text = "";
                t.text = cur;
            }
        }
    }
}
