using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SailwindTranslator
{
    /// <summary>
    /// Точка входа плагина. Ставится как любой BepInEx-плагин: .dll в BepInEx/plugins/.
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("gravydevsupreme.xunity.autotranslator", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "ru.sailwind.translator";
        public const string NAME = "Sailwind Translator";
        public const string VERSION = "1.3.1";

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        internal static ConfigEntry<bool> CfgEnableTranslation;
        internal static ConfigEntry<bool> CfgDumpUntranslated;
        internal static ConfigEntry<bool> CfgLiveTranslate;
        internal static ConfigEntry<int> CfgLiveWorkers;
        internal static ConfigEntry<string> CfgLanguage;

        // UI/Hotkeys/Fonts (v1.3.0)
        internal static ConfigEntry<KeyCode> CfgEditorKey;
        internal static ConfigEntry<KeyCode> CfgToggleKey;
        internal static ConfigEntry<string> CfgGameFont; // "disk:filename.ttf" или "os:Arial" или ""
        internal static ConfigEntry<string> CfgUiFont;   // отдельный шрифт для UI мода (или "")

        internal static TranslationManager Manager;
        internal static FontCyrillicResolver FontResolver;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            // Конфиг
            CfgEnableTranslation   = Config.Bind("General", "Enable", true, "Включить подмену текста");
            CfgDumpUntranslated    = Config.Bind("General", "DumpUntranslated", false, "Записывать непереведённые строки в untranslated.csv");
            CfgLiveTranslate       = Config.Bind("General", "LiveTranslate", true, "Онлайн-перевод незнакомых строк (нужен интернет)");
            CfgLiveWorkers         = Config.Bind("General", "LiveWorkers", 4, "Кол-во фоновых потоков перевода (1-8)");
            CfgLanguage            = Config.Bind("General", "Language", "ru", "Активный язык: ru / en");

            CfgEditorKey = Config.Bind("UI", "EditorKey", KeyCode.F3, "Клавиша открытия/закрытия редактора");
            CfgToggleKey = Config.Bind("UI", "ToggleKey", KeyCode.F2, "Клавиша переключения RU/EN");
            CfgGameFont  = Config.Bind("UI", "GameFont", "", "Шрифт игры (disk:filename.ttf / os:Arial / пусто = авто)");
            CfgUiFont    = Config.Bind("UI", "UiFont", "", "Шрифт UI мода (пусто = как у игры)");

            Manager = new TranslationManager();
            Manager.Load();

            FontResolver = new FontCyrillicResolver();
            FontResolver.Init();

            // Применяем выбранный шрифт из конфига, если задан.
            if (!string.IsNullOrEmpty(CfgGameFont.Value))
            {
                var fm = new FontManager();
                fm.Scan();
                fm.Apply(CfgGameFont.Value);
            }

            // Harmony-патч: перехват TextMesh.text setter.
            try
            {
                _harmony = new Harmony(GUID);
                _harmony.PatchAll(typeof(TextMeshPatcher));
                Log.LogInfo("Harmony: патчи применены (TextMesh).");
            }
            catch (System.Exception ex)
            {
                Log.LogError("Harmony PatchAll УПАЛ: " + ex);
            }

            // Компоненты
            gameObject.AddComponent<ModUI>();           // новый UI с вкладками
            gameObject.AddComponent<LangToggle>();       // F2 (переназначаемая) — RU/EN
            gameObject.AddComponent<SceneTranslator>();  // активный перевод текста

            LiveTranslator.Start();

            Log.LogInfo(NAME + " v" + VERSION + " loaded. " +
                KeyName(CfgEditorKey.Value) + "=editor, " + KeyName(CfgToggleKey.Value) + "=RU/EN, LiveTranslate=" + CfgLiveTranslate.Value);
        }

        private static string KeyName(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.F2: return "F2";
                case KeyCode.F3: return "F3";
            }
            return k.ToString();
        }

        private void OnDestroy()
        {
            LiveTranslator.Stop();
            _harmony?.UnpatchSelf();
            Manager?.Save();
        }
    }
}
