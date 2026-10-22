using System;
using Radio.World;
using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// Звонящий телефон: тянет звонок, пока игрок не снимет трубку.
    /// </summary>
    /// <remarks>
    /// Звонков за ночь несколько: начальник в 00:00, Саша в 03:00. Каждый привязан к часам
    /// смены плюс задержка в реальных секундах: часы в 00:00 стоят, пока игрок бездействует,
    /// и без задержки телефон зазвонил бы в первый же кадр.
    /// Отладочный перевод часов дальше отметки отменяет звонок и глушит его, если он уже идёт.
    /// Кто окажется в трубке, решает Phone.yarn, а не этот компонент.
    /// Кроме звонков по часам, диалог может попросить перезвонить: <c>&lt;&lt;ring_phone 60&gt;&gt;</c>
    /// — телефон зазвонит через 60 реальных секунд. Это ответ на выбор игрока
    /// (перезвон Тамарочки), а не событие ночи, поэтому оно не привязано к часам.
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    public sealed class PhoneRinger : MonoBehaviour, ISaveBlocker
    {
        [Header("Звук")]
        [Tooltip("Звонок телефона. Зацикливается сам, отдельная галочка не нужна.")]
        [SerializeField] private AudioClip ringClip;

        [Header("Когда звонить")]
        [Tooltip("Устарело: ночь теперь записана в самой отметке («ЧЧ:ММ:НН»). Не используется.")]
        [SerializeField, HideInInspector] private int[] ringsOnNights = { 1 };

        [Tooltip("Когда звонит: отметки на часах смены и задержка в реальных секундах после каждой.")]
        [SerializeField] private GameTimeMark[] rings = { new GameTimeMark("00:00", 2f) };

        private AudioSource _source;
        private TimeManager _time;
        private TimedEvent[] _rings = Array.Empty<TimedEvent>();
        private float _callbackAt = -1f;

        // Какая отметка звенит сейчас. Минус один — звонок включили не по часам.
        private int _ringingIndex = -1;

        /// <summary>Телефон звонит прямо сейчас. Пока нет — снимать трубку не с чего.</summary>
        public bool IsRinging { get; private set; }

        /// <summary>Звонит или ждёт обещанного перезвона — сохраниться посреди нельзя.</summary>
        public bool BlocksSave => IsRinging || _callbackAt >= 0f;

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
            // Ночь не проверяем: она записана в отметке звонка, а смена ночи идёт без
            // перезагрузки сцены — проверка при старте отрезала бы звонки следующей ночи.
            _time = GameSession.Current != null ? GameSession.Current.Time : null;

            var runner = FindAnyObjectByType<Yarn.Unity.DialogueRunner>();

            if (runner != null)
            {
                runner.AddCommandHandler("ring_phone", (Action<float>)RingIn);
            }
            _rings = new TimedEvent[rings.Length];

            for (var i = 0; i < rings.Length; i++)
            {
                _rings[i] = new TimedEvent(rings[i], TimedEvent.KeyFor(this, i));

                if (!_rings[i].IsValid)
                {
                    Debug.LogError($"{nameof(PhoneRinger)}: время звонка «{rings[i].at}» не в формате ЧЧ:ММ.", this);
                }
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
            if (_callbackAt >= 0f && Time.time >= _callbackAt && !IsRinging)
            {
                _callbackAt = -1f;
                StartRinging();
            }

            if (_time == null)
            {
                return;
            }

            // Опрашиваем все отметки, даже пока телефон звонит: иначе отметка, пришедшая
            // во время звонка, сработала бы позже, чем должна.
            for (var i = 0; i < _rings.Length; i++)
            {
                if (_rings[i].IsValid && _rings[i].Poll(_time) && !IsRinging)
                {
                    StartRinging();
                    _ringingIndex = i;
                }
            }
        }

        /// <summary>Перезвонить через <paramref name="seconds"/> реальных секунд.</summary>
        public void RingIn(float seconds) => _callbackAt = Time.time + Mathf.Max(0f, seconds);

        private void OnDebugJumped(int minutes)
        {
            // Обещанный перезвон относится к прошлому времени — отменяем.
            _callbackAt = -1f;
            var stop = false;

            for (var i = 0; i < _rings.Length; i++)
            {
                // Глушим, только если отменён тот звонок, что звенит сейчас: прошлый,
                // давно отзвонивший, не повод обрывать нынешний.
                if (_rings[i].IsValid && _rings[i].SkipIfBefore(minutes)
                    && (_ringingIndex == i || _ringingIndex < 0))
                {
                    stop = true;
                }
            }

            if (stop)
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
            _ringingIndex = -1;
            _source.Stop();
            RingingStopped?.Invoke();
        }
    }
}
