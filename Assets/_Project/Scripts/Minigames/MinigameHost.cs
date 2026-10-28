using System;
using Radio.Dialogue;
using Radio.Interaction;
using Radio.World;
using UnityEngine;
using Yarn.Unity;

namespace Radio.Minigames
{
    /// <summary>
    /// Запускает мини-игры по команде из диалога и возвращает управление игроку,
    /// когда игра закончилась.
    /// </summary>
    /// <remarks>
    /// Команда не открывает поле сразу: она только запоминает просьбу, а поле
    /// появляется, когда диалог закроется. Иначе сетка легла бы поверх ещё идущего
    /// разговора, и игрок читал бы реплику сквозь провод.
    /// </remarks>
    public sealed class MinigameHost : MonoBehaviour, ISaveBlocker
    {
        [Header("Ссылки")]
        [SerializeField] private DialogueRunner runner;

        [SerializeField] private DialogueController dialogue;

        [Tooltip("Корень поля мини-игры. Выключен, пока в неё не играют.")]
        [SerializeField] private GameObject root;

        [SerializeField] private WirePuzzleGame wire;

        [Tooltip("Эфир за столом (GDD 02.22). Им сейчас идут все выходы в эфир.")]
        [SerializeField] private Radio.Broadcast.BroadcastGame broadcast;

        /// <summary>Чем играется запуск.</summary>
        public enum RunKind
        {
            [Tooltip("Эфир за столом: заявки слушателей и кассеты (GDD 02.22).")]
            Broadcast = 0,

            [Tooltip("«Провода»: поле-клавиатура с проводом (бывший «Эфир», GDD 02.3). " +
                     "Сейчас не используется, ждёт другого применения.")]
            Wire = 1,
        }

        /// <summary>
        /// Один эфир ночи: с какого времени он открыт, какие уровни идут подряд
        /// и что меняется в мире после победы.
        /// </summary>
        /// <remarks>
        /// Эфиров за ночь несколько, и сложность у каждого своя. Игрок эфир не выбирает:
        /// команда start_minigame wire берёт тот, что открыт по часам смены и ещё не пройден.
        /// Между эфирами «Начать эфир» на микшере погашен (функция broadcast_ready).
        /// </remarks>
        [Serializable]
        public struct WireRun
        {
            [Tooltip("Для логов и инспектора. В игре не показывается.")]
            public string id;

            [Tooltip("Что открывается: эфир за столом или «Провода».")]
            public RunKind kind;

            [Tooltip("С какого времени эфир открыт: «ЧЧ:ММ» — первая ночь, «ЧЧ:ММ:НН» — ночь НН.")]
            public string from;

            [Tooltip("Задача, которая снимается при запуске эфира. Пусто — никакая.")]
            public string task;

            [Tooltip("Только для «Проводов»: уровни, которые идут подряд. Прошёл один — сразу " +
                     "следующий, поле не закрывается и музыка не прерывается. Один и тот же ассет " +
                     "можно поставить дважды: провод каждый раз новый.")]
            public WirePuzzle[] levels;

            [Tooltip("Флаг мира, который ставится после победы. Пусто — ничего не ставить.")]
            public string successFlag;

            [Tooltip("Задача, которая появляется после победы: «Поговорить со слушателями». " +
                     "Пусто — никакая.")]
            public string nextTask;

            [Tooltip("Узел диалога сразу после эфира (погасить «В ЭФИРЕ», мысль героя, " +
                     "следующая задача). Пусто — без разговора.")]
            public string afterNode;

            [Tooltip("До какого времени довести часы смены после победы, «ЧЧ:ММ». " +
                     "Пусто — часы не трогать. Уже пройденную отметку часы не откатывают.")]
            public string timeAfterWin;
        }

        [Header("Запуски")]
        [SerializeField] private WireRun[] runs = Array.Empty<WireRun>();

        private PlayerInteractor _interactor;
        private bool _pending;
        private WireRun _run;
        private int _level;

