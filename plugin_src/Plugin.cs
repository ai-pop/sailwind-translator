using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SailwindTranslator
{
    /// <summary>
    /// Точка входа плагина. Ставится как любой BepInEx-плагин: .dll кидается в BepInEx/plugins/.
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("gravydevsupreme.xunity.autotranslator", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "ru.sailwind.translator";
        public const string NAME = "Sailwind Translator";
        public const string VERSION = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> CfgEnableTranslation;
        internal static ConfigEntry<bool> CfgAutoFit;
        internal static ConfigEntry<float> CfgAutoFitMinScale;
        internal static ConfigEntry<bool> CfgDumpUntranslated;
        internal static ConfigEntry<string> CfgLanguage;

        internal static TranslationManager Manager;
        internal static FontCyrillicResolver FontResolver;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            // Конфиг в BepInEx/config/SailwindTranslator.cfg
            CfgEnableTranslation   = Config.Bind("General", "Enable", true, "Включить подмену текста");
            CfgAutoFit             = Config.Bind("General", "AutoFitFont", true, "Авто-уменьшение шрифта, если текст длиннее оригинала");
            CfgAutoFitMinScale     = Config.Bind("General", "AutoFitMinScale", 0.6f, "Минимальный множитель размера шрифта (0.5–1.0)");
            CfgDumpUntranslated    = Config.Bind("General", "DumpUntranslated", true, "Записывать непереведённые строки в untranslated.csv");
            CfgLanguage            = Config.Bind("General", "Language", "ru", "Активный язык: ru / en");

            Manager = new TranslationManager();
            Manager.Load();

            FontResolver = new FontCyrillicResolver();
            FontResolver.Init();

            // Harmony-патчи
            _harmony = new Harmony(GUID);
            _harmony.PatchAll(typeof(TextPatcher));

            // Компоненты
            gameObject.AddComponent<EditorMenu>();
            gameObject.AddComponent<LangToggle>();
            gameObject.AddComponent<FontAutoFit>();

            Log.LogInfo($"{NAME} v{VERSION} loaded. F1=editor, F2=toggle EN/RU, F3=reset cache.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            Manager?.Save();
        }
    }
}
