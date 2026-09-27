using System;
using Radio.Dialogue;
using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// Входная дверь квартиры. По нажатию игрок встаёт у двери и смотрит на лестничную
    /// площадку через отдельную камеру, курсор освобождается, а дверь открывается.
    /// Повторное нажатие возвращает ходьбу и закрывает дверь.
    /// </summary>
    public class EntranceDoor : Interactable
    {
        [Header("Вид на площадку")]
        [Tooltip("Камера, смотрящая из квартиры на лестничную площадку. В сцене должна быть " +
                 "выключена — её включает этот скрипт.")]
        [SerializeField] private Camera doorCamera;

        [Header("Открывание")]
        [Tooltip("Ось петель: пустой объект на петлевом краю проёма, внутри которого лежит полотно. " +
                 "Поворачивается он, а не модель: пивот модели стоит там, где его оставил экспорт.")]
        [SerializeField] private Transform hinge;

        [Tooltip("Угол открытия, градусов. Знак задаёт сторону: положительный — поворот " +
                 "по часовой, если смотреть сверху.")]
        [SerializeField] private float openAngle = 95f;

        [Tooltip("Сколько длится открытие или закрытие, с.")]
        [SerializeField] private float openDuration = 0.8f;

        /// <summary>
        /// Гость за дверью: кто стучит и какой разговор начинается, когда открыли.
        /// </summary>
        /// <remarks>
        /// Гостей за ночь несколько: бабка Зина в 00:30, кошка в 04:00. У каждого свой
        /// <see cref="DoorKnocker"/> со временем прихода и звуком, дверь просто смотрит,
        /// кто из них сейчас ждёт.
        /// </remarks>
        [Serializable]
        public struct Visitor
        {
            [Tooltip("Кто стучит. Если он сейчас ждёт, открытие двери начинает его разговор.")]
            public DoorKnocker knocker;

            [Tooltip("Узел .yarn с разговором у порога.")]
            public string node;
        }

        [Header("Гости")]
        [Tooltip("Пусто — дверь просто открывается.")]
        [SerializeField] private Visitor[] visitors = Array.Empty<Visitor>();

        [Tooltip("Если пусто, ищется в сцене.")]
        [SerializeField] private DialogueController dialogue;

        private Quaternion _closedRotation;
        private PlayerInteractor _interactor;
        private bool _visitorPending;
        private string _visitorNode;
        private bool _visitorTalking;
        private float _visitorTimer;
        private float _progress;
        private float _target;
        private bool _looking;

        /// <summary>Пока игрок смотрит в проём, он занят дверью: следующее нажатие отпустит его.</summary>
        public override bool IsExclusive => true;

        /// <summary>Дверь открыта или открывается.</summary>
        public bool IsOpen => _target > 0f;

        protected override void Awake()
        {
            base.Awake();

            if (doorCamera == null || hinge == null)
            {
                Debug.LogError($"{nameof(EntranceDoor)}: не заданы камера или ось петель. Дверь отключена.", this);
                enabled = false;
                return;
            }

            // Страхуемся от сохранённого в сцене включённого состояния: двух активных камер быть не должно.
            doorCamera.enabled = false;

            _closedRotation = hinge.localRotation;
        }

        public override void Interact(PlayerInteractor interactor)
        {
            if (_looking)
            {
                Leave(interactor);
            }
            else
            {
                Enter(interactor);
            }
        }

        private void Enter(PlayerInteractor interactor)
        {
            // Курсором владеет контроллер: он же его и освобождает.
            interactor.Movement.AddSuspendRequest(this);

            // Порядок важен: сперва гасим старую камеру, потом зажигаем новую.
            // Иначе будет кадр с двумя активными MainCamera и недетерминированный Camera.main.
            interactor.PlayerCamera.enabled = false;
            doorCamera.enabled = true;
            interactor.SetActiveCamera(doorCamera);

            interactor.BeginExclusive(this);
            _looking = true;
            _target = 1f;
            _interactor = interactor;

            foreach (var visitor in visitors)
            {
                if (visitor.knocker == null || !visitor.knocker.IsKnocking)
                {
                    continue;
                }

                // Разговор начинается, когда дверь уже открыта: гость не заговорит сквозь полотно.
                visitor.knocker.Stop();
                _visitorNode = visitor.node;
                _visitorPending = true;
                _visitorTimer = openDuration;
                break;
            }
        }

        private void Leave(PlayerInteractor interactor)
        {
            doorCamera.enabled = false;
            interactor.PlayerCamera.enabled = true;
            interactor.SetActiveCamera(interactor.PlayerCamera);

            interactor.Movement.RemoveSuspendRequest(this);

            interactor.EndExclusive(this);
            _looking = false;
            _target = 0f;

            // Закрыли дверь, не дождавшись разговора, — гость остаётся за дверью
            // и постучит снова: визит ещё не состоялся.
            _visitorPending = false;
        }

        private void Update()
        {
            if (!_visitorPending)
            {
                return;
            }

            _visitorTimer -= Time.unscaledDeltaTime;

            if (_visitorTimer > 0f)
            {
                return;
            }

            _visitorPending = false;
            StartVisitorDialogue();
        }

        private void StartVisitorDialogue()
        {
            if (dialogue == null)
            {
                dialogue = FindAnyObjectByType<DialogueController>();
            }

            if (dialogue == null || string.IsNullOrWhiteSpace(_visitorNode))
            {
                Debug.LogError($"{nameof(EntranceDoor)}: гость пришёл, но нет диалога или узла.", this);
                return;
            }

            _visitorTalking = true;
            dialogue.DialogueFinished += HandleVisitorGone;
            dialogue.StartDialogue(_visitorNode);
        }

        private void HandleVisitorGone()
        {
            dialogue.DialogueFinished -= HandleVisitorGone;

            if (!_visitorTalking)
            {
                return;
            }

            _visitorTalking = false;

            // Гость ушёл — игрок закрывает дверь сам, отдельного нажатия не нужно.
            if (_looking && _interactor != null)
            {
                Leave(_interactor);
            }
        }

        private void OnDestroy()
        {
            if (dialogue != null)
            {
                dialogue.DialogueFinished -= HandleVisitorGone;
            }
        }

        private void LateUpdate()
        {
            if (Mathf.Approximately(_progress, _target))
            {
                return;
            }

            // Идём к цели с текущего места, а не с начала: нажатие посреди открытия
            // просто разворачивает дверь обратно, без скачка к крайнему положению.
            // unscaledDeltaTime: дверь должна двигаться и когда игра на паузе.
            var step = Time.unscaledDeltaTime / Mathf.Max(0.0001f, openDuration);
            _progress = Mathf.MoveTowards(_progress, _target, step);

            // SmoothStep убирает рывки на старте и в конце — тяжёлое полотно,
            // а не створка на пружине.
            var angle = openAngle * Mathf.SmoothStep(0f, 1f, _progress);
            hinge.localRotation = _closedRotation * Quaternion.AngleAxis(angle, Vector3.up);
        }
    }
}
