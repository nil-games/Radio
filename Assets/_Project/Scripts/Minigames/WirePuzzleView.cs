using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Radio.Minigames
{
    /// <summary>
    /// Рисует поле мини-игры «Провод»: бледную раскладку клавиатуры, сам провод,
    /// точки нажатия и бегущий по проводу маячок.
    /// </summary>
    /// <remarks>
    /// Сетка собирается кодом из <see cref="KeyboardGrid"/>, а не расставляется руками
    /// в префабе: сорок шесть клеток, выставленных мышью, разъедутся при первой же правке,
    /// и проверить их можно будет только глазами.
    /// </remarks>
    public sealed class WirePuzzleView : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Контейнер поля. Сетка и провод строятся внутри него.")]
        [SerializeField] private RectTransform board;

        [Tooltip("Шрифт подписей. Обязан содержать кириллицу.")]
        [SerializeField] private TMP_FontAsset font;

        [Tooltip("Строка состояния под полем.")]
        [SerializeField] private TextMeshProUGUI status;

        [Header("Размеры")]
        [Tooltip("Сторона клетки, пикселей.")]
        [SerializeField] private float cellSize = 64f;

        [Tooltip("Зазор между клетками, пикселей.")]
        [SerializeField] private float cellGap = 4f;

        [Tooltip("Толщина провода, пикселей.")]
        [SerializeField] private float wireThickness = 10f;

        [Header("Цвета")]
        [SerializeField] private Color cellColor = new Color(1f, 1f, 1f, 0.07f);
        [SerializeField] private Color labelColor = new Color(1f, 1f, 1f, 0.22f);
        [SerializeField] private Color wireColor = new Color(0.36f, 0.35f, 0.86f, 1f);
        [SerializeField] private Color pointColor = new Color(0.42f, 0.42f, 0.95f, 1f);
        [SerializeField] private Color pointDoneColor = new Color(0.45f, 0.95f, 0.55f, 1f);
        [SerializeField] private Color pointNextColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color beaconColor = new Color(1f, 0.38f, 0.75f, 1f);

        private readonly List<Image> _points = new List<Image>();
        private RectTransform _beacon;
        private RectTransform _wireRoot;
        private bool _built;

        /// <summary>Сторона клетки вместе с зазором — шаг сетки.</summary>
        private float Step => cellSize + cellGap;

        /// <summary>
        /// Собирает поле под готовый путь. Путь приходит снаружи, а не строится здесь:
        /// он случайный, и построенный второй раз разошёлся бы с тем, по которому
        /// на самом деле бежит сигнал.
        /// </summary>
        public void Build(IReadOnlyList<Vector2> path)
        {
            if (board == null || font == null)
            {
                Debug.LogError($"{nameof(WirePuzzleView)}: не заданы поле или шрифт.", this);
                return;
            }

            for (var i = board.childCount - 1; i >= 0; i--)
            {
                Destroy(board.GetChild(i).gameObject);
            }

            _points.Clear();

            board.sizeDelta = new Vector2(KeyboardGrid.Width * Step, KeyboardGrid.RowCount * Step);

            BuildKeys();

            if (path != null && path.Count >= 2)
            {
                BuildWire(path);
                BuildPoints(path);
            }

            BuildBeacon();
            _built = true;
        }

        /// <summary>Передвинуть маячок. Координаты — в клетках, как в раскладке.</summary>
        public void SetBeacon(Vector2 cellPosition)
        {
            if (_beacon != null)
            {
                _beacon.anchoredPosition = ToLocal(cellPosition);
            }
        }

        /// <summary>
        /// Подсветить, какая точка следующая, и погасить уже пройденные.
        /// </summary>
        public void SetProgress(int nextPointIndex)
        {
            for (var i = 0; i < _points.Count; i++)
            {
                _points[i].color = i < nextPointIndex ? pointDoneColor
                                 : i == nextPointIndex ? pointNextColor
                                 : pointColor;
            }
        }

        public void SetStatus(string text, Color color)
        {
            if (status == null)
            {
                return;
            }

            status.text = text;
            status.color = color;
        }

        private void BuildKeys()
        {
            foreach (var cell in KeyboardGrid.Cells)
            {
                var rect = Create("Key_" + cell.Latin, board);
                rect.sizeDelta = new Vector2(cellSize, cellSize);
                rect.anchoredPosition = ToLocal(cell.Center);
                var image = rect.gameObject.AddComponent<Image>();
                image.color = cellColor;
                image.raycastTarget = false;

                // Кириллица крупно — её игрок видит на своих клавишах; латиница мелко
                // в углу, чтобы раскладка читалась и на английской клавиатуре.
                var big = CreateLabel("Cyrillic", rect, cellSize * 0.42f, TextAlignmentOptions.Center);
                big.text = cell.Cyrillic;

                // У клавиш без собственного символа мелкой подписи нет: она дублировала бы
                // крупную и только рябила бы в углу.
                if (string.IsNullOrEmpty(cell.Latin))
                {
                    continue;
                }

                var small = CreateLabel("Latin", rect, cellSize * 0.24f, TextAlignmentOptions.TopLeft);
                small.text = cell.Latin;
                small.margin = new Vector4(5f, 3f, 0f, 0f);
            }
        }

        private void BuildWire(IReadOnlyList<Vector2> path)
        {
            _wireRoot = Create("Wire", board);
            _wireRoot.anchorMin = new Vector2(0f, 1f);
            _wireRoot.anchorMax = new Vector2(0f, 1f);
            _wireRoot.sizeDelta = Vector2.zero;
            _wireRoot.anchoredPosition = Vector2.zero;

            for (var i = 0; i < path.Count - 1; i++)
            {
                var from = ToLocal(path[i]);
                var to = ToLocal(path[i + 1]);
                var delta = to - from;

                var segment = Create("Segment" + i, _wireRoot);
                segment.sizeDelta = new Vector2(delta.magnitude + wireThickness, wireThickness);
                segment.anchoredPosition = from + delta * 0.5f;
                segment.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

                var image = segment.gameObject.AddComponent<Image>();
                image.color = wireColor;
                image.raycastTarget = false;
            }
        }

        private void BuildPoints(IReadOnlyList<Vector2> path)
        {
            // Первая клетка — старт, на ней нажимать нечего.
            for (var i = 1; i < path.Count; i++)
            {
                var rect = Create("Point" + i, board);
                rect.sizeDelta = new Vector2(cellSize * 0.5f, cellSize * 0.5f);
                rect.anchoredPosition = ToLocal(path[i]);

                var image = rect.gameObject.AddComponent<Image>();
                image.color = pointColor;
                image.raycastTarget = false;
                _points.Add(image);
            }
        }

        private void BuildBeacon()
        {
            _beacon = Create("Beacon", board);
            _beacon.sizeDelta = new Vector2(cellSize * 0.30f, cellSize * 0.62f);

            var image = _beacon.gameObject.AddComponent<Image>();
            image.color = beaconColor;
            image.raycastTarget = false;
        }

        /// <summary>
        /// Из клеток в пиксели. Начало координат поля — верхний левый угол, поэтому
        /// Y у клеток отрицательный и домножать его не нужно.
        /// </summary>
        private Vector2 ToLocal(Vector2 cellPosition) => cellPosition * Step;

        private static RectTransform Create(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;

            // Все дети поля крепятся к его верхнему левому углу: так координаты клеток
            // совпадают с тем, как считает раскладка.
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private TextMeshProUGUI CreateLabel(string name, Transform parent, float size, TextAlignmentOptions alignment)
        {
            var rect = Create(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.color = labelColor;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        private void OnDisable()
        {
            // Следующий заход собирает поле заново: уровень мог смениться.
            _built = false;
        }

        public bool IsBuilt => _built;
    }
}
