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
        [Tooltip("Уровень, который откроется по команде start_minigame wire.")]
        [SerializeField] private WirePuzzle wireLevel;

        [Header("Итог")]
        [Tooltip("Флаг мира, который ставится после победы. Пусто — ничего не ставить.")]
        [SerializeField] private string successFlag = "STORY_FIRST_BROADCAST_STARTED";

        private PlayerInteractor _interactor;
        private bool _pending;

        private void Awake()
        {
            if (root == null || wire == null)
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
            wire.Begin(wireLevel);
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
            ReleasePlayer();

            if (string.IsNullOrWhiteSpace(successFlag))
            {
                return;
            }

            var session = GameSession.Current;

            if (session != null)
            {
                session.World.Set(successFlag, true);
            }
        }

        private void HandleAborted() => ReleasePlayer();
    }
}
