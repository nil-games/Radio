using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Radio.Interaction
{
    /// <summary>
    /// Экран радиоприёмника: шкала частот, бегущая по ней черта и цифры текущей частоты.
    /// Плюс подсказка по управлению внизу экрана игрока, пока он стоит у радио.
    /// </summary>
    /// <remarks>
    /// Шкала собирается кодом, как поля мини-игр: два десятка рисок, выставленных мышью,
    /// разъехались бы при первой же смене диапазона.
    /// </remarks>
    public sealed class RadioDialView : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Мировой холст на месте чёрного экрана радио. Выключен, пока к радио не подошли.")]
        [SerializeField] private RectTransform screen;

        [Tooltip("Шрифт подписей. Обязан содержать кириллицу.")]
        [SerializeField] private TMP_FontAsset font;

        [Header("Шкала")]
        [Tooltip("Отступ шкалы от краёв экрана, в единицах холста.")]
        [SerializeField] private float margin = 14f;

        [Tooltip("Подписи ставятся на каждое кратное этому числу МГц.")]
        [SerializeField] private int labelEvery = 4;

        [Header("Цвета")]
        [SerializeField] private Color backgroundColor = new Color(0.03f, 0.04f, 0.04f, 1f);
        [SerializeField] private Color scaleColor = new Color(0.95f, 0.85f, 0.6f, 0.85f);
        [SerializeField] private Color needleColor = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private Color readoutColor = new Color(1f, 0.9f, 0.65f, 1f);

        private RectTransform _needle;
        private TextMeshProUGUI _readout;
        private GameObject _hint;
        private float _min;
        private float _max;
        private float _left;
        private float _right;
        private bool _built;
        private bool _powered = true;
        private float _frequency;

        /// <summary>Показать экран и подсказку.</summary>
        public void Show(float min, float max)
        {
            if (screen == null || font == null)
            {
                Debug.LogError($"{nameof(RadioDialView)}: не заданы экран или шрифт.", this);
                return;
            }

            if (!_built || !Mathf.Approximately(min, _min) || !Mathf.Approximately(max, _max))
            {
                _min = min;
                _max = max;
                Build();
                _built = true;
            }

            screen.gameObject.SetActive(true);
            _hint.SetActive(true);
        }

        public void Hide()
        {
            if (screen != null)
            {
                screen.gameObject.SetActive(false);
            }

            if (_hint != null)
            {
                _hint.SetActive(false);
            }
        }

        public void SetFrequency(float frequency)
        {
            if (!_built)
            {
                return;
            }

            _frequency = frequency;
            var t = Mathf.InverseLerp(_min, _max, frequency);
            _needle.anchoredPosition = new Vector2(Mathf.Lerp(_left, _right, t), _needle.anchoredPosition.y);
            Refresh();
        }

        /// <summary>
        /// Выключенное радио показывает шкалу без подсветки: ручку крутить можно,
        /// частота запомнится, но видно, что приёмник молчит.
        /// </summary>
        public void SetPower(bool on)
        {
            _powered = on;
            Refresh();
        }

        private void Refresh()
        {
            if (!_built)
            {
                return;
            }

            _readout.text = _powered ? $"{_frequency:0.0} МГц" : $"ВЫКЛ  {_frequency:0.0}";
            _readout.color = _powered ? readoutColor : readoutColor * new Color(1f, 1f, 1f, 0.35f);
            _needle.GetComponent<Image>().color = _powered ? needleColor : needleColor * new Color(1f, 1f, 1f, 0.35f);
        }

        private void Build()
        {
            for (var i = screen.childCount - 1; i >= 0; i--)
            {
                Destroy(screen.GetChild(i).gameObject);
            }

            var size = screen.rect.size;
            _left = margin;
            _right = size.x - margin;

            // Всё считаем от левого нижнего угла: так риски и подписи ложатся
            // прямыми координатами без поправок на центр.
            var background = CreateRect("Background", screen, Vector2.zero, size);
            background.gameObject.AddComponent<Image>().color = backgroundColor;

            var baselineY = size.y * 0.42f;
            var baseline = CreateRect("Baseline", screen, new Vector2(_left, baselineY), new Vector2(_right - _left, 1.2f));
            baseline.gameObject.AddComponent<Image>().color = scaleColor;

            for (var mhz = Mathf.CeilToInt(_min); mhz <= Mathf.FloorToInt(_max); mhz++)
            {
                var x = Mathf.Lerp(_left, _right, Mathf.InverseLerp(_min, _max, mhz));
                var major = labelEvery > 0 && mhz % labelEvery == 0;
                var height = major ? 10f : 5f;

                var tick = CreateRect("Tick", screen, new Vector2(x - 0.5f, baselineY), new Vector2(1f, height));
                tick.gameObject.AddComponent<Image>().color = scaleColor;

                if (!major)
                {
                    continue;
                }

                var label = CreateText("Label", mhz.ToString(), 9f, scaleColor);
                SetRect(label.rectTransform, new Vector2(x - 15f, baselineY - 16f), new Vector2(30f, 14f));
            }

            _needle = CreateRect("Needle", screen, new Vector2(_left, baselineY - 8f), new Vector2(2f, 30f));
            _needle.pivot = new Vector2(0.5f, 0f);
            _needle.gameObject.AddComponent<Image>().color = needleColor;

            _readout = CreateText("Readout", string.Empty, 17f, readoutColor);
            _readout.alignment = TextAlignmentOptions.Right;
            SetRect(_readout.rectTransform, new Vector2(size.x - margin - 110f, size.y - margin - 20f), new Vector2(110f, 22f));

            BuildHint();
        }

        private void BuildHint()
        {
            if (_hint != null)
            {
                return;
            }

            // Подсказка живёт на экране игрока, а не на радио: на экранчике 24 на 9 сантиметров
            // ей не место, а игрок должен сразу понять, чем крутить.
            _hint = new GameObject("RadioHint", typeof(RectTransform));
            _hint.transform.SetParent(transform, false);

            var canvas = _hint.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = _hint.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(_hint.transform, false);
            text.font = font;
            text.text = "Q / E — крутить ручку настройки     R — вкл / выкл     F — отойти";
            text.fontSize = 28f;
            text.color = new Color(1f, 1f, 1f, 0.7f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 60f);
            rect.sizeDelta = new Vector2(1200f, 50f);

            _hint.SetActive(false);
        }

        private TextMeshProUGUI CreateText(string name, string text, float fontSize, Color color)
        {
            var rect = CreateRect(name, screen, Vector2.zero, Vector2.zero);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Прямоугольник с якорем и пивотом в левом нижнем углу экрана.</summary>
        private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
