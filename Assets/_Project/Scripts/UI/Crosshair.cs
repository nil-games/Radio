using UnityEngine;
using UnityEngine.UI;

namespace Radio.UI
{
    /// <summary>
    /// Прицел в центре экрана. В покое — точка, при наведении на интерактивный объект
    /// разрастается в кольцо.
    /// </summary>
    public sealed class Crosshair : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Корень видимой части: гасится целиком, когда прицел не нужен.")]
        [SerializeField] private GameObject visual;

        [SerializeField] private Image dot;
        [SerializeField] private Image ring;

        [Header("Размеры")]
        [Tooltip("Диаметр точки в покое, px при эталонном разрешении 1920x1080.")]
        [SerializeField] private float idleSize = 6f;

        [Tooltip("Диаметр кольца при наведении на интерактивный объект, px.")]
        [SerializeField] private float focusedSize = 22f;

        [Tooltip("Время перехода точка-кольцо, с. Короткое: прицел должен читаться мгновенно.")]
        [SerializeField] private float transitionTime = 0.08f;

        private float _progress;
        private bool _focused;

        private void Awake()
        {
            if (visual == null || dot == null || ring == null)
            {
                Debug.LogError($"{nameof(Crosshair)}: не заданы ссылки на изображения прицела.", this);
                enabled = false;
                return;
            }

            ApplyVisual(0f);
        }

        /// <summary>Наведён ли прицел на интерактивный объект.</summary>
        public void SetFocused(bool focused) => _focused = focused;

        /// <summary>Показывать ли прицел вообще: в режиме сидя он не нужен.</summary>
        public void SetVisible(bool isVisible)
        {
            if (visual.activeSelf != isVisible)
            {
                visual.SetActive(isVisible);
            }
        }

        private void LateUpdate()
        {
            // unscaledDeltaTime, чтобы переход пережил будущую паузу.
            var target = _focused ? 1f : 0f;
            _progress = Mathf.MoveTowards(_progress, target, Time.unscaledDeltaTime / Mathf.Max(0.0001f, transitionTime));
            ApplyVisual(_progress);
        }

        private void ApplyVisual(float progress)
        {
            var eased = Mathf.SmoothStep(0f, 1f, progress);
            var size = Vector2.one * Mathf.Lerp(idleSize, focusedSize, eased);

            dot.rectTransform.sizeDelta = size;
            ring.rectTransform.sizeDelta = size;

            // Кроссфейд, а не подмена спрайта: подмена даёт заметный щелчок,
            // а так точка выглядит именно превращающейся в кольцо.
            dot.color = new Color(1f, 1f, 1f, 1f - eased);
            ring.color = new Color(1f, 1f, 1f, eased);
        }
    }
}
