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
    /// Функция Yarn <c>phone_was_ringing()</c> — звонил ли телефон, когда сняли трубку:
    /// звонок по времени (Дежурный, Север) не должен начаться от трубки, снятой заранее.
    /// </remarks>
    public sealed class PhoneInteractable : DialogueInteractable
    {
        [Header("Телефон")]
        [Tooltip("Если пусто, ищется на этом же объекте.")]
        [SerializeField] private PhoneRinger ringer;

        private bool _wasRinging;

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

            var runner = FindAnyObjectByType<Yarn.Unity.DialogueRunner>();

            if (runner != null)
            {
                runner.AddFunction("phone_was_ringing", (System.Func<bool>)(() => _wasRinging));
            }
        }

        public override void Interact(PlayerInteractor interactor)
        {
            // Звонок обрываем до начала разговора, а не после: иначе первая реплика
            // прозвучала бы поверх звонящего телефона. Если телефон молчал,
            // вызов ничего не делает.
            _wasRinging = ringer != null && ringer.IsRinging;

            if (ringer != null)
            {
                ringer.StopRinging();
            }

            base.Interact(interactor);
        }
    }
}