        /// <summary>Мини-игру попросили, но она ещё не началась — сохраниться посреди нельзя.</summary>
        public bool BlocksSave => _pending;

        private void Awake()
        {
            if ((wire == null && broadcast == null) || runs.Length == 0)
            {
                Debug.LogError($"{nameof(MinigameHost)}: не заданы мини-игры или запуски.", this);
                enabled = false;
                return;
            }

            if (root != null)
            {
                root.SetActive(false);
            }

            if (runner == null)
            {
                runner = FindFirstObjectByType<DialogueRunner>();
            }

            if (dialogue == null)
            {
                dialogue = FindFirstObjectByType<DialogueController>();
            }

            if (runner != null)
            {
                runner.AddCommandHandler("start_minigame", (Action<string>)Request);
                runner.AddFunction("broadcast_ready", (Func<bool>)(() => FindOpenRun() >= 0));
            }

            if (dialogue != null)
            {
                dialogue.DialogueFinished += StartPendingIfAny;
            }

            if (wire != null)
            {
                wire.Won += HandleWon;
                wire.Aborted += HandleAborted;
            }

            if (broadcast != null)
            {
                broadcast.Finished += HandleBroadcastFinished;
            }
        }

        private void OnDestroy()
        {
            if (dialogue != null)
            {
                dialogue.DialogueFinished -= StartPendingIfAny;
            }

            if (wire != null)
            {
                wire.Won -= HandleWon;
                wire.Aborted -= HandleAborted;
            }

            if (broadcast != null)
            {
                broadcast.Finished -= HandleBroadcastFinished;
            }
        }

        /// <summary>Команда из диалога: start_minigame wire.</summary>
        private void Request(string id)
        {
            if (!string.Equals(id, "wire", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"{nameof(MinigameHost)}: мини-игры {id} не существует.", this);
                return;
            }

            var index = FindOpenRun();

            if (index < 0)
            {
                Debug.LogError($"{nameof(MinigameHost)}: сейчас нет открытого эфира.", this);
                return;
            }

            _run = runs[index];

            if (_run.kind == RunKind.Broadcast && broadcast == null)
            {
                Debug.LogError($"{nameof(MinigameHost)}: эфир {_run.id} — эфир за столом, но он не подключён.", this);
                return;
            }

            if (_run.kind == RunKind.Wire && (wire == null || root == null || _run.levels == null || _run.levels.Length == 0))
            {
                Debug.LogError($"{nameof(MinigameHost)}: у «Проводов» {_run.id} нет поля или уровней.", this);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_run.task))
            {
                var tasks = FindAnyObjectByType<TaskLog>();

                if (tasks != null)
                {
                    tasks.Complete(_run.task);
                }
            }
            _pending = true;

            // Команда может прийти и вне диалога — тогда открываем сразу.
            if (dialogue == null || !dialogue.IsRunning)
            {
                StartPendingIfAny();
            }
        }

        /// <summary>
        /// Эфир, открытый по часам и ещё не пройденный. Если таких несколько, берётся
        /// самый поздний: ранний эфир, пропущенный отладочным переводом часов, уже неактуален.
        /// Минус один — открытого эфира нет.
        /// </summary>
        private int FindOpenRun()
        {
            var session = GameSession.Current;

            if (session == null)
            {
                return -1;
            }

            var best = -1;
            var bestFrom = -1;

            for (var i = 0; i < runs.Length; i++)
            {
                var run = runs[i];

                if (!new GameTimeMark(run.from).TryGetTotalMinutes(out var from))
                {
                    from = 0;
                }

                if (from > session.Time.TotalMinutes || from < bestFrom)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(run.successFlag)
                    && session.World.TryGet<bool>(run.successFlag, out var done) && done)
                {
                    continue;
                }

                best = i;
                bestFrom = from;
            }

