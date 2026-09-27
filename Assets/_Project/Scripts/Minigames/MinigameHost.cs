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
    public sealed class MinigameHost : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private DialogueRunner runner;

        [SerializeField] private DialogueController dialogue;

        [Tooltip("Корень поля мини-игры. Выключен, пока в неё не играют.")]
        [SerializeField] private GameObject root;

        [SerializeField] private WirePuzzleGame wire;

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

            [Tooltip("С какого времени эфир открыт: «ЧЧ:ММ» — первая ночь, «ЧЧ:ММ:НН» — ночь НН.")]
            public string from;

            [Tooltip("Задача, которая снимается при запуске эфира. Пусто — никакая.")]
            public string task;

            [Tooltip("Уровни, которые идут подряд. Прошёл один — сразу следующий, поле не " +
                     "закрывается и музыка не прерывается. Один и тот же ассет можно поставить " +
                     "дважды: провод каждый раз новый.")]
            public WirePuzzle[] levels;

            [Tooltip("Флаг мира, который ставится после победы. Пусто — ничего не ставить.")]
            public string successFlag;

            [Tooltip("Задача, которая появляется после победы: «Поговорить со слушателями». " +
                     "Пусто — никакая.")]
            public string nextTask;

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

        private void Awake()
        {
            if (root == null || wire == null || runs.Length == 0)
            {
                Debug.LogError($"{nameof(MinigameHost)}: не заданы поле или мини-игра.", this);
                enabled = false;
                return;
            }

            root.SetActive(false);

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

            wire.Won += HandleWon;
            wire.Aborted += HandleAborted;
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

            if (runs[index].levels == null || runs[index].levels.Length == 0)
            {
                Debug.LogError($"{nameof(MinigameHost)}: у эфира {_run.id} нет уровней.", this);
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
            root.SetActive(true);
            _level = 0;
            BeginLevel();
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
            root.SetActive(false);

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
            if (++_level < _run.levels.Length)
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
        }

        private void AdvanceTime(TimeManager time)
        {
            var timeAfterWin = _run.timeAfterWin;

            if (string.IsNullOrWhiteSpace(timeAfterWin))
            {
                return;
            }

            var parts = timeAfterWin.Split(':');

            if (parts.Length != 2
                || !int.TryParse(parts[0], out var hour)
                || !int.TryParse(parts[1], out var minute))
            {
                Debug.LogError($"{nameof(MinigameHost)}: время «{timeAfterWin}» не в формате ЧЧ:ММ.", this);
                return;
            }

            time.AdvanceTo(hour, minute);
        }

        private void HandleAborted() => ReleasePlayer();
    }
}
