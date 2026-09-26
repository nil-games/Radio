using System;
using Radio.World;
using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// Звонящий телефон: тянет звонок, пока игрок не снимет трубку.
    /// </summary>
    /// <remarks>
    /// Звонок привязан к часам смены плюс задержка в реальных секундах: часы в 00:00 стоят,
    /// пока игрок бездействует, и без задержки телефон зазвонил бы в первый же кадр.
    /// Отладочный перевод часов дальше отметки отменяет звонок и глушит его, если он уже идёт.
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

        [Tooltip("Когда зазвонит: отметка на часах смены и задержка в реальных секундах после неё.")]
        [SerializeField] private GameTimeMark ringAt = new GameTimeMark("00:00", 2f);

        private AudioSource _source;
        private TimeManager _time;
        private TimedEvent _ring;
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
            _time = GameSession.Current != null ? GameSession.Current.Time : null;
            var night = _time != null ? _time.Night : 1;
            _armed = Array.IndexOf(ringsOnNights, night) >= 0;
            _ring = new TimedEvent(ringAt);

            if (!_ring.IsValid)
            {
                Debug.LogError($"{nameof(PhoneRinger)}: время звонка «{ringAt.at}» не в формате ЧЧ:ММ.", this);
                _armed = false;
            }

            if (_time != null)
            {
                _time.DebugJumped += OnDebugJumped;
            }
        }

        private void OnDestroy()
        {
            if (_time != null)
            {
                _time.DebugJumped -= OnDebugJumped;
            }
        }

        private void Update()
        {
            if (_armed && !IsRinging && _ring.Poll(_time))
            {
                StartRinging();
            }
        }

        private void OnDebugJumped(int minutes)
        {
            if (_ring != null && _ring.SkipIfBefore(minutes))
            {
                StopRinging();
            }
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
