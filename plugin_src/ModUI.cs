using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SailwindTranslator
{
    /// <summary>
    /// UI мода: тёмный минимализм, вкладки, жёсткая изоляция ввода,
    /// выбор шрифта (как в OptiFine), переназначение клавиш.
    /// </summary>
    public class ModUI : MonoBehaviour
    {
        public static ModUI Instance { get; private set; }

        private bool _visible;
        private Rect _window = new Rect(60, 60, 760, 560);
        private Vector2 _scroll = Vector2.zero;
        private GUISkin _skin;
        private Font _uiFont;

        private enum Tab { Translations, Settings, Fonts, Controls }
        private Tab _tab = Tab.Translations;

        private string _search = "";
        private string _newKey = "";
        private string _newValue = "";
        private List<KeyValuePair<string, string>> _rows = new List<KeyValuePair<string, string>>();

        private string _rebindingAction = null;

        private FontManager _fonts;
        private Vector2 _fontScroll;
        private float _fontScanTimer;

        // Текстуры скина удерживаем в полях + hideFlags — иначе IMGUI теряет фон.
        private Texture2D _texBg, _texBgAlt, _texBgHover, _texBgActive, _texAccent, _texDivider;

        public static bool IsVisible => Instance != null && Instance._visible;

        public void Awake() { Instance = this; _fonts = new FontManager(); }
        public void Start() { _fonts.Scan(); }
        public void OnDestroy() { MouseController.ReturnToGame(); }

        private void OnGUI()
        {
            if (_skin == null) BuildSkin();

            var e = Event.current;
            if (e != null && e.type == EventType.KeyDown)
            {
                if (_rebindingAction != null && !IsModifierKey(e.keyCode))
                {
                    AssignRebind(e.keyCode);
                    return;
                }
                if (IsEditorKey(e.keyCode))
                {
                    Toggle();
                    e.Use();
                    return;
                }
            }

            if (!_visible) return;
            GUI.skin = _skin;
            GUI.Window(987654, _window, DrawWindow, "");
        }

        private void Toggle()
        {
            _visible = !_visible;
            if (_visible)
            {
                MouseController.ReleaseForUI();
                Refresh();
            }
            else
            {
                MouseController.ReturnToGame();
                _rebindingAction = null;
            }
        }

        // ---------- Окно и вкладки ----------

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal(GUILayout.Height(28));
            DrawTabButton("Переводы", Tab.Translations);
            DrawTabButton("Настройки", Tab.Settings);
            DrawTabButton("Шрифты", Tab.Fonts);
            DrawTabButton("Управление", Tab.Controls);
            GUILayout.FlexibleSpace();
            GUILayout.Label("RU ⇄ EN: " + (Plugin.CfgLanguage.Value == "ru" ? "RU" : "EN"), _skin.GetStyle("status"));
            GUILayout.EndHorizontal();

            GUILayout.Box("", _skin.GetStyle("divider"), GUILayout.ExpandWidth(true), GUILayout.Height(1));

            switch (_tab)
            {
                case Tab.Translations: DrawTranslations(); break;
                case Tab.Settings:     DrawSettings(); break;
                case Tab.Fonts:         DrawFonts(); break;
                case Tab.Controls:      DrawControls(); break;
            }

            GUILayout.FlexibleSpace();
            GUILayout.Box("", _skin.GetStyle("divider"), GUILayout.ExpandWidth(true), GUILayout.Height(1));
            GUILayout.BeginHorizontal();
            GUILayout.Label(KeyLabel(Plugin.CfgEditorKey.Value) + " — закрыть", _skin.GetStyle("hint"));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Закрыть", GUILayout.Width(80))) Toggle();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 28));
        }

        private void DrawTabButton(string label, Tab tab)
        {
            bool active = _tab == tab;
            GUIStyle s = _skin.GetStyle(active ? "tab_active" : "tab");
            if (GUILayout.Button(label, s, GUILayout.Height(24))) _tab = tab;
        }

        private void DrawTranslations()
        {
            GUILayout.BeginHorizontal(_skin.box);
            GUILayout.Label("Поиск:", GUILayout.Width(60));
            _search = GUILayout.TextField(_search, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Обновить", GUILayout.Width(90))) Refresh();
            if (GUILayout.Button("Сохранить", GUILayout.Width(90))) Plugin.Manager?.Save();
            if (GUILayout.Button("Перечитать", GUILayout.Width(90))) { Plugin.Manager?.Load(); Refresh(); }
            GUILayout.EndHorizontal();

            int count = Plugin.Manager?.Count ?? 0;
            GUILayout.Label("Всего: " + count + "   |   Показано: " + _rows.Count, _skin.GetStyle("muted"));

            _scroll = GUILayout.BeginScrollView(_scroll, _skin.box);
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                GUILayout.BeginHorizontal(_skin.box);
                GUILayout.Label(row.Key, _skin.GetStyle("key"), GUILayout.Width(280));
                string nv = GUILayout.TextField(row.Value ?? "", GUILayout.ExpandWidth(true));
                if (nv != (row.Value ?? ""))
                {
                    Plugin.Manager?.Set(row.Key, nv);
                    _rows[i] = new KeyValuePair<string, string>(row.Key, nv);
                }
                if (GUILayout.Button("×", GUILayout.Width(26)))
                {
                    Plugin.Manager?.Remove(row.Key);
                    _rows.RemoveAt(i); i--;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal(_skin.box);
            GUILayout.Label("EN:", GUILayout.Width(40));
            _newKey = GUILayout.TextField(_newKey, GUILayout.Width(260));
            GUILayout.Label("RU:", GUILayout.Width(40));
            _newValue = GUILayout.TextField(_newValue, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("+", GUILayout.Width(30)))
            {
                if (!string.IsNullOrWhiteSpace(_newKey))
                {
                    Plugin.Manager?.Set(_newKey, _newValue);
                    Plugin.Manager?.Save();
                    _newKey = ""; _newValue = ""; Refresh();
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSettings()
        {
            GUILayout.BeginVertical(_skin.box);
            Plugin.CfgEnableTranslation.Value = GUILayout.Toggle(Plugin.CfgEnableTranslation.Value, "  Перевод включён");
            Plugin.CfgLiveTranslate.Value     = GUILayout.Toggle(Plugin.CfgLiveTranslate.Value,     "  Онлайн-перевод незнакомых строк (нужен интернет)");
            Plugin.CfgDumpUntranslated.Value  = GUILayout.Toggle(Plugin.CfgDumpUntranslated.Value,  "  Записывать непереведённые строки в untranslated.csv");

            GUILayout.Space(8);
            GUILayout.Label("Потоки перевода: " + Plugin.CfgLiveWorkers.Value, _skin.GetStyle("muted"));
            Plugin.CfgLiveWorkers.Value = (int)GUILayout.HorizontalSlider(Plugin.CfgLiveWorkers.Value, 1, 8);

            GUILayout.Space(8);
            GUILayout.Label("Язык интерфейса игры:", _skin.GetStyle("muted"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(Plugin.CfgLanguage.Value == "ru", "Русский") && Plugin.CfgLanguage.Value != "ru")
            { Plugin.CfgLanguage.Value = "ru"; SceneTranslator.OnLanguageChanged(); }
            if (GUILayout.Toggle(Plugin.CfgLanguage.Value == "en", "English") && Plugin.CfgLanguage.Value != "en")
            { Plugin.CfgLanguage.Value = "en"; SceneTranslator.OnLanguageChanged(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Сохранить конфиг", GUILayout.Width(160)))
                Plugin.Instance.Config.Save();
            if (GUILayout.Button("Очистить кеш переводов", GUILayout.Width(200)))
            {
                if (Plugin.Manager != null)
                {
                    foreach (var k in Plugin.Manager.All().Select(p => p.Key).ToList())
                        Plugin.Manager.Remove(k);
                    Plugin.Manager.Save();
                    Refresh();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawFonts()
        {
            _fontScanTimer += Time.deltaTime;
            if (_fontScanTimer > 1f) { _fontScanTimer = 0; if (_fonts.FolderChanged()) _fonts.Scan(); }

            GUILayout.Label("Шрифт перевода (применяется на лету)", _skin.GetStyle("muted"));
            GUILayout.Label("Положи .ttf в BepInEx/plugins/SailwindTranslator/ и нажми «Пересканировать».", _skin.GetStyle("hint"));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Пересканировать папку", GUILayout.Width(200))) _fonts.Scan();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Найдено: " + _fonts.Entries.Count, _skin.GetStyle("muted"));
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            _fontScroll = GUILayout.BeginScrollView(_fontScroll, _skin.box);
            string current = Plugin.CfgGameFont.Value;
            foreach (var fe in _fonts.Entries)
            {
                bool selected = fe.Id == current;
                GUIStyle row = _skin.GetStyle(selected ? "fontrow_active" : "fontrow");
                GUILayout.BeginHorizontal(row);
                GUILayout.Label(selected ? "●" : "○", _skin.GetStyle(selected ? "accent" : "muted"), GUILayout.Width(20));
                GUILayout.Label(fe.DisplayName, GUILayout.ExpandWidth(true));
                GUILayout.Label(fe.Source, _skin.GetStyle("muted"), GUILayout.Width(70));
                if (!selected && GUILayout.Button("Выбрать", GUILayout.Width(90)))
                {
                    if (_fonts.Apply(fe.Id)) SceneTranslator.OnLanguageChanged();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        private void DrawControls()
        {
            GUILayout.Label("Переназначение клавиш. Нажми «Изменить», затем клавишу.", _skin.GetStyle("muted"));
            DrawKeyRow("Открыть/закрыть редактор", "editor", Plugin.CfgEditorKey.Value);
            DrawKeyRow("Переключить RU/EN",         "toggle", Plugin.CfgToggleKey.Value);
            GUILayout.Space(12);
            if (_rebindingAction != null)
                GUILayout.Label("Нажми любую клавишу… (Esc — отмена)", _skin.GetStyle("accent"));
        }

        private void DrawKeyRow(string label, string action, KeyCode current)
        {
            GUILayout.BeginHorizontal(_skin.box);
            GUILayout.Label(label, GUILayout.Width(260));
            GUILayout.FlexibleSpace();
            GUILayout.Label(KeyLabel(current), _skin.GetStyle("key"), GUILayout.Width(100));
            bool rebinding = _rebindingAction == action;
            if (GUILayout.Button(rebinding ? "Отмена" : "Изменить", GUILayout.Width(90)))
                _rebindingAction = rebinding ? null : action;
            GUILayout.EndHorizontal();
        }

        private void AssignRebind(KeyCode key)
        {
            if (key == KeyCode.Escape) { _rebindingAction = null; return; }
            switch (_rebindingAction)
            {
                case "editor": Plugin.CfgEditorKey.Value = key; break;
                case "toggle": Plugin.CfgToggleKey.Value = key; break;
            }
            _rebindingAction = null;
            Plugin.Instance.Config.Save();
        }

        private bool IsEditorKey(KeyCode k)
        {
            KeyCode target = Plugin.CfgEditorKey != null ? Plugin.CfgEditorKey.Value : KeyCode.F3;
            return k == target;
        }

        private static bool IsModifierKey(KeyCode k)
        {
            return k == KeyCode.LeftShift || k == KeyCode.RightShift ||
                   k == KeyCode.LeftControl || k == KeyCode.RightControl ||
                   k == KeyCode.LeftAlt || k == KeyCode.RightAlt;
        }

        private static string KeyLabel(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.F2: return "F2";
                case KeyCode.F3: return "F3";
                case KeyCode.Space: return "Пробел";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "→";
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
            }
            return k.ToString();
        }

        private void Refresh()
        {
            if (Plugin.Manager != null) _rows = Plugin.Manager.Search(_search, 500);
        }

        // ---------- Скин ----------

        private void BuildSkin()
        {
            _uiFont = Plugin.FontResolver?.GetFontForUi() ?? Font.CreateDynamicFontFromOSFont("Arial", 14);

            _skin = ScriptableObject.CreateInstance<GUISkin>();
            _skin.font = _uiFont;
            _skin.hideFlags = HideFlags.HideAndDontSave;

            Color bg       = HexColor("1a1d22");
            Color bgAlt    = HexColor("21252b");
            Color bgHover  = HexColor("2a2f37");
            Color bgActive = HexColor("2f3540");
            Color text     = HexColor("c8ccd4");
            Color muted    = HexColor("6b7280");
            Color accent   = HexColor("5b9aa0");
            Color divider  = HexColor("2f3338");

            // Создаём ОДИН РАЗ и держим в полях (иначе GC убивает — фон пропадает).
            _texBg       = MakeTex(bg);
            _texBgAlt    = MakeTex(bgAlt);
            _texBgHover  = MakeTex(bgHover);
            _texBgActive = MakeTex(bgActive);
            _texAccent   = MakeTex(accent);
            _texDivider  = MakeTex(divider);

            // box — фон панелей
            var box = new GUIStyle(GUI.skin.box);
            box.normal.background = _texBgAlt; box.normal.textColor = text;
            box.onNormal.background = _texBgAlt;
            _skin.box = box;

            // window — фон всего окна (это был главный баг: прозрачное окно)
            var win = new GUIStyle(GUI.skin.window);
            win.normal.background = _texBg; win.normal.textColor = text;
            win.onNormal.background = _texBg; win.onNormal.textColor = text;
            win.padding = new RectOffset(10, 10, 10, 10);
            win.border = new RectOffset(2, 2, 2, 2);
            _skin.window = win;

            _skin.label = new GUIStyle(GUI.skin.label) { normal = { textColor = text }, fontSize = 13 };

            _skin.textField = new GUIStyle(GUI.skin.textField)
            {
                normal = { background = _texBg, textColor = text },
                focused = { background = _texBgHover, textColor = text },
                hover = { background = _texBgHover, textColor = text },
                padding = new RectOffset(6, 6, 4, 4),
                fontSize = 13,
                border = new RectOffset(2, 2, 2, 2)
            };

            _skin.button = new GUIStyle(GUI.skin.button)
            {
                normal = { background = _texBgAlt, textColor = text },
                hover = { background = _texBgHover, textColor = text },
                active = { background = _texBgActive, textColor = text },
                onNormal = { background = _texBgHover, textColor = text },
                padding = new RectOffset(10, 10, 5, 5),
                fontSize = 13,
                border = new RectOffset(2, 2, 2, 2)
            };

            _skin.toggle = new GUIStyle(GUI.skin.toggle)
            {
                normal = { textColor = text },
                hover = { textColor = text },
                onNormal = { textColor = text },
                fontSize = 13,
                padding = new RectOffset(4, 4, 4, 4)
            };

            _skin.horizontalSlider = new GUIStyle(GUI.skin.horizontalSlider)
            {
                normal = { background = _texBg, textColor = text },
                fixedHeight = 4
            };
            _skin.horizontalSliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb)
            {
                normal = { background = _texAccent },
                hover = { background = _texAccent },
                active = { background = _texAccent },
                fixedWidth = 12, fixedHeight = 12
            };

            _skin.scrollView = new GUIStyle(GUI.skin.scrollView)
            {
                normal = { background = _texBg },
                border = new RectOffset(2, 2, 2, 2)
            };

            _skin.customStyles = new GUIStyle[]
            {
                MakeStyle("tab", _texBgAlt, text, 13, new RectOffset(12,12,4,4)),
                MakeStyle("tab_active", _texAccent, HexColor("ffffff"), 13, new RectOffset(12,12,4,4)),
                MakeStyle("status", _texBg, muted, 12, new RectOffset(8,8,4,4), TextAnchor.MiddleRight),
                MakeStyle("hint", _texBgAlt, muted, 11, new RectOffset(2,2,2,2)),
                MakeStyle("muted", _texBgAlt, muted, 12, new RectOffset(2,2,2,2)),
                MakeStyle("accent", _texBgAlt, accent, 13, new RectOffset(2,2,2,2)),
                MakeStyle("key", _texBg, HexColor("9aa3ad"), 12, new RectOffset(6,6,3,3)),
                MakeStyle("divider", _texDivider, text, 1, new RectOffset(0,0,0,0)),
                MakeStyle("fontrow", _texBgAlt, text, 13, new RectOffset(8,8,6,6)),
                MakeStyle("fontrow_active", _texBgActive, HexColor("ffffff"), 13, new RectOffset(8,8,6,6)),
            };

            ApplyFont(_skin, _uiFont);
        }

        private static GUIStyle MakeStyle(string name, Texture2D bg, Color textColor, int fontSize, RectOffset padding, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var s = new GUIStyle
            {
                name = name,
                fontSize = fontSize,
                padding = padding,
                alignment = anchor,
                border = new RectOffset(2, 2, 2, 2)
            };
            s.normal.background = bg;
            s.normal.textColor = textColor;
            s.onNormal.background = bg;
            s.onNormal.textColor = textColor;
            s.hover.background = bg;
            s.hover.textColor = textColor;
            return s;
        }

        private static void ApplyFont(GUISkin skin, Font font)
        {
            if (font == null) return;
            skin.font = font;
            var fields = typeof(GUISkin).GetFields().Where(f => f.FieldType == typeof(GUIStyle));
            foreach (var f in fields)
            {
                var s = (GUIStyle)f.GetValue(skin);
                if (s != null) s.font = font;
            }
            if (skin.customStyles != null)
                foreach (var s in skin.customStyles)
                    if (s != null) s.font = font;
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = c;
            t.SetPixels(pixels);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Point;
            return t;
        }

        private static Color HexColor(string hex)
        {
            hex = hex.TrimStart('#');
            if (hex.Length != 6) return Color.white;
            int r = Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = Convert.ToInt32(hex.Substring(4, 2), 16);
            return new Color(r / 255f, g / 255f, b / 255f);
        }
    }
}
