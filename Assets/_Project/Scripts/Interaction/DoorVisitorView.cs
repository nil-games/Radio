using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// Видимый гость за входной дверью: модель стоит на площадке, пока гость ждёт,
    /// и начинает свою анимацию, когда дверь открывают.
    /// </summary>
    /// <remarks>
    /// Модель появляется вместе с приходом гостя (по его <see cref="DoorKnocker"/>), а не
    /// с началом разговора: разговор стартует, когда дверь уже открыта, и гость возник
    /// бы на пороге прямо на глазах у игрока. Пока дверь закрыта, анимация стоит на
    /// первом кадре, и открывший дверь видит её с самого начала — кошка садится
    /// и поднимает взгляд уже при нём. Уходит гость вместе с концом визита.
    /// </remarks>
    public sealed class DoorVisitorView : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Гость, чью модель показываем: видна, пока он ждёт за дверью или разговаривает.")]
        [SerializeField] private DoorKnocker knocker;

        [Tooltip("Дверь: с её открытием начинается анимация.")]
        [SerializeField] private EntranceDoor door;

        [Tooltip("Модель гостя. Выключена, пока гостя нет.")]
        [SerializeField] private GameObject model;

        [Tooltip("Аниматор модели. Пусто — гость просто стоит.")]
        [SerializeField] private Animator animator;

        [Header("Анимация")]
        [Tooltip("Состояние аниматора, которое играет, когда дверь открыли.")]
        [SerializeField] private string stateName = "Seat";

        private bool _visible;
        private bool _started;

        private void Awake()
        {
            if (knocker == null || door == null || model == null)
            {
                Debug.LogError($"{nameof(DoorVisitorView)}: не заданы гость, дверь или модель.", this);
                enabled = false;
                return;
            }

            model.SetActive(false);
        }

        private void Update()
        {
            var visible = knocker.IsKnocking;

            if (visible != _visible)
            {
                _visible = visible;
                model.SetActive(visible);
                _started = false;

                if (visible)
                {
                    Freeze();
                }
            }

            if (_visible && !_started && door.IsOpen)
            {
                _started = true;

                if (animator != null)
                {
                    animator.speed = 1f;
                }
            }
        }

        /// <summary>Встать на первый кадр анимации и ждать, пока откроют дверь.</summary>
        private void Freeze()
        {
            if (animator == null)
            {
                return;
            }

            animator.Play(stateName, 0, 0f);
            animator.Update(0f);
            animator.speed = 0f;
        }
    }
}
