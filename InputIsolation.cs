using System;
using System.Reflection;
using HarmonyLib;

namespace SailwindTranslator
{
    /// <summary>
    /// Жёсткая изоляция ввода: пока наш UI открыт, патчим ключевые методы
    /// игры, чтобы они ничего не делали. Надёжнее флага GameState.inCursorMenu,
    /// потому что НЕ все игровые компоненты его проверяют.
    ///
    /// Патчим (Prefix, return):
    ///   GoPointer.DoRaycast           — наведение/выделение кнопок
    ///   GoPointer.MainButtonDown      — ЛКМ клик
    ///   GoPointer.AltButtonDown       — ПКМ клик
    ///   GoPointer.AltButtonHeld       — удержание ПКМ
    ///   MouseLook.Update              — вращение камерой мышью
    ///
    /// Все Prefix'ы проверяют ModUI.IsVisible и если UI открыт — return false
    /// (для void методов тоже работает, просто не пускает оригинал).
    /// </summary>
    public static class InputIsolation
    {
        public static void Patch(Harmony harmony)
        {
            try
            {
                PatchMethod(harmony, "GoPointer", "DoRaycast",     AccessTools.Method(typeof(InputIsolation), nameof(PrefixSkipVoid)));
                PatchMethod(harmony, "GoPointer", "MainButtonDown", AccessTools.Method(typeof(InputIsolation), nameof(PrefixSkipBool)));
                PatchMethod(harmony, "GoPointer", "AltButtonDown",  AccessTools.Method(typeof(InputIsolation), nameof(PrefixSkipBool)));
                PatchMethod(harmony, "GoPointer", "AltButtonHeld",  AccessTools.Method(typeof(InputIsolation), nameof(PrefixSkipBool)));
                PatchMethod(harmony, "MouseLook", "Update",         AccessTools.Method(typeof(InputIsolation), nameof(PrefixSkipVoid)));
                Plugin.Log?.LogInfo("[ISOLATE] Harmony-патчи ввода установлены.");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("[ISOLATE] Patch: " + ex.Message);
            }
        }

        private static void PatchMethod(Harmony harmony, string typeName, string methodName, MethodInfo prefix)
        {
            try
            {
                Type t = FindType(typeName);
                if (t == null)
                {
                    Plugin.Log?.LogWarning("[ISOLATE] тип " + typeName + " не найден.");
                    return;
                }
                var m = t.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (m == null)
                {
                    Plugin.Log?.LogWarning("[ISOLATE] метод " + typeName + "." + methodName + " не найден.");
                    return;
                }
                harmony.Patch(m, prefix: new HarmonyMethod(prefix));
                Plugin.Log?.LogInfo("[ISOLATE] патч на " + typeName + "." + methodName + " установлен.");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("[ISOLATE] PatchMethod " + typeName + "." + methodName + ": " + ex.Message);
            }
        }

        // Prefix для void-методов: если UI открыт — не выполнять оригинал.
        private static bool PrefixSkipVoid()
        {
            return !ModUI.IsVisible;
        }

        // Prefix для bool-методов: если UI открыт — возвращаем false.
        private static bool PrefixSkipBool(ref bool __result)
        {
            if (ModUI.IsVisible) { __result = false; return false; }
            return true;
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { var t = asm.GetType(fullName); if (t != null) return t; } catch { }
            }
            return null;
        }
    }
}
