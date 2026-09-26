using UnityEngine;

namespace Radio.World
{
    /// <summary>
    /// Ставит задачу, когда часы смены доходят до отметки: «в 01:00 вернуться за стол».
    /// </summary>
    /// <remarks>
    /// По часам, а не из текста диалога: тогда задача появляется и при отладочной
    /// перестановке времени, и при любом другом пути к этой отметке, а не только
    /// после конкретного разговора. Перевод часов дальше отметки отменяет задачу.
    /// </remarks>
    public sealed class TaskAtTime : MonoBehaviour
    {
        [Tooltip("Когда ставится задача: отметка на часах смены и задержка в реальных секундах после неё.")]
        [SerializeField] private GameTimeMark when = new GameTimeMark("01:00");

        [Tooltip("Задача из каталога.")]
        [SerializeField] private string taskId = "return_to_desk";

        [Tooltip("Если пусто, ищется в сцене.")]
        [SerializeField] private TaskLog tasks;

        [Tooltip("Если пусто, берутся из GameSession в сцене.")]
        [SerializeField] private TimeManager time;

        private TimedEvent _event;

        // Start, а не Awake: сессия заводится в своём Awake, и порядок между ними не гарантирован.
        private void Start()
        {
            _event = new TimedEvent(when);

            if (!_event.IsValid)
            {
                Debug.LogError($"{nameof(TaskAtTime)}: время «{when.at}» не в формате ЧЧ:ММ.", this);
                enabled = false;
                return;
            }

            if (time == null && GameSession.Current != null)
            {
                time = GameSession.Current.Time;
            }

            if (tasks == null)
            {
                tasks = FindAnyObjectByType<TaskLog>();
            }

            if (time == null || tasks == null)
            {
                Debug.LogError($"{nameof(TaskAtTime)}: нет часов смены или списка задач.", this);
                enabled = false;
                return;
            }

            time.DebugJumped += OnDebugJumped;
        }

        private void OnDestroy()
        {
            if (time != null)
            {
                time.DebugJumped -= OnDebugJumped;
            }
        }

        private void Update()
        {
            if (_event.Poll(time))
            {
                tasks.Add(taskId);
            }
        }

        private void OnDebugJumped(int minutes) => _event.SkipIfBefore(minutes);
    }
}
