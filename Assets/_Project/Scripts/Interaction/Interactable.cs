using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// База для всего, с чем игрок может взаимодействовать: кресло, кнопка, ручка, ящик.
    /// Объект отвечает только за то, что произойдёт; поиск цели, ввод и подсказку
    /// держит <see cref="PlayerInteractor"/> на игроке.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        [Header("Подсказка")]
        [Tooltip("Точка, над которой всплывает подсказка. Если пусто — берётся сам объект. " +
                 "У блокаутной мебели пивот часто далеко от видимой геометрии, поэтому анкер лучше задавать явно.")]
        [SerializeField] private Transform promptAnchor;

        [Tooltip("Текст подсказки. Обычно клавиша, но может быть и словом.")]
        [SerializeField] private string promptText = "F";

        public Transform PromptAnchor => promptAnchor != null ? promptAnchor : transform;

        public string PromptText => promptText;

        /// <summary>Доступно ли взаимодействие прямо сейчас: дверь заперта, кассета уже вставлена и т.п.</summary>
        public virtual bool CanInteract => isActiveAndEnabled;

        /// <summary>
        /// Пока true, игрок считается занятым этим объектом: поиск других целей не идёт,
        /// а следующее нажатие уходит сюда же. Так устроен выход из режима сидения.
        /// </summary>
        public virtual bool IsExclusive => false;

        public abstract void Interact(PlayerInteractor interactor);

        /// <summary>Объект стал ближайшей целью. Задел под подсветку.</summary>
        public virtual void OnFocusEnter() { }

        /// <summary>Объект перестал быть целью.</summary>
        public virtual void OnFocusExit() { }
    }
}
