using UnityEngine;

namespace SailwindTranslator
{
    /// <summary>
    /// Периодически вызывает FontResolver.TryRegister(), потому что:
    /// 1) на момент Awake шрифты игры ещё не загружены ("0 TMP_FontAsset" в логе);
    /// 2) Harmony-патчи могут вообще не сработать, если Sailwind использует
    ///    свой текстовый класс — тогда ApplyTo() не позовётся, и шрифт пришлось бы
    ///    создавать только тут, по таймеру.
    /// </summary>
    public class FontTicker : MonoBehaviour
    {
        private float _timer = 0f;
        private const float INTERVAL = 1f;

        private void Update()
        {
            if (Plugin.FontResolver == null) return;
            _timer += Time.deltaTime;
            if (_timer < INTERVAL) return;
            _timer = 0f;

            Plugin.FontResolver.TryRegister();
        }
    }
}
