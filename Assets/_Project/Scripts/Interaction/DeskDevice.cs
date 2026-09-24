using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// Прибор на рабочем столе: микрофон, микшер, плеер, телефон, монитор.
    /// Отвечает на наведение курсора в режиме за столом — обводится тонким контуром.
    /// </summary>
    /// <remarks>
    /// Поведение по нажатию появится вместе с диалогами и панелями устройств: у каждого
    /// прибора оно своё, и общего действия, которое можно было бы описать здесь, нет.
    /// Класс всё равно заводится отдельным, а не собирается из готовых: обводка работает
    /// только на наследниках <see cref="Interactable"/>, и это точка, куда поведение
    /// приборов будет добавляться.
    /// </remarks>
    public class DeskDevice : Interactable
    {
        [Header("Прибор")]
        [Tooltip("Имя для подсказок и логов. Если пусто, берётся имя объекта.")]
        [SerializeField] private string displayName;

        /// <summary>Как прибор называется в интерфейсе.</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        public override void Interact(PlayerInteractor interactor)
        {
            // Пока ничего. Сюда придут панель прибора и разговор по телефону.
        }
    }
}
