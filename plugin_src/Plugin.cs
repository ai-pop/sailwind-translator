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
        public const string VERSION = "1.2.4";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> CfgEnableTranslation;
        internal static ConfigEntry<bool> CfgAutoFit;
        internal static ConfigEntry<float> CfgAutoFitMinScale;
        internal static ConfigEntry<bool> CfgDumpUntranslated;
        internal static ConfigEntry<bool> CfgLiveTranslate;
        internal static ConfigEntry<int> CfgLiveWorkers;
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
            CfgDumpUntranslated    = Config.Bind("General", "DumpUntranslated", false, "Записывать непереведённые строки в untranslated.csv");
            CfgLiveTranslate       = Config.Bind("General", "LiveTranslate", true, "Переводить незнакомый текст онлайн в реальном времени (нужен интернет)");
            CfgLiveWorkers         = Config.Bind("General", "LiveWorkers", 4, "Кол-во фоновых потоков перевода (1-8). Больше = быстрее, но больше нагрузки на сеть.");
            CfgLanguage            = Config.Bind("General", "Language", "ru", "Активный язык: ru / en");

            Manager = new TranslationManager();
            Manager.Load();

            FontResolver = new FontCyrillicResolver();
            FontResolver.Init();

            // Harmony-патчи.
            // ОСНОВНОЙ патч — на UnityEngine.TextMesh.text (весь текст Sailwind идёт через него).
            // (TMP_Text/UI.Text в игре отсутствуют — сканер показал 0/0, поэтому отдельные патчи не нужны.)
            try
            {
                _harmony = new Harmony(GUID);
                _harmony.PatchAll(typeof(TextMeshPatcher));
                Log.LogInfo("Harmony: патчи применены (TextMesh + TMP + UI.Text).");
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Harmony PatchAll УПАЛ (патчи НЕ применены): {ex}");
            }

            // Компоненты
            gameObject.AddComponent<EditorMenu>();
            gameObject.AddComponent<LangToggle>();
            gameObject.AddComponent<FontAutoFit>();
            gameObject.AddComponent<SceneTranslator>();   // активный перевод зашитого текста

            // Живой переводчик: онлайн-перевод незнакомых строк в фоне + кеш.
            LiveTranslator.Start();

            Log.LogInfo(NAME + " v" + VERSION + " loaded. F3=editor, F2=toggle EN/RU. LiveTranslate=" + CfgLiveTranslate.Value);
        }

        private void OnDestroy()
        {
            LiveTranslator.Stop();
            _harmony?.UnpatchSelf();
            Manager?.Save();
        }
    }
}
