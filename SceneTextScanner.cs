using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using TMPro;

namespace SailwindTranslator
{
    /// <summary>
    /// Диагностический сканер сцен. Раз в пару секунд (первые ~40 сек после старта)
    /// ищет в сцене все текстовые компоненты и пишет в лог их реальные типы.
    /// Цель: понять, использует ли Sailwind TMP_Text / UI.Text или свой
    /// собственный класс (подсказка из лога: "key button ... doing UpdateText").
    /// </summary>
    public class SceneTextScanner : MonoBehaviour
    {
        private float _timer = 0f;
        private float _stopAt = 40f;
        private float _interval = 2f;
        private HashSet<string> _reportedTypes = new HashSet<string>();

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < _interval) return;
            _timer = 0f;

            Scan();

            if (_stopAt > 0 && Time.realtimeSinceStartup > _stopAt)
            {
                // последняя сводка и самоуничтожение
                LogSummary();
                Destroy(this);
            }
        }

        private void Scan()
        {
            try
            {
                int tmp = 0, ui = 0;
                try { tmp = FindObjectsOfType<TMP_Text>().Length; } catch { }
                try { ui = FindObjectsOfType<UnityEngine.UI.Text>().Length; } catch { }

                Plugin.Log?.LogInfo($"[SCAN] TMP_Text: {tmp}, UI.Text: {ui} (кадр {Time.frameCount})");

                // Перебираем все MonoBehaviour в сцене и собираем типы,
                // похожие на текст (имя содержит Text/Label/TMP/Field).
                var mbs = FindObjectsOfType<MonoBehaviour>();
                var interesting = new Dictionary<string, int>();
                foreach (var mb in mbs)
                {
                    if (mb == null) continue;
                    Type t = null;
                    try { t = mb.GetType(); } catch { continue; }
                    if (t == null) continue;
                    string n = t.Name;
                    if (n == null) continue;
                    if (n.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Label", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("TMP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("TextField", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string key = t.FullName;
                        if (!interesting.ContainsKey(key)) interesting[key] = 0;
                        interesting[key]++;
                    }
                }

                // Логируем только НОВЫЕ типы (чтобы не спамить), с примером публичного строкового свойства
                foreach (var kv in interesting)
                {
                    if (_reportedTypes.Add(kv.Key))
                    {
                        string sample = DescribeType(kv.Key);
                        Plugin.Log?.LogInfo(
                            $"[SCAN] тип текста: {kv.Key}  (экземпляров: {kv.Value}){sample}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[SCAN] ошибка: {ex.Message}");
            }
        }

        private static string DescribeType(string fullName)
        {
            Type t = FindType(fullName);
            if (t == null) return "  [тип не найден]";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine();
            sb.Append("        поля/свойства со строками: ");
            var members = new List<string>();
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (p.PropertyType == typeof(string)) members.Add(p.Name);
            }
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (f.FieldType == typeof(string)) members.Add(f.Name);
            }
            sb.Append(members.Count == 0 ? "(нет строковых)" : string.Join(", ", members.Distinct().Take(12)));
            // методы, похожие на setText
            var setMethods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                               .Where(m => (m.Name.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            m.Name.StartsWith("set_", StringComparison.Ordinal)) &&
                                           m.GetParameters().Length <= 1)
                               .Select(m => m.Name)
                               .Distinct().Take(12);
            sb.AppendLine();
            sb.Append("        текстовые методы: " + string.Join(", ", setMethods));
            return sb.ToString();
        }

        private void LogSummary()
        {
            Plugin.Log?.LogInfo($"[SCAN] итог. Найдено типов: {_reportedTypes.Count}. " +
                                "Если среди них нет TMPro.TMP_Text, значит Sailwind использует свой класс — " +
                                "пришли этот лог, добавлю патч на него.");
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
