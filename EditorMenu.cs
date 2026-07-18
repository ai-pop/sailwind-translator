using System.Collections.Generic;
using UnityEngine;

namespace SailwindTranslator
{
    /// <summary>
    /// Окно редактора перевода (IMGUI).
    /// Нажми F3 в игре чтобы открыть/закрыть.
    ///
    /// ВАЖНО: нажатие F3 ловится в OnGUI() через Event.current.
    /// В Update() это не работает (Event.current там всегда null).
    ///
    /// ВАЖНО: E намеренно НЕ используется — в Sailwind это кнопка
    /// взаимодействия (открыть дверь, взять предмет и т.п.),
    /// поэтому редактор вынесен на F3, чтобы не конфликтовать с геймплеем.
    /// </summary>
    public class EditorMenu : MonoBehaviour
    {
        private bool _visible = false;
        private Rect _window = new Rect(40, 40, 760, 560);
        private Vector2 _scroll = Vector2.zero;
        private string _search = "";
        private string _newKey = "";
        private string _newValue = "";

        // Cooldown, чтобы одно нажатие F3 не сработало дважды (Layout/KeyDown)
        private float _lastF3Time = 0f;
        private const float F3_COOLDOWN = 0.3f;

        // Кэш для отображения
        private List<KeyValuePair<string, string>> _rows = new List<KeyValuePair<string, string>>();

        private void OnGUI()
        {
            var e = Event.current;
            if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.F3
                && Time.realtimeSinceStartup - _lastF3Time > F3_COOLDOWN)
            {
                _visible = !_visible;
                _lastF3Time = Time.realtimeSinceStartup;
                if (_visible) Refresh();
            }

            if (!_visible) return;
            _window = GUI.Window(987654, _window, DrawWindow, "Sailwind Translator Editor — Press F3 to close");
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            // Toolbar
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("Search:", GUILayout.Width(50));
            _search = GUILayout.TextField(_search, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Refresh", GUILayout.Width(80))) Refresh();
            if (GUILayout.Button("Save", GUILayout.Width(60)))
            {
                Plugin.Manager.Save();
            }
            if (GUILayout.Button("Reload", GUILayout.Width(70)))
            {
                Plugin.Manager.Load();
                Refresh();
            }
            GUILayout.EndHorizontal();

            // Stats
            int count = Plugin.Manager?.Count ?? 0;
            string lang = Plugin.CfgLanguage?.Value ?? "en";
            GUILayout.Label($"Total: {count} | Lang: {lang} | Showing: {_rows.Count}",
                GUI.skin.box);

            // Список переводов
            _scroll = GUILayout.BeginScrollView(_scroll, GUI.skin.box);
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                GUILayout.BeginHorizontal(GUI.skin.box);

                GUILayout.Label(row.Key, GUILayout.Width(280), GUILayout.Height(40));
                var newVal = GUILayout.TextField(row.Value ?? "", GUILayout.ExpandWidth(true), GUILayout.Height(40));

                if (newVal != (row.Value ?? ""))
                {
                    Plugin.Manager.Set(row.Key, newVal);
                    _rows[i] = new KeyValuePair<string, string>(row.Key, newVal);
                }

                if (GUILayout.Button("X", GUILayout.Width(30), GUILayout.Height(40)))
                {
                    Plugin.Manager.Remove(row.Key);
                    _rows.RemoveAt(i);
                    i--;
                }

                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            // Добавление новой записи
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("EN:", GUILayout.Width(40));
            _newKey = GUILayout.TextField(_newKey, GUILayout.Width(260));
            GUILayout.Label("RU:", GUILayout.Width(40));
            _newValue = GUILayout.TextField(_newValue, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Add", GUILayout.Width(60)))
            {
                if (!string.IsNullOrWhiteSpace(_newKey))
                {
                    Plugin.Manager.Set(_newKey, _newValue);
                    Plugin.Manager.Save();
                    _newKey = "";
                    _newValue = "";
                    Refresh();
                }
            }
            GUILayout.EndHorizontal();

            // Подсказки
            GUILayout.Label("Press F3 to toggle menu. Changes apply instantly. Click Save to write to disk.",
                GUI.skin.box);

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void Refresh()
        {
            if (Plugin.Manager != null)
            {
                _rows = Plugin.Manager.Search(_search, 500);
            }
        }
    }
}
