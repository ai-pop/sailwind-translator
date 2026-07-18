using System;
using System.Reflection;
using UnityEngine;

namespace SailwindTranslator
{
    /// <summary>
    /// Управление курсором и изоляция ввода через ШТАТНЫЙ механизм игры.
    ///
    /// В игре есть MouseLook.ToggleMouseLookAndCursor(bool newState):
    ///   newState=true  → мышь захвачена (lockState=Locked, visible=false,
    ///                    GameState.inCursorMenu=false) — обычный геймплей.
    ///   newState=false → мышь свободна (visible=true, lockState=None,
    ///                    GameState.inCursorMenu=true) — игра игнорирует клики.
    ///
    /// ВАЖНО (v1.3.1): запоминаем, был ли курсор уже свободен ДО открытия UI
    /// (например, игрок в главном меню/настройках). Тогда при закрытии НЕ возвращаем
    /// мышь в игру — оставляем курсор видимым, как и было. Иначе после закрытия
    /// нашего UI курсор исчезал бы в меню, где он нужен.
    /// </summary>
    public static class MouseController
    {
        private static MethodInfo _toggleMethod;
        private static bool _cached;
        private static bool _wasInCursorMenu;  // состояние до открытия нашего UI
        private static bool _releasedByUs;     // мы ли отпускали курсор

        /// <summary>Отпустить курсор и изолировать игровой UI (открыть наш UI).</summary>
        public static void ReleaseForUI()
        {
            try
            {
                EnsureCached();
                _wasInCursorMenu = GetInCursorMenu();
                if (_wasInCursorMenu)
                {
                    // Уже в режиме курсора (меню/настройки) — ничего не делаем,
                    // курсор уже свободен, игровой UI уже молчит.
                    _releasedByUs = false;
                    Plugin.Log?.LogInfo("[MOUSE] уже в cursor-menu — курсор не трогаем.");
                    return;
                }
                // Были в игре — отпускаем.
                Toggle(false);
                _releasedByUs = true;
                Plugin.Log?.LogInfo("[MOUSE] курсор освобождён, игровой UI изолирован.");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("[MOUSE] ReleaseForUI: " + ex.Message);
            }
        }

        /// <summary>Вернуть мышь в исходное состояние (закрыть наш UI).</summary>
        public static void ReturnToGame()
        {
            try
            {
                // Возвращаем только если МЫ отпускали. Если игрок был в меню —
                // курсор остаётся свободным, как и было до нас.
                if (!_releasedByUs)
                {
                    Plugin.Log?.LogInfo("[MOUSE] курсор не трогаем (были в cursor-menu).");
                    return;
                }
                Toggle(true);
                _releasedByUs = false;
                Plugin.Log?.LogInfo("[MOUSE] мышь возвращена в игру.");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("[MOUSE] ReturnToGame: " + ex.Message);
            }
        }

        private static void Toggle(bool toGame)
        {
            EnsureCached();
            if (_toggleMethod != null)
            {
                _toggleMethod.Invoke(null, new object[] { toGame });
                return;
            }
            // Фоллбэк.
            if (toGame)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
                SetGameStateCursorMenu(false);
            }
            else
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                SetGameStateCursorMenu(true);
            }
        }

        private static bool GetInCursorMenu()
        {
            try
            {
                Type gs = FindType("GameState");
                if (gs == null) return false;
                var f = gs.GetField("inCursorMenu", BindingFlags.Public | BindingFlags.Static);
                if (f == null) return false;
                return Convert.ToBoolean(f.GetValue(null));
            }
            catch { return false; }
        }

        private static void SetGameStateCursorMenu(bool value)
        {
            try
            {
                Type gs = FindType("GameState");
                if (gs == null) return;
                var f = gs.GetField("inCursorMenu", BindingFlags.Public | BindingFlags.Static);
                f?.SetValue(null, value);
            }
            catch { }
        }

        private static void EnsureCached()
        {
            if (_cached) return;
            _cached = true;
            try
            {
                Type mouseLook = FindType("MouseLook");
                if (mouseLook == null)
                {
                    Plugin.Log?.LogWarning("[MOUSE] тип MouseLook не найден.");
                    return;
                }
                _toggleMethod = mouseLook.GetMethod("ToggleMouseLookAndCursor",
                    BindingFlags.Public | BindingFlags.Static);
                if (_toggleMethod == null)
                    Plugin.Log?.LogWarning("[MOUSE] метод ToggleMouseLookAndCursor не найден.");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("[MOUSE] EnsureCached: " + ex.Message);
            }
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
