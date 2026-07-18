using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SailwindTranslator
{
    /// <summary>
    /// Переключатель языка RU/EN по CfgToggleKey (по умолчанию F2, переназначаемая).
    ///
    /// Проверка клавиши в OnGUI — Event.current валиден только там.
    /// При тогле дёргает SceneTranslator.OnLanguageChanged для обновления сцены.
    /// </summary>
    public class LangToggle : MonoBehaviour
    {
        private float _lastToggle = 0f;
        private const float TOGGLE_COOLDOWN = 0.3f;

        private void OnGUI()
        {
            var e = Event.current;
            if (e == null) return;
            if (e.type != EventType.KeyDown) return;

            KeyCode target = Plugin.CfgToggleKey != null ? Plugin.CfgToggleKey.Value : KeyCode.F2;
            if (e.keyCode != target) return;
            if (Time.realtimeSinceStartup - _lastToggle < TOGGLE_COOLDOWN) return;

            ToggleLanguage();
            _lastToggle = Time.realtimeSinceStartup;
        }

        private void ToggleLanguage()
        {
            if (Plugin.CfgLanguage == null) return;
            var cur = Plugin.CfgLanguage.Value;
            var next = cur == "ru" ? "en" : "ru";
            Plugin.CfgLanguage.Value = next;
            Plugin.Log?.LogInfo("Language toggled: " + cur + " → " + next);
            SceneTranslator.OnLanguageChanged();
        }
    }
}
