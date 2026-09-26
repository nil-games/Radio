using Radio.World;
using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// Гость за входной дверью: пришёл, постучал и ждёт, пока игрок откроет.
    /// </summary>
    /// <remarks>
    /// Стучит ровно дважды, а не без конца: первый раз — в момент прихода, где бы ни был
    /// игрок, и тогда же появляется задача; второй — когда игрок впервые встаёт из-за стола,
    /// чтобы напомнить, куда идти. Дальше гость молча ждёт: задача в списке и так
    /// не даст о нём забыть, а стук каждые несколько секунд быстро начинает раздражать.
    /// Приходит гость по часам смены (с задержкой в реальных секундах, если нужна),
    /// уходит — по флагу конца визита. Отладочный перевод часов дальше отметки прихода
    /// отменяет визит целиком.
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    public sealed class DoorKnocker : MonoBehaviour
    {
        [Header("Звук")]
        [Tooltip("Одна серия стука. Можно оставить пустым: тогда дверь «стучит» беззвучно, " +
                 "но всё остальное работает.")]
        [SerializeField] private AudioClip knockClip;

        [Header("Когда стучать")]
        [Tooltip("Когда гость приходит: отметка на часах смены и задержка в реальных секундах после неё.")]
        [SerializeField] private GameTimeMark arriveAt = new GameTimeMark("00:30");

        [Tooltip("Флаг мира, после которого гость больше не приходит.")]
        [SerializeField] private string doneFlag = "STORY_GRANNY_VISIT_DONE";

        [Header("Задача")]
        [Tooltip("Задача, которая появляется с первым стуком. Снимает её сам разговор у двери. " +
                 "Пусто — без задачи.")]
        [SerializeField] private string taskId = "door_knock";

        [Header("Ссылки")]
        [Tooltip("Если пусто, ищется в сцене.")]
        [SerializeField] private PlayerInteractor interactor;

        [Tooltip("Если пусто, ищется в сцене.")]
        [SerializeField] private TaskLog tasks;

        private AudioSource _source;
        private TimeManager _time;
        private TimedEvent _arrival;
        private bool _arrived;
        private bool _remindedOnStandUp;
        private bool _wasSuspended;

        /// <summary>Гость стоит за дверью и ждёт.</summary>
        public bool IsKnocking { get; private set; }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;

            // Стук слышно со стороны двери: игрок должен понять, куда идти.
            _source.spatialBlend = 1f;
        }

        // Часы берём в Start: в Awake сессии в сцене могло ещё не быть.
        private void Start()
        {
            _time = GameSession.Current != null ? GameSession.Current.Time : null;
            _arrival = new TimedEvent(arriveAt);

            if (!_arrival.IsValid)
            {
                Debug.LogError($"{nameof(DoorKnocker)}: время прихода «{arriveAt.at}» не в формате ЧЧ:ММ.", this);
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

        private void OnDebugJumped(int minutes)
        {
            if (_arrival != null && _arrival.SkipIfBefore(minutes))
            {
                Stop();
            }
        }

        private void Update()
        {
            // Условия проверяются каждый кадр, а не один раз: визит могли закончить в обход
            // двери — тогда гость должен уйти сам.
            _arrival?.Poll(_time);
            IsKnocking = GuestWaiting();

            var suspended = IsPlayerSuspended();

            if (IsKnocking && !_arrived)
            {
                _arrived = true;
                KnockOnce();
                AddTask();
            }
            else if (IsKnocking && !_remindedOnStandUp && _wasSuspended && !suspended)
            {
                // Игрок встал из-за стола (или вышел из мини-игры в ходьбу) — стучим ещё раз.
                _remindedOnStandUp = true;
                KnockOnce();
            }

            _wasSuspended = suspended;
        }

        /// <summary>Дверь открыли: гость дождался, стук обрывается.</summary>
        public void Stop()
        {
            IsKnocking = false;
            _source.Stop();
        }

        private bool GuestWaiting()
        {
            if (_arrival == null || !_arrival.Fired || _arrival.Skipped)
            {
                return false;
            }

            var session = GameSession.Current;
            return session != null && (!session.World.TryGet<bool>(doneFlag, out var done) || !done);
        }

        /// <summary>Игрок сидит за столом, играет или разговаривает — ходить он не может.</summary>
        private bool IsPlayerSuspended()
        {
            if (interactor == null)
            {
                interactor = FindAnyObjectByType<PlayerInteractor>();
            }

            return interactor != null && interactor.Movement != null && interactor.Movement.IsSuspended;
        }

        private void AddTask()
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return;
            }

            if (tasks == null)
            {
                tasks = FindAnyObjectByType<TaskLog>();
            }

            if (tasks != null)
            {
                tasks.Add(taskId);
            }
        }

        private void KnockOnce()
        {
            if (knockClip == null)
            {
                // Механика не должна ждать аудиофайла: иначе её не проверить до самого конца.
                Debug.Log($"{nameof(DoorKnocker)}: тук-тук-тук (звук стука не задан).", this);
                return;
            }

            _source.PlayOneShot(knockClip);
        }
    }
}
