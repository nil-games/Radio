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

        [Header("Уровни")]
        [Tooltip("Уровни, которые идут подряд по команде start_minigame wire. Прошёл один — " +
                 "сразу следующий, поле не закрывается и музыка не прерывается. " +
                 "Один и тот же ассет можно поставить дважды: провод каждый раз новый.")]
        [SerializeField] private WirePuzzle[] wireLevels = System.Array.Empty<WirePuzzle>();

        [Header("Итог")]
        [Tooltip("Флаг мира, который ставится после победы. Пусто — ничего не ставить.")]
        [SerializeField] private string successFlag = "STORY_FIRST_BROADCAST_STARTED";

        [Tooltip("До какого времени довести часы смены после победы, «ЧЧ:ММ». " +
                 "Пусто — часы не трогать. Уже пройденную отметку часы не откатывают.")]
        [SerializeField] private string timeAfterWin = "00:30";

        private PlayerInteractor _interactor;
        private bool _pending;
        private int _level;

        private void Awake()
        {
            if (root == null || wire == null || wireLevels.Length == 0)
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
                runner.AddCommandHandler("start_minigame", (System.Action<string>)Request);
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
            if (!string.Equals(id, "wire", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"{nameof(MinigameHost)}: мини-игры {id} не существует.", this);
                return;
            }

            _pending = true;

            // Команда может прийти и вне диалога — тогда открываем сразу.
            if (dialogue == null || !dialogue.IsRunning)
            {
                StartPendingIfAny();
            }
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
            var label = wireLevels.Length > 1 ? $"Уровень {_level + 1}/{wireLevels.Length}." : null;
            wire.Begin(wireLevels[_level], label);
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
            if (++_level < wireLevels.Length)
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

            // Часы после эфира: на отметку 00:30 привязан приход гостя (DoorKnocker),
            // так что стук раздастся сразу после победы.
            AdvanceTime(session.Time);

            if (!string.IsNullOrWhiteSpace(successFlag))
            {
                session.World.Set(successFlag, true);
            }
        }

        private void AdvanceTime(TimeManager time)
        {
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
