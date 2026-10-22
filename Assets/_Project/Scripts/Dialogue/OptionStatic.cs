using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Radio.Dialogue
{
    /// <summary>
    /// Помехи VHS за текстом варианта ответа, пока он выбран: навели мышь или дошли
    /// стрелками. Появляются и гаснут плавно.
    /// </summary>
    /// <remarks>
    /// Выбор читаем у EventSystem, а не ловим события: Yarn сам выделяет вариант при
    /// наведении, а снимает выделение, когда выбор уже закрылся и пункт ушёл в пул, —
    /// опрос в этом случае надёжнее пары OnSelect/OnDeselect.
    /// <para/>
    /// Сам шум рисует шейдер Radio/UI/VhsStatic на <see cref="image"/>; здесь только
    /// его прозрачность. Недоступный (зачёркнутый) вариант помех не получает.
    /// </remarks>
    public sealed class OptionStatic : MonoBehaviour
    {
        [Tooltip("Картинка с материалом помех. Лежит под текстом варианта.")]
        [SerializeField] private Image image;

        [Tooltip("Вариант ответа, за выбором которого следим. Пусто — берётся с этого объекта.")]
        [SerializeField] private Selectable option;

        [Tooltip("Сколько длится появление и угасание помех, с.")]
        [SerializeField] private float fadeTime = 0.15f;

        private float _alpha;

        private void Awake()
        {
            if (option == null)
            {
                option = GetComponent<Selectable>();
            }

            Apply(0f);
        }

        private void OnDisable()
        {
            // Пункт вернули в пул посреди угасания: в следующий раз он должен
            // появиться чистым, а не с помехами прошлого выбора.
            _alpha = 0f;
            Apply(0f);
        }

        private void Update()
        {
            if (image == null || option == null)
            {
                return;
            }

            var events = EventSystem.current;
            var selected = events != null && events.currentSelectedGameObject == option.gameObject
                           && option.IsInteractable();

            var target = selected ? 1f : 0f;
            var step = fadeTime > 0f ? Time.unscaledDeltaTime / fadeTime : 1f;
            _alpha = Mathf.MoveTowards(_alpha, target, step);
            Apply(_alpha);
        }

        private void Apply(float alpha)
        {
            if (image == null)
            {
                return;
            }

            var color = image.color;
            color.a = alpha;
            image.color = color;

            // Совсем погасший слой не рисуем вовсе: шейдер считает шум на каждом пикселе.
            image.enabled = alpha > 0f;
        }
    }
}
