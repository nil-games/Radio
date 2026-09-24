using System;
using Radio.Interaction;
using Radio.Player;
using UnityEngine;
using Yarn.Unity;

namespace Radio.Dialogue
{
    /// <summary>
    /// Запускает диалоги и на время разговора приводит в порядок управление игроком.
    /// Единственная точка входа: всё остальное зовёт <see cref="StartDialogue"/>.
    /// </summary>
    /// <remarks>
    /// Поведение выбирается само, без настройки на каждом объекте.
    /// Если игрок уже остановлен кем-то другим — он сидит за столом, — приостановка чужая,
    /// и трогать её нельзя: сняв её в конце разговора, мы подняли бы игрока из кресла.
    /// Если игрок стоит, разговор останавливает его сам и освобождает курсор.
    /// В обоих случаях прицел и клавиша взаимодействия выключаются: отвечают мышью.
    /// </remarks>
    public sealed class DialogueController : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private DialogueRunner runner;

        [Tooltip("Корень панели диалога. Выключен, пока никто не говорит.")]
        [SerializeField] private GameObject panelRoot;

        [Header("Игрок")]
        [Tooltip("Если пусто, ищется в сцене при первом запуске диалога. " +
                 "Префаб не может хранить ссылку на объект сцены.")]
        [SerializeField] private PlayerInteractor interactor;

        private bool _suspendedByUs;

        /// <summary>Разговор закончился. Расписание ждёт этого, чтобы пустить следующий.</summary>
        public event Action DialogueFinished;

        public bool IsRunning => runner != null && runner.IsDialogueRunning;

        private void Awake()
        {
            if (runner == null || panelRoot == null)
            {
                Debug.LogError($"{nameof(DialogueController)}: не заданы Dialogue Runner или панель. " +
                               "Диалоги отключены.", this);
                enabled = false;
                return;
            }

            panelRoot.SetActive(false);

            // UnityEvent в инспекторе оставляем пустыми: по соглашению проекта всё
            // подключается кодом. Поля могут быть не созданы — Yarn объявляет их обнуляемыми.
            runner.onDialogueComplete ??= new UnityEngine.Events.UnityEvent();
            runner.onDialogueComplete.AddListener(HandleDialogueComplete);
        }

        private void OnDestroy()
        {
            if (runner != null && runner.onDialogueComplete != null)
            {
                runner.onDialogueComplete.RemoveListener(HandleDialogueComplete);
            }
        }

        /// <summary>
        /// Начать разговор с указанного узла Yarn. Повторный вызов во время разговора
        /// игнорируется: очередь держит тот, кто её завёл, иначе реплики наложились бы.
        /// </summary>
        public void StartDialogue(string node)
        {
            if (!enabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(node))
            {
                Debug.LogError($"{nameof(DialogueController)}: пустое имя узла.", this);
                return;
            }

            if (runner.IsDialogueRunning)
            {
                Debug.LogWarning($"{nameof(DialogueController)}: узел {node} пропущен, " +
                                 "разговор уже идёт.", this);
                return;
            }

            TakeOverPlayer();
            panelRoot.SetActive(true);

            // Задача намеренно не ожидается: конец разговора приходит событием,
            // а ждать его здесь означало бы заморозить вызывающего.
            _ = runner.StartDialogue(node);
        }

        /// <summary>Закрыть разговор досрочно — крестик в углу панели.</summary>
        public void RequestClose()
        {
            if (runner != null && runner.IsDialogueRunning)
            {
                _ = runner.Stop();
            }
        }

        private void TakeOverPlayer()
        {
            EnsureInteractor();

            if (interactor == null)
            {
                return;
            }

            // Прицел и клавиша взаимодействия молчат весь разговор.
            interactor.AddInputBlock(this);

            var movement = interactor.Movement;

            if (movement == null || movement.IsSuspended)
            {
                // Игрок уже сидит: приостановка чужая, курсор и так свободен.
                _suspendedByUs = false;
                return;
            }

            movement.AddSuspendRequest(this);
            _suspendedByUs = true;
        }

        private void ReleasePlayer()
        {
            if (interactor == null)
            {
                return;
            }

            interactor.RemoveInputBlock(this);

            if (_suspendedByUs && interactor.Movement != null)
            {
                interactor.Movement.RemoveSuspendRequest(this);
            }

            _suspendedByUs = false;
        }

        private void EnsureInteractor()
        {
            if (interactor != null)
            {
                return;
            }

            interactor = FindFirstObjectByType<PlayerInteractor>();

            if (interactor == null)
            {
                Debug.LogWarning($"{nameof(DialogueController)}: в сцене нет {nameof(PlayerInteractor)}. " +
                                 "Диалог покажется, но управление останется у игрока.", this);
            }
        }

        private void HandleDialogueComplete()
        {
            ReleasePlayer();
            panelRoot.SetActive(false);
            DialogueFinished?.Invoke();
        }
    }
}
