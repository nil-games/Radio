using Radio.Interaction;
using UnityEngine;

namespace Radio.Dialogue
{
    /// <summary>
    /// Телефон на столе. Отвечает на нажатие, только пока звонит:
    /// снятая трубка обрывает звонок и начинает разговор.
    /// </summary>
    public sealed class PhoneInteractable : DialogueInteractable
    {
        [Header("Телефон")]
        [Tooltip("Если пусто, ищется на этом же объекте.")]
        [SerializeField] private PhoneRinger ringer;

        /// <summary>
        /// Молчащий телефон не подсвечивается и не нажимается: обводка на нём
        /// обещала бы разговор, которого не будет.
        /// </summary>
        public override bool CanInteract => base.CanInteract && ringer != null && ringer.IsRinging;

        protected override void Awake()
        {
            base.Awake();

            if (ringer == null)
            {
                ringer = GetComponent<PhoneRinger>();
            }

            if (ringer == null)
            {
                Debug.LogError($"{nameof(PhoneInteractable)}: не найден {nameof(PhoneRinger)}. " +
                               "Телефон никогда не станет доступен.", this);
            }
        }

        public override void Interact(PlayerInteractor interactor)
        {
            // Звонок обрываем до начала разговора, а не после: иначе первая реплика
            // прозвучала бы поверх звонящего телефона.
            if (ringer != null)
            {
                ringer.StopRinging();
            }

            base.Interact(interactor);
        }
    }
}