            return best;
        }

        private void StartPendingIfAny()
        {
            if (!_pending)
            {
                return;
            }

            _pending = false;
            TakeOverPlayer();

            if (_run.kind == RunKind.Broadcast)
            {
                BeginBroadcast();
                return;
            }

            root.SetActive(true);
            _level = 0;
            BeginLevel();
        }

        /// <summary>
        /// Часы на экране эфира идут полчаса от начала эфира (эфир — 30 игровых минут,
        /// решение автора 28.10.2026). Куда часы смены встанут после эфира — отдельно,
        /// «Время после победы»: пустые получасы (свободные окна) перескакиваются.
        /// </summary>
        private void BeginBroadcast()
        {
            var session = GameSession.Current;
            var night = session != null ? session.Time.Night : 1;
            var now = session != null ? session.Time.Minutes : 0;
            broadcast.Begin(night, BroadcastNumber(_run), now, now + BroadcastMinutes);
        }

        /// <summary>Сколько игровых минут идёт эфир на экранных часах.</summary>
        public const int BroadcastMinutes = 30;

        /// <summary>
        /// Какой это по счёту эфир своей ночи (1, 2, 3…): по нему лист «Сложность»
        /// выбирает строку. Считается по отметкам «с какого времени» в списке запусков.
        /// </summary>
        private int BroadcastNumber(WireRun run)
        {
            if (!new GameTimeMark(run.from).TryGetTotalMinutes(out var total))
            {
                return 1;
            }

            var night = total / TimeManager.ShiftEndMinutes;
            var number = 1;

            foreach (var other in runs)
            {
                if (other.kind == RunKind.Broadcast
                    && new GameTimeMark(other.from).TryGetTotalMinutes(out var otherTotal)
                    && otherTotal / TimeManager.ShiftEndMinutes == night
                    && otherTotal < total)
                {
                    number++;
                }
            }

            return number;
        }

        /// <summary>
        /// Отладка (правый Ctrl, затем B и номер): сразу открыть эфир номер
        /// <paramref name="number"/> текущей ночи, со сложностью этого эфира. Без микшера,
        /// без задач и без последствий: флаг, задачи и часы после него не меняются.
        /// </summary>
        public void DebugStartBroadcast(int number)
        {
            if (broadcast == null || broadcast.Phase != Radio.Broadcast.BroadcastPhase.Idle)
            {
                return;
            }

            var session = GameSession.Current;
            var night = session != null ? session.Time.Night : 1;
            var now = session != null ? session.Time.Minutes : 0;
            _debugRun = true;
            _pending = false;
            TakeOverPlayer();
            broadcast.Begin(night, Mathf.Max(1, number), now, now + BroadcastMinutes);
            Debug.Log($"{nameof(MinigameHost)}: отладка — эфир {number} ночи {night}.", this);
        }

        private bool _debugRun;

        /// <summary>
        /// Отладка (правый Ctrl, затем P): засчитать эфир пройденным. Идёт эфир — он
        /// заканчивается сразу, с набранными итогами. Эфир не начат, но открыт по часам —
        /// засчитывается без игры. Дальше всё как после настоящего эфира: флаг эфира,
        /// задача снята, часы сдвинуты, узел «после эфира». Возвращает, что сделано.
        /// </summary>
        public string DebugCompleteBroadcast()
        {
            if (broadcast != null && broadcast.Phase != Radio.Broadcast.BroadcastPhase.Idle)
            {
                broadcast.DebugFinishNow();
                return "эфир закончен досрочно";
            }

            var index = FindOpenRun();

            if (index < 0 || runs[index].kind != RunKind.Broadcast)
            {
                return "сейчас нет открытого эфира";
            }

            _run = runs[index];
            _pending = false;

            if (!string.IsNullOrWhiteSpace(_run.task))
            {
                var tasks = FindAnyObjectByType<TaskLog>();

                if (tasks != null)
                {
                    tasks.Complete(_run.task);
                }
            }

            HandleWon();
            return $"эфир {_run.id} засчитан без игры";
        }

        private void HandleBroadcastFinished(Radio.Broadcast.BroadcastTally tally)
        {
            if (_debugRun)
            {
                _debugRun = false;
                ReleasePlayer();
                return;
            }

            // Баллы и деньги эфира копятся во флагах мира: их читает утренний разговор
            // (зарплата), а позже — шкалы сюжета (GDD 02.22). Остальные последствия
            // пока только в сводке. Дальше — как после победы в «Проводах».
            var world = GameSession.Current != null ? GameSession.Current.World : null;

            if (world != null)
            {
                world.TryGet<float>("BROADCAST_POINTS", out var points);
                world.TryGet<float>("MONEY", out var money);
                world.Set("BROADCAST_POINTS", points + tally.Points);
                world.Set("MONEY", money + tally.Money);
            }

            HandleWon();
        }

        private static bool TryParseClock(string value, out int hour, out int minute)
        {
            hour = minute = 0;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var parts = value.Split(':');
            return parts.Length >= 2 && int.TryParse(parts[0], out hour) && int.TryParse(parts[1], out minute);
        }

        private void BeginLevel()
        {
            // Номер уровня показываем, только когда их несколько: «1/1» ничего не сообщает.
            var label = _run.levels.Length > 1 ? $"Уровень {_level + 1}/{_run.levels.Length}." : null;
            wire.Begin(_run.levels[_level], label);
        }

        private void TakeOverPlayer()
        {
            if (_interactor == null)
            {
                _interactor = FindFirstObjectByType<PlayerInteractor>();
            }

            if (_interactor == null)
            {
                return;
            }

            // Тот же счётчик владельцев, что у кресла и диалога: мини-игра отпустит
            // только своё, и игрок не встанет из кресла, когда она закончится.
            _interactor.AddInputBlock(this);

            if (_interactor.Movement != null)
            {
                _interactor.Movement.AddSuspendRequest(this);
            }
        }

        private void ReleasePlayer()
        {
            if (root != null)
            {
                root.SetActive(false);
            }

            if (_interactor == null)
            {
                return;
            }

            _interactor.RemoveInputBlock(this);

            if (_interactor.Movement != null)
            {
                _interactor.Movement.RemoveSuspendRequest(this);
            }
        }

        private void HandleWon()
        {
            // Поле не гасим между уровнями: на нём висит музыка, и она должна
            // играть сквозь всю мини-игру, а не начинаться заново.
            if (_run.kind == RunKind.Wire && ++_level < _run.levels.Length)
            {
                BeginLevel();
                return;
            }

            ReleasePlayer();

            var session = GameSession.Current;

            if (session == null)
            {
                return;
            }

            // Часы после эфира: например, на 00:30 привязан приход гостя (DoorKnocker),
            // так что стук раздастся сразу после победы в первом эфире.
            AdvanceTime(session.Time);

            if (!string.IsNullOrWhiteSpace(_run.successFlag))
            {
                session.World.Set(_run.successFlag, true);
            }

            if (!string.IsNullOrWhiteSpace(_run.nextTask))
            {
                var tasks = FindAnyObjectByType<TaskLog>();

                if (tasks != null)
                {
                    tasks.Add(_run.nextTask);
                }
            }

            if (!string.IsNullOrWhiteSpace(_run.afterNode) && dialogue != null)
            {
                dialogue.StartDialogue(_run.afterNode);
            }
        }

        private void AdvanceTime(TimeManager time)
        {
            var timeAfterWin = _run.timeAfterWin;

            if (string.IsNullOrWhiteSpace(timeAfterWin))
            {
                return;
            }

            if (!TryParseClock(timeAfterWin, out var hour, out var minute))
            {
                Debug.LogError($"{nameof(MinigameHost)}: время «{timeAfterWin}» не в формате ЧЧ:ММ.", this);
                return;
            }

            time.AdvanceTo(hour, minute);
        }

        private void HandleAborted() => ReleasePlayer();
    }
}
