using System;
using Radio.World;
using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// Звонящий телефон: тянет звонок, пока игрок не снимет трубку.
    /// </summary>
    /// <remarks>
    /// Звонок начинается по реальному времени, а не по игровым часам: игрок в этот момент
    /// только вошёл в квартиру и ещё ничего не сделал, а часы смены стоят, пока он бездействует.
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    public sealed class PhoneRinger : MonoBehaviour
    {
        [Header("Звук")]
        [Tooltip("Звонок телефона. Зацикливается сам, отдельная галочка не нужна.")]
        [SerializeField] private AudioClip ringClip;

        [Header("Когда звонить")]
        [Tooltip("Ночи, в которые телефон звонит при входе в квартиру.")]
        [SerializeField] private int[] ringsOnNights = { 1 };

        [Tooltip("Через сколько реальных секунд после начала смены зазвонит.")]
        [SerializeField] private float delaySeconds = 2f;

        private AudioSource _source;
        private float _startedAt;
        private bool _armed;

        /// <summary>Телефон звонит прямо сейчас. Пока нет — снимать трубку не с чего.</summary>
        public bool IsRinging { get; private set; }

        /// <summary>Трубку сняли или звонок оборвался.</summary>
        public event Action RingingStopped;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.clip = ringClip;
            _source.loop = true;
            _source.playOnAwake = false;

            // Звук идёт от самого аппарата: в комнате его должно быть слышно с той стороны,
            // где стоит телефон, иначе игрок не поймёт, куда идти.
            _source.spatialBlend = 1f;

        }

        // Номер ночи спрашиваем в Start: в Awake сессии в сцене могло ещё не быть,
        // порядок между объектами Unity не гарантирует.
        private void Start()
        {
            var night = GameSession.Current != null ? GameSession.Current.Time.Night : 1;
            _armed = Array.IndexOf(ringsOnNights, night) >= 0;
            _startedAt = Time.time;
        }

        private void Update()
        {
            if (!_armed || IsRinging)
            {
                return;
            }

            if (Time.time - _startedAt < delaySeconds)
            {
                return;
            }

            StartRinging();
        }

        public void StartRinging()
        {
            if (IsRinging)
            {
                return;
            }

            IsRinging = true;
            _armed = false;

            if (ringClip == null)
            {
                // Звонок без звука всё равно должен работать: механика телефона не
                // обязана ждать аудиофайл, иначе проверить её нельзя до самого конца.
                Debug.LogWarning($"{nameof(PhoneRinger)}: не задан звук звонка, телефон звонит беззвучно.", this);
                return;
            }

            _source.Play();
        }

        public void StopRinging()
        {
            if (!IsRinging)
            {
                return;
            }

            IsRinging = false;
            _source.Stop();
            RingingStopped?.Invoke();
        }
    }
}
