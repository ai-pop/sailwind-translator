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
    ///                    GameState.inCursorMenu=true) — игра игнорирует клики
    ///                    и raycast (MainButtonDown/AltButtonDown/DoRaycast все
    ///                    проверяют !GameState.inCursorMenu).
    ///
    /// Вызываем через reflection — MouseLook в Assembly-CSharp, не в наших refs.
    /// </summary>
    public static class MouseController
    {
        private static MethodInfo _toggleMethod;
        private static bool _cached;

        /// <summary>Отпустить курсор и отключить реакцию игрового UI (открыть наш UI).</summary>
        public static void ReleaseForUI()
        {
            try
            {
                EnsureCached();
                if (_toggleMethod != null)
                {
                    _toggleMethod.Invoke(null, new object[] { false });
                    Plugin.Log?.LogInfo("[MOUSE] курсор освобождён, игровой UI изолирован.");
                }
                else
                {
                    // Фоллбэк — напрямую через Cursor + GameState.
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                    SetGameStateCursorMenu(true);
                    Plugin.Log?.LogWarning("[MOUSE] фоллбэк: прямой Cursor + GameState.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("[MOUSE] ReleaseForUI: " + ex.Message);
            }
        }

        /// <summary>Вернуть мышь в игровой режим (закрыть наш UI).</summary>
        public static void ReturnToGame()
        {
            try
            {
                EnsureCached();
                if (_toggleMethod != null)
                {
                    _toggleMethod.Invoke(null, new object[] { true });
                }
                else
                {
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    SetGameStateCursorMenu(false);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("[MOUSE] ReturnToGame: " + ex.Message);
            }
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
