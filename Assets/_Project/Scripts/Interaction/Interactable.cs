using System;
using Radio.Interaction.Highlight;
using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// База для всего, с чем игрок может взаимодействовать: кресло, кнопка, ручка, ящик.
    /// Объект отвечает только за то, что произойдёт; поиск цели, ввод и прицел
    /// держит <see cref="PlayerInteractor"/> на игроке.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        [Header("Взаимодействие")]
        [Tooltip("Дистанция от глаз игрока, ближе которой объект реагирует на прицел, м. " +
                 "От неё зависят и реакция прицела, и обводка, и возможность нажать — " +
                 "они проверяются одним условием и не могут разойтись.")]
        [SerializeField] private float interactionRadius = 2f;

        [Tooltip("Доступен ли предмет, когда игрок просто ходит по комнате. Для приборов на " +
                 "столе галочку снимают: они работают только в режиме за столом, а обводка " +
                 "на подходе обещала бы взаимодействие, которого не будет.")]
        [SerializeField] private bool availableWhileWalking = true;

        [Header("Ссылки")]
        [Tooltip("Подсветка объекта. Если пусто — ищется среди своих компонентов.")]
        [SerializeField] private InteractableHighlighter highlighter;

        [Tooltip("Анимация взаимодействия. Необязательна: без неё действие происходит мгновенно.")]
        [SerializeField] private InteractionAnimator interactionAnimator;

        private bool _playing;

        public float InteractionRadius => interactionRadius;

        /// <summary>Виден ли предмет прицелу, пока игрок ходит, а не сидит за столом.</summary>
        public bool AvailableWhileWalking => availableWhileWalking;

        /// <summary>
        /// Доступно ли взаимодействие прямо сейчас: дверь заперта, кассета уже вставлена,
        /// либо ещё играет анимация предыдущего действия.
        /// </summary>
        public virtual bool CanInteract => isActiveAndEnabled && !_playing;

        /// <summary>
        /// Пока true, игрок считается занятым этим объектом: поиск других целей не идёт,
        /// а следующее нажатие уходит сюда же. Так устроен выход из режима сидения.
        /// </summary>
        public virtual bool IsExclusive => false;

        public abstract void Interact(PlayerInteractor interactor);

        protected virtual void Awake()
        {
            if (highlighter == null)
            {
                highlighter = GetComponentInChildren<InteractableHighlighter>();
            }
        }

        /// <summary>Объект попал под прицел.</summary>
        public virtual void OnFocusEnter()
        {
            highlighter?.Show();
        }

        /// <summary>Прицел ушёл с объекта.</summary>
        public virtual void OnFocusExit()
        {
            highlighter?.Hide();
        }

        /// <summary>
        /// Проигрывает анимацию взаимодействия и выполняет действие по её окончании.
        /// Если аниматора нет, действие выполняется сразу — благодаря этому сценарии
        /// пишутся одинаково и до появления анимаций, и после.
        /// </summary>
        protected void PlayThen(string stateName, Action action)
        {
            if (interactionAnimator == null)
            {
                action();
                return;
            }

            // Пока идёт анимация, CanInteract возвращает false: иначе игрок нажмёт
            // второй раз в середине и запустит действие повторно.
            _playing = true;

            interactionAnimator.Play(stateName, () =>
            {
                _playing = false;
                action();
            });
        }
    }
}
