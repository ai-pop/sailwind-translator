// Минимальный stub TMPro для компиляции плагина.
// В рантайме игры реальная реализация TextMeshPro подменит это.
// Stub содержит только API, которое использует SailwindTranslator.

using System.Collections.Generic;
using UnityEngine;

namespace TMPro
{
    public class TMP_FontAsset : UnityEngine.Object
    {
        public new string name;
        public List<TMP_FontAsset> fallbackFontAssetTable = new List<TMP_FontAsset>();
        public bool HasCharacter(char c) => true;
        public static TMP_FontAsset CreateFontAsset(Font font) => null;
    }

    public abstract class TMP_Text : MonoBehaviour
    {
        public virtual string text { get; set; }
        public TMP_FontAsset font { get; set; }
        public float fontSize { get; set; }
        public float fontSizeDefault { get; set; }
        public bool enableAutoSizing { get; set; }
        public RectTransform rectTransform { get { return GetComponent<RectTransform>(); } }
        public Bounds textBounds { get { return default(Bounds); } }

        public void SetText(string text) { this.text = text; }
        public void ForceMeshUpdate() { }
    }
}
