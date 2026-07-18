using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SailwindTranslator
{
    /// <summary>
    /// Переключатель языка на F2.
    /// Меняет значение CfgLanguage между "ru" и "en",
    /// затем перечитывает все тексты, чтобы UI обновился.
    ///
    /// ВАЖНО: проверка клавиши делается в OnGUI(), а НЕ в Update().
    /// Event.current валиден ТОЛЬКО внутри OnGUI — в Update() он всегда null
    /// (старая версия из-за этого вообще не реагировала на F2).
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
            if (e.keyCode != KeyCode.F2) return;

            // Считаем нажатие «съеденным», чтобы GUI-контролы не дублировали
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
            Plugin.Log?.LogInfo($"Language toggled: {cur} → {next}");

            RefreshAllText();
        }

        private void RefreshAllText()
        {
            // TextMesh — основной тип текста в Sailwind. Перечитываем, чтобы
            // сеттер-патч отработал заново (для динамических строк).
            var meshes = FindObjectsOfType<TextMesh>();
            foreach (var t in meshes)
            {
                if (t == null) continue;
                var cur = t.text;
                t.text = "";
                t.text = cur;
            }

            // Сканер сцены переведёт/восстановит зашитый текст (меню и т.п.).
            SceneTranslator.OnLanguageChanged();

            // TMP / UI.Text — на случай, если где-то есть (в игре их нет, но безопасно).
            var tmp = FindObjectsOfType<TMP_Text>();
            foreach (var t in tmp)
            {
                if (t == null) continue;
                var cur = t.text;
                t.text = "";
                t.text = cur;
            }

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
