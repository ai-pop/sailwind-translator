// Минимальный stub UnityEngine.UI.Text для компиляции плагина.
// В рантайме игры реальная Unity UI подменит это.

using UnityEngine;

namespace UnityEngine.UI
{
    public class Text : MonoBehaviour
    {
        public virtual string text { get; set; }
        public Font font { get; set; }
        public int fontSize { get; set; }
    }
}
