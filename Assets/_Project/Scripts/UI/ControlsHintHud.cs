using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Radio.UI
{
    /// <summary>
    /// Постоянная подсказка по управлению в правом нижнем углу: «F - Взаимодействие / Отмена».
    /// </summary>
    /// <remarks>
    /// Своим холстом поверх остальных, как список задач: подсказка нужна и при ходьбе,
    /// и за столом, и в разговоре. Строится кодом, как и остальной интерфейс проекта.
    /// </remarks>
    public sealed class ControlsHintHud : MonoBehaviour
    {
        [Tooltip("Шрифт подсказки. Обязан содержать кириллицу.")]
        [SerializeField] private TMP_FontAsset font;

        [SerializeField] private string text = "F - Взаимодействие / Отмена";

        [SerializeField] private float fontSize = 26f;

        [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.9f);

        [Tooltip("Обводка: без неё белый текст теряется на светлых стенах.")]
        [SerializeField] private Color outlineColor = Color.black;

        [Range(0f, 1f)]
        [SerializeField] private float outlineWidth = 0.2f;

        [Tooltip("Отступ от правого и нижнего края, в единицах холста (эталон 1920×1080).")]
        [SerializeField] private Vector2 margin = new Vector2(36f, 28f);

        [Tooltip("Порядок холста. Выше диалога и мини-игр, как у списка задач.")]
        [SerializeField] private int sortingOrder = 50;

        private void Awake()
        {
            if (font == null)
            {
                Debug.LogError($"{nameof(ControlsHintHud)}: не задан шрифт.", this);
                enabled = false;
                return;
            }

            Build();
        }

        private void Build()
        {
            // TryGetComponent, а не «GetComponent ?? AddComponent»: в редакторе отсутствующий
            // компонент возвращается поддельным null, и оператор ?? его не замечает.
            if (!TryGetComponent<Canvas>(out var canvas))
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            if (!TryGetComponent<CanvasScaler>(out var scaler))
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Рейкастера на холсте нет намеренно: подсказка не должна перехватывать клики.
            var go = new GameObject("Hint", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-margin.x, margin.y);
            rect.sizeDelta = new Vector2(700f, 60f);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.outlineColor = outlineColor;
            label.outlineWidth = outlineWidth;
            label.alignment = TextAlignmentOptions.BottomRight;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }
    }
}
