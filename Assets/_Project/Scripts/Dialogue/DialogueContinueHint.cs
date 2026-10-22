using TMPro;
using UnityEngine;

namespace Radio.Dialogue
{
    /// <summary>
    /// Подсказка в правом нижнем углу: какой клавишей листать реплики.
    /// </summary>
    /// <remarks>
    /// Видна ровно тогда, когда видна реплика: прозрачность берётся у панели реплик.
    /// Пока на экране варианты ответа, панель реплик скрыта — и подсказка вместе с ней,
    /// потому что пробел там ничего не выбирает.
    /// <para/>
    /// Сама клавиша задаётся в Line Advancer; меняя её там, поменяй и текст здесь.
    /// </remarks>
    public sealed class DialogueContinueHint : MonoBehaviour
    {
        [Tooltip("CanvasGroup панели реплик (Line Presenter).")]
        [SerializeField] private CanvasGroup linePresenter;

        [Tooltip("Canvas диалога, куда ставится подсказка.")]
        [SerializeField] private RectTransform canvasRoot;

        [SerializeField] private string text = "Space (пробел) — дальше";

        [SerializeField] private float fontSize = 26f;

        [Tooltip("Отступ от правого нижнего угла, пикселей при 1920×1080. По высоте — над " +
                 "подсказкой «F — взаимодействие / отмена», которая стоит в самом углу.")]
        [SerializeField] private Vector2 margin = new Vector2(40f, 80f);

        [Range(0f, 1f)]
        [SerializeField] private float opacity = 0.6f;

        private TextMeshProUGUI _label;

        private void Awake()
        {
            if (linePresenter == null || canvasRoot == null)
            {
                Debug.LogError($"{nameof(DialogueContinueHint)}: не заданы панель реплик или Canvas.", this);
                enabled = false;
                return;
            }

            var go = new GameObject("ContinueHint", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvasRoot, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-margin.x, margin.y);
            rect.sizeDelta = new Vector2(600f, 60f);

            _label = go.AddComponent<TextMeshProUGUI>();
            _label.text = text;
            _label.fontSize = fontSize;
            _label.alignment = TextAlignmentOptions.BottomRight;
            _label.raycastTarget = false;

            // Шрифт с кириллицей — тот же, что у реплик.
            var sample = linePresenter.GetComponentInChildren<TMP_Text>(true);
            if (sample != null)
            {
                _label.font = sample.font;
            }

            _label.alpha = 0f;
        }

        private void LateUpdate()
        {
            var visible = linePresenter.gameObject.activeInHierarchy ? linePresenter.alpha : 0f;
            _label.alpha = visible * opacity;
        }
    }
}
