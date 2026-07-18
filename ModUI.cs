using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SailwindTranslator
{
    /// <summary>
    /// UI мода: тёмный минимализм, вкладки, жёсткая изоляция ввода,
    /// выбор шрифта, переназначение клавиш, ПЕРЕТАСКИВАНИЕ и RESIZE окна.
    ///
    /// Архитектура окна (v1.3.3): чёткие НЕпересекающиеся регионы.
    ///   Шапка        [0 .. titlebarH]               — вкладки + drag
    ///   Контент      [titlebarH+1 .. H-footerH-1]   — flexible, в GUI.BeginGroup (клиппинг)
    ///   Футер        [H-footerH .. H]               — слева до W-gripCol
    ///   Resize-ручка [W-gripSize-pad .. W] в углу   — отдельный регион
    /// </summary>
    public class ModUI : MonoBehaviour
    {
        public static ModUI Instance { get; private set; }

        private bool _visible;
        private Rect _window = new Rect(80, 80, 820, 620);
        private GUISkin _skin;
        private Font _uiFont;

        // Отдельный скролл на каждую вкладку.
        private Vector2 _scrollTrans, _scrollSettings, _scrollFonts, _scrollControls;

        private enum Tab { Translations, Settings, Fonts, Controls }
        private Tab _tab = Tab.Translations;

        private string _search = "";
        private string _newKey = "";
        private string _newValue = "";
        private List<KeyValuePair<string, string>> _rows = new List<KeyValuePair<string, string>>();

        private string _rebindingAction = null;

        private FontManager _fonts;
        private float _fontScanTimer;

        // Текстуры скина — в полях + hideFlags (иначе IMGUI теряет фон).
        private Texture2D _texBg, _texBgAlt, _texBgHover, _texBgActive, _texAccent, _texDivider, _texTitlebar;
        private Texture2D _lineTex;

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

            _window.x = Mathf.Clamp(_window.x, 0, Screen.width - 80);
            _window.y = Mathf.Clamp(_window.y, 0, Screen.height - 60);

            _window = GUI.Window(987654, _window, DrawWindow, "");
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

        // ---------- Окно ----------

        private void DrawWindow(int id)
        {
            float W = _window.width;
            float H = _window.height;
            const float titlebarH = 34f;
            const float footerH = 38f;
            const float gripSize = 22f;
            const float gripPad = 4f;

            // === РЕГИОНЫ ===
            Rect titlebar = new Rect(0, 0, W, titlebarH);
            GUI.DrawTexture(titlebar, _texTitlebar);
            DrawTabs(titlebar);

            GUI.DrawTexture(new Rect(0, titlebarH, W, 1), _texDivider);

            float contentY = titlebarH + 1;
            float contentH = Mathf.Max(40f, H - titlebarH - 1 - footerH - 1);
            Rect content = new Rect(0, contentY, W, contentH);

            // Контент в GUI.BeginGroup — гарантированный клиппинг.
            GUI.BeginGroup(content);
            GUILayout.BeginArea(new Rect(0, 0, content.width, content.height));
            switch (_tab)
            {
                case Tab.Translations: DrawTranslations(); break;
                case Tab.Settings:     DrawSettings(); break;
                case Tab.Fonts:         DrawFonts(); break;
                case Tab.Controls:      DrawControls(); break;
            }
            GUILayout.EndArea();
            GUI.EndGroup();

            GUI.DrawTexture(new Rect(0, H - footerH - 1, W, 1), _texDivider);

            // Футер — слева до W-gripSize-gripPad, не пересекает resize-ручку.
            Rect footer = new Rect(0, H - footerH, W - gripSize - gripPad, footerH);
            GUILayout.BeginArea(footer);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.Label(KeyLabel(Plugin.CfgEditorKey.Value) + " — закрыть", _skin.GetStyle("hint"));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Закрыть", GUILayout.Width(100), GUILayout.Height(28))) Toggle();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Resize-ручка — отдельный регион в углу.
            Rect grip = new Rect(W - gripSize - gripPad, H - gripSize - gripPad, gripSize, gripSize);
            GUI.DrawTexture(grip, _texBgActive);
            DrawResizeGrip(grip);
            ResizeLogic(grip);

            // Перетаскивание окна — по шапке.
            GUI.DragWindow(new Rect(0, 0, W, titlebarH));
        }

        private void DrawTabs(Rect titlebar)
        {
            // Адаптивная ширина вкладок — помещаются при любом размере окна.
            float budget = (titlebar.width - 134f) / 4f; // 134 — под статус справа
            float tabW = Mathf.Clamp(budget, 92f, 150f);
            float x = 6f;
            float y = 5f;
            float h = titlebar.height - 10f;
            DrawTabRect("Переводы",   new Rect(x, y, tabW, h), Tab.Translations); x += tabW + 2;
            DrawTabRect("Настройки",  new Rect(x, y, tabW, h), Tab.Settings);     x += tabW + 2;
            DrawTabRect("Шрифты",     new Rect(x, y, tabW, h), Tab.Fonts);        x += tabW + 2;
            DrawTabRect("Управление", new Rect(x, y, tabW, h), Tab.Controls);

            Rect status = new Rect(titlebar.width - 128f, y, 122f, h);
            GUI.Label(status, "RU ⇄ EN: " + (Plugin.CfgLanguage.Value == "ru" ? "RU" : "EN"),
                _skin.GetStyle("status"));
        }

        private void DrawTabRect(string label, Rect r, Tab tab)
        {
            bool active = _tab == tab;
            GUIStyle s = _skin.GetStyle(active ? "tab_active" : "tab");
            if (GUI.Button(r, label, s)) _tab = tab;
        }

        private void DrawResizeGrip(Rect r)
        {
            Color c = _skin.GetStyle("muted").normal.textColor;
            Texture2D tex = LineTex(c);
            GUI.DrawTexture(new Rect(r.x + 4, r.y + r.height - 6, r.width - 8, 1), tex);
            GUI.DrawTexture(new Rect(r.x + 8, r.y + r.height - 11, r.width - 14, 1), tex);
            GUI.DrawTexture(new Rect(r.x + 12, r.y + r.height - 16, r.width - 20, 1), tex);
        }

        private Texture2D LineTex(Color c)
        {
            if (_lineTex == null)
            {
                _lineTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _lineTex.hideFlags = HideFlags.HideAndDontSave;
                _lineTex.wrapMode = TextureWrapMode.Clamp;
                _lineTex.filterMode = FilterMode.Point;
            }
            _lineTex.SetPixel(0, 0, c);
            _lineTex.Apply();
            return _lineTex;
        }

        private void ResizeLogic(Rect handle)
        {
            var e = Event.current;
            if (e == null) return;
            if (e.type == EventType.MouseDown && handle.Contains(e.mousePosition)) _resizing = true;
            if (e.type == EventType.MouseUp) _resizing = false;
            if (_resizing && e.type == EventType.MouseDrag)
            {
                _window.width = Mathf.Clamp(_window.width + e.delta.x, MinW, Screen.width - _window.x);
                _window.height = Mathf.Clamp(_window.height + e.delta.y, MinH, Screen.height - _window.y);
                e.Use();
            }
        }
        private bool _resizing;
        private const float MinW = 560f;
        private const float MinH = 400f;

        // ---------- Вкладки ----------

        private void DrawTranslations()
        {
            float keyW = Mathf.Max(180f, _window.width * 0.36f);

            GUILayout.BeginHorizontal(_skin.box);
            GUILayout.Label("Поиск:", GUILayout.Width(60), GUILayout.Height(26));
            _search = GUILayout.TextField(_search, GUILayout.ExpandWidth(true), GUILayout.Height(26));
            if (GUILayout.Button("Обновить", GUILayout.Width(90), GUILayout.Height(26))) Refresh();
            if (GUILayout.Button("Сохранить", GUILayout.Width(90), GUILayout.Height(26))) Plugin.Manager?.Save();
            if (GUILayout.Button("Перечитать", GUILayout.Width(90), GUILayout.Height(26))) { Plugin.Manager?.Load(); Refresh(); }
            GUILayout.EndHorizontal();

            int count = Plugin.Manager?.Count ?? 0;
            GUILayout.Label("Всего: " + count + "   |   Показано: " + _rows.Count, _skin.GetStyle("muted"));

            _scrollTrans = GUILayout.BeginScrollView(_scrollTrans, _skin.box, GUILayout.ExpandHeight(true));
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                GUILayout.BeginHorizontal(_skin.box);
                GUILayout.Label(row.Key, _skin.GetStyle("key"), GUILayout.Width(keyW), GUILayout.Height(26));
                string nv = GUILayout.TextField(row.Value ?? "", GUILayout.ExpandWidth(true), GUILayout.Height(26));
                if (nv != (row.Value ?? ""))
                {
                    Plugin.Manager?.Set(row.Key, nv);
                    _rows[i] = new KeyValuePair<string, string>(row.Key, nv);
                }
                if (GUILayout.Button("×", GUILayout.Width(28), GUILayout.Height(26)))
                {
                    Plugin.Manager?.Remove(row.Key);
                    _rows.RemoveAt(i); i--;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal(_skin.box);
            GUILayout.Label("EN:", GUILayout.Width(40), GUILayout.Height(26));
            _newKey = GUILayout.TextField(_newKey, GUILayout.Width(keyW), GUILayout.Height(26));
            GUILayout.Label("RU:", GUILayout.Width(40), GUILayout.Height(26));
            _newValue = GUILayout.TextField(_newValue, GUILayout.ExpandWidth(true), GUILayout.Height(26));
            if (GUILayout.Button("+", GUILayout.Width(32), GUILayout.Height(26)))
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
            _scrollSettings = GUILayout.BeginScrollView(_scrollSettings, _skin.box, GUILayout.ExpandHeight(true));
            GUILayout.BeginVertical(_skin.box);
            Plugin.CfgEnableTranslation.Value = GUILayout.Toggle(Plugin.CfgEnableTranslation.Value, "  Перевод включён");
            Plugin.CfgLiveTranslate.Value     = GUILayout.Toggle(Plugin.CfgLiveTranslate.Value,     "  Онлайн-перевод незнакомых строк (нужен интернет)");
            Plugin.CfgDumpUntranslated.Value  = GUILayout.Toggle(Plugin.CfgDumpUntranslated.Value,  "  Записывать непереведённые строки в untranslated.csv");

            GUILayout.Space(10);
            GUILayout.Label("Потоки перевода: " + Plugin.CfgLiveWorkers.Value, _skin.GetStyle("muted"));
            Plugin.CfgLiveWorkers.Value = (int)GUILayout.HorizontalSlider(Plugin.CfgLiveWorkers.Value, 1, 8);

            GUILayout.Space(10);
            GUILayout.Label("Язык интерфейса игры:", _skin.GetStyle("muted"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(Plugin.CfgLanguage.Value == "ru", "Русский") && Plugin.CfgLanguage.Value != "ru")
            { Plugin.CfgLanguage.Value = "ru"; SceneTranslator.OnLanguageChanged(); }
            if (GUILayout.Toggle(Plugin.CfgLanguage.Value == "en", "English") && Plugin.CfgLanguage.Value != "en")
            { Plugin.CfgLanguage.Value = "en"; SceneTranslator.OnLanguageChanged(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(14);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Сохранить конфиг", GUILayout.Width(170), GUILayout.Height(28)))
                Plugin.Instance.Config.Save();
            if (GUILayout.Button("Очистить кеш переводов", GUILayout.Width(210), GUILayout.Height(28)))
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
            GUILayout.EndScrollView();
        }

        private void DrawFonts()
        {
            _fontScanTimer += Time.deltaTime;
            if (_fontScanTimer > 1f) { _fontScanTimer = 0; if (_fonts.FolderChanged()) _fonts.Scan(); }

            GUILayout.BeginVertical(_skin.box, GUILayout.ExpandHeight(true));
            GUILayout.Label("Шрифт перевода (применяется на лету)", _skin.GetStyle("muted"));
            GUILayout.Label("Положи .ttf в BepInEx/plugins/SailwindTranslator/ и нажми «Пересканировать».", _skin.GetStyle("hint"));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Пересканировать папку", GUILayout.Width(210), GUILayout.Height(26))) _fonts.Scan();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Найдено: " + _fonts.Entries.Count, _skin.GetStyle("muted"));
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            _scrollFonts = GUILayout.BeginScrollView(_scrollFonts, _skin.box, GUILayout.ExpandHeight(true));
            string current = Plugin.CfgGameFont.Value;
            foreach (var fe in _fonts.Entries)
            {
                bool selected = fe.Id == current;
                GUIStyle row = _skin.GetStyle(selected ? "fontrow_active" : "fontrow");
                GUILayout.BeginHorizontal(row);
                GUILayout.Label(selected ? "●" : "○", _skin.GetStyle(selected ? "accent" : "muted"), GUILayout.Width(22), GUILayout.Height(28));
                GUILayout.Label(fe.DisplayName, GUILayout.ExpandWidth(true), GUILayout.Height(28));
                GUILayout.Label(fe.Source, _skin.GetStyle("muted"), GUILayout.Width(76), GUILayout.Height(28));
                if (!selected && GUILayout.Button("Выбрать", GUILayout.Width(100), GUILayout.Height(26)))
                {
                    if (_fonts.Apply(fe.Id)) SceneTranslator.OnLanguageChanged();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawControls()
        {
            _scrollControls = GUILayout.BeginScrollView(_scrollControls, _skin.box, GUILayout.ExpandHeight(true));
            GUILayout.BeginVertical(_skin.box);
            GUILayout.Label("Переназначение клавиш. Нажми «Изменить», затем клавишу.", _skin.GetStyle("muted"));
            GUILayout.Space(8);
            DrawKeyRow("Открыть/закрыть редактор", "editor", Plugin.CfgEditorKey.Value);
            GUILayout.Space(4);
            DrawKeyRow("Переключить RU/EN",         "toggle", Plugin.CfgToggleKey.Value);
            GUILayout.Space(16);
            if (_rebindingAction != null)
                GUILayout.Label("Нажми любую клавишу… (Esc — отмена)", _skin.GetStyle("accent"));
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private void DrawKeyRow(string label, string action, KeyCode current)
        {
            GUILayout.BeginHorizontal(_skin.box);
            GUILayout.Label(label, GUILayout.ExpandWidth(true), GUILayout.Height(30));
            GUILayout.Label(KeyLabel(current), _skin.GetStyle("key"), GUILayout.Width(110), GUILayout.Height(30));
            bool rebinding = _rebindingAction == action;
            if (GUILayout.Button(rebinding ? "Отмена" : "Изменить", GUILayout.Width(100), GUILayout.Height(28)))
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

        // ---------- Helpers ----------

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
            Color titlebar = HexColor("161920");

            _texBg       = MakeTex(bg);
            _texBgAlt    = MakeTex(bgAlt);
            _texBgHover  = MakeTex(bgHover);
            _texBgActive = MakeTex(bgActive);
            _texAccent   = MakeTex(accent);
            _texDivider  = MakeTex(divider);
            _texTitlebar = MakeTex(titlebar);

            var box = new GUIStyle(GUI.skin.box);
            box.normal.background = _texBgAlt; box.normal.textColor = text;
            box.onNormal.background = _texBgAlt;
            _skin.box = box;

            var win = new GUIStyle(GUI.skin.window);
            win.normal.background = _texBg; win.normal.textColor = text;
            win.onNormal.background = _texBg; win.onNormal.textColor = text;
            win.padding = new RectOffset(0, 0, 0, 0);
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
                MakeStyle("tab", _texBgAlt, text, 13, new RectOffset(10,10,3,3)),
                MakeStyle("tab_active", _texAccent, HexColor("ffffff"), 13, new RectOffset(10,10,3,3)),
                MakeStyle("status", _texTitlebar, muted, 12, new RectOffset(8,8,4,4), TextAnchor.MiddleRight),
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
