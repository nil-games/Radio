using System.Collections.Generic;
using Radio.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Radio.UI
{
    /// <summary>
    /// Список задач в правом верхнем углу. Виден всегда — при ходьбе, за столом,
    /// в разговоре и в мини-игре: своим холстом поверх остальных.
    /// </summary>
    /// <remarks>
    /// Новая строка проявляется, снятая — коротко гаснет и уходит: мгновенная подмена
    /// текста в углу экрана прошла бы незамеченной, а смысл панели как раз в том,
    /// чтобы игрок заметил новую задачу.
    /// </remarks>
    public sealed class TaskHud : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private TaskLog log;

        [Tooltip("Шрифт подписей. Обязан содержать кириллицу.")]
        [SerializeField] private TMP_FontAsset font;

        [Header("Вид")]
        [Tooltip("Порядок холста. Выше диалога, мини-игр и радио, чтобы список не пропадал под ними.")]
        [SerializeField] private int sortingOrder = 50;

        [SerializeField] private Vector2 margin = new Vector2(36f, 32f);

        [SerializeField] private float width = 460f;

        [SerializeField] private float fadeTime = 0.35f;

        [SerializeField] private Color headerColor = new Color(1f, 0.85f, 0.55f, 0.85f);
        [SerializeField] private Color textColor = new Color(1f, 1f, 1f, 0.92f);
        [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.45f);

        private sealed class Row
        {
            public string Id;
            public CanvasGroup Group;
            public bool Leaving;
        }

        private readonly List<Row> _rows = new List<Row>();
        private RectTransform _list;
        private CanvasGroup _panel;

        /// <summary>
        /// Нижний край панели задач от верха экрана, в единицах холста (эталон 1920×1080).
        /// По нему другие надписи встают под список, как бы он ни вырос.
        /// </summary>
        public float Bottom => _list == null ? 0f : margin.y + _list.rect.height;

        /// <summary>Отступ панели от правого края экрана, в единицах холста.</summary>
        public float RightMargin => margin.x;

        private void Awake()
        {
            if (log == null || font == null)
            {
                Debug.LogError($"{nameof(TaskHud)}: не заданы журнал задач или шрифт.", this);
                enabled = false;
                return;
            }

            Build();
            log.Changed += Sync;
        }

        private void OnDestroy()
        {
            if (log != null)
            {
                log.Changed -= Sync;
            }
        }

        private void Start() => Sync();

        private void Update()
        {
            var step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime);

            for (var i = _rows.Count - 1; i >= 0; i--)
            {
                var row = _rows[i];
                row.Group.alpha = Mathf.MoveTowards(row.Group.alpha, row.Leaving ? 0f : 1f, step);

                if (row.Leaving && row.Group.alpha <= 0f)
                {
                    Destroy(row.Group.gameObject);
                    _rows.RemoveAt(i);
                }
            }

            // Панель с заголовком гаснет вместе с последней строкой, а не раньше неё.
            _panel.alpha = Mathf.MoveTowards(_panel.alpha, _rows.Count > 0 ? 1f : 0f, step);
        }

        /// <summary>Сверить строки со списком: новые добавить, снятые отправить гаснуть.</summary>
        private void Sync()
        {
            foreach (var row in _rows)
            {
                if (!row.Leaving && !Contains(row.Id))
                {
                    row.Leaving = true;
                }
            }

            foreach (var entry in log.Active)
            {
                if (FindRow(entry.Id) == null)
                {
                    _rows.Add(CreateRow(entry));
                }
            }
        }

        private bool Contains(string id)
        {
            foreach (var entry in log.Active)
            {
                if (entry.Id == id)
                {
                    return true;
                }
            }

            return false;
        }

        private Row FindRow(string id)
        {
            foreach (var row in _rows)
            {
                if (row.Id == id && !row.Leaving)
                {
                    return row;
                }
            }

            return null;
        }

        private Row CreateRow(TaskLog.Entry entry)
        {
            var go = new GameObject("Task_" + entry.Id, typeof(RectTransform));
            go.transform.SetParent(_list, false);

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var label = CreateText(go.transform, "• " + entry.Text, 26f, textColor);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.alignment = TextAlignmentOptions.TopRight;

            // Высота строки по тексту: длинная задача переносится, а не обрезается.
            var fitter = go.AddComponent<LayoutElement>();
            fitter.preferredWidth = width;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            fitter.preferredHeight = label.GetPreferredValues(label.text, width, 0f).y;

            return new Row { Id = entry.Id, Group = group };
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

            // Рейкастера на холсте нет намеренно: список не должен перехватывать клики
            // по интерфейсу под ним.
            var panel = new GameObject("Panel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = -margin;

            var background = panel.AddComponent<Image>();
            background.color = panelColor;
            background.raycastTarget = false;

            _panel = panel.AddComponent<CanvasGroup>();
            _panel.alpha = 0f;
            _panel.blocksRaycasts = false;

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 14, 16);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var size = panel.AddComponent<ContentSizeFitter>();
            size.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            size.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var header = CreateText(rect, "ЗАДАЧИ", 20f, headerColor);
            header.alignment = TextAlignmentOptions.Right;
            header.characterSpacing = 8f;
            var headerSize = header.gameObject.AddComponent<LayoutElement>();
            headerSize.preferredWidth = width;
            headerSize.preferredHeight = 26f;

            // Строки лежат прямо в панели под заголовком: раскладка у них общая.
            _list = rect;
        }

        private TextMeshProUGUI CreateText(Transform parent, string text, float size, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }
    }
}
