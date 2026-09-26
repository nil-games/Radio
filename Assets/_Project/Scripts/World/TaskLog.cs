using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

namespace Radio.World
{
    /// <summary>
    /// Текущие задачи игрока. Сюжет ставит и снимает их командами диалога:
    /// &lt;&lt;task_add id&gt;&gt; — появилась, &lt;&lt;task_done id&gt;&gt; — игрок к ней приступил.
    /// Для условий есть функция task_active("id").
    /// </summary>
    /// <remarks>
    /// Состояние каждой задачи дублируется во флаг мира TASK_id: так его видят сохранения
    /// и любая проверка в игре, а не только этот список.
    /// Выполненная задача не возвращается повторным task_add: ветку диалога можно пройти
    /// ещё раз, и сделанное не должно снова всплыть в списке.
    /// </remarks>
    public sealed class TaskLog : MonoBehaviour
    {
        public readonly struct Entry
        {
            public readonly string Id;
            public readonly string Text;

            public Entry(string id, string text)
            {
                Id = id;
                Text = text;
            }
        }

        private const string FlagPrefix = "TASK_";

        [SerializeField] private TaskCatalog catalog;

        [Tooltip("Если пусто, ищется в сцене.")]
        [SerializeField] private DialogueRunner runner;

        private readonly List<Entry> _active = new List<Entry>();
        private readonly HashSet<string> _done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _registered;
        private TimeManager _time;

        /// <summary>Активные задачи в порядке появления.</summary>
        public IReadOnlyList<Entry> Active => _active;

        /// <summary>Список изменился.</summary>
        public event Action Changed;

        private void Awake()
        {
            if (catalog == null)
            {
                Debug.LogError($"{nameof(TaskLog)}: не задан каталог задач. Список задач отключён.", this);
                enabled = false;
                return;
            }

            RegisterCommands();
        }

        private void Start()
        {
            // Раннер мог проснуться позже нас — тогда регистрируемся здесь.
            RegisterCommands();

            // До стартовых задач: отладка могла перевести часы ещё до этого кадра.
            _time = GameSession.Current != null ? GameSession.Current.Time : null;

            if (_time != null)
            {
                _time.DebugJumped += SkipBefore;
            }

            foreach (var id in catalog.StartTasks)
            {
                Add(id);
            }
        }

        private void OnDestroy()
        {
            if (_time != null)
            {
                _time.DebugJumped -= SkipBefore;
            }
        }

        public bool IsActive(string id) => IndexOf(id) >= 0;

        public void Add(string id)
        {
            if (!enabled || string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            if (!catalog.TryGetText(id, out var text))
            {
                Debug.LogError($"{nameof(TaskLog)}: задачи {id} нет в каталоге.", this);
                return;
            }

            if (_done.Contains(id) || IsActive(id))
            {
                return;
            }

            _active.Add(new Entry(id, text));
            SetFlag(id, true);
            Changed?.Invoke();
        }

        /// <summary>
        /// Игрок приступил к задаче. Задача, которой ещё нет в списке, тоже считается сделанной:
        /// игрок мог дойти до неё раньше, чем сюжет о ней сказал, — тогда она и не появится.
        /// </summary>
        public void Complete(string id)
        {
            if (!enabled || string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            if (!catalog.TryGetText(id, out _))
            {
                Debug.LogError($"{nameof(TaskLog)}: задачи {id} нет в каталоге.", this);
                return;
            }

            _done.Add(id);
            SetFlag(id, false);

            var index = IndexOf(id);

            if (index < 0)
            {
                return;
            }

            _active.RemoveAt(index);
            Changed?.Invoke();
        }

        /// <summary>
        /// Только для отладки: снять все задачи, которые относятся ко времени раньше отметки,
        /// и не давать им появиться позже. Нужно, чтобы после перевода часов в списке
        /// остались только задачи нового времени. Зовётся по TimeManager.DebugJumped.
        /// </summary>
        public void SkipBefore(int minutes)
        {
            foreach (var id in catalog.IdsBefore(minutes))
            {
                Complete(id);
            }
        }

        private int IndexOf(string id)
        {
            for (var i = 0; i < _active.Count; i++)
            {
                if (string.Equals(_active[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void SetFlag(string id, bool active)
        {
            var session = GameSession.Current;

            if (session != null)
            {
                session.World.Set(FlagPrefix + id, active);
            }
        }

        private void RegisterCommands()
        {
            if (_registered)
            {
                return;
            }

            if (runner == null)
            {
                runner = FindAnyObjectByType<DialogueRunner>();
            }

            if (runner == null)
            {
                return;
            }

            runner.AddCommandHandler("task_add", (Action<string>)Add);
            runner.AddCommandHandler("task_done", (Action<string>)Complete);
            runner.AddFunction("task_active", (Func<string, bool>)IsActive);
            _registered = true;
        }
    }
}
