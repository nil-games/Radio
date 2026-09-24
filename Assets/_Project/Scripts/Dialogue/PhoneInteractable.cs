using Radio.Interaction;
using UnityEngine;

namespace Radio.Dialogue
{
    /// <summary>
    /// Телефон на столе. Снятая трубка обрывает звонок и начинает разговор.
    /// </summary>
    /// <remarks>
    /// Аппарат доступен всегда, а не только пока звонит: молчащий телефон,
    /// который нельзя даже взять, читается как сломанный. Что окажется в трубке,
    /// решает сам узел Yarn — если никто не звонит, в ней гудки.
    /// </remarks>
    public sealed class PhoneInteractable : DialogueInteractable
    {
        [Header("Телефон")]
        [Tooltip("Если пусто, ищется на этом же объекте.")]
        [SerializeField] private PhoneRinger ringer;

        protected override void Awake()
        {
            base.Awake();

            if (ringer == null)
            {
                ringer = GetComponent<PhoneRinger>();
            }

            if (ringer == null)
            {
                // Не ошибка: телефон без звонка работает, просто никогда не звонит.
                Debug.LogWarning($"{nameof(PhoneInteractable)}: не найден {nameof(PhoneRinger)}, " +
                                 "аппарат звонить не будет.", this);
            }
        }

        public override void Interact(PlayerInteractor interactor)
        {
            // Звонок обрываем до начала разговора, а не после: иначе первая реплика
            // прозвучала бы поверх звонящего телефона. Если телефон молчал,
            // вызов ничего не делает.
            if (ringer != null)
            {
                ringer.StopRinging();
            }

            base.Interact(interactor);
        }
    }
}
