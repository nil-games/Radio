using Radio.Player;
using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// Ищет ближайший объект взаимодействия вокруг игрока, показывает подсказку
    /// и передаёт нажатие выбранному объекту.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("Поиск цели")]
        [Tooltip("Радиус поиска вокруг игрока, м. Подсказка появляется, когда объект попал в этот радиус.")]
        [SerializeField] private float searchRadius = 1.4f;

        [Tooltip("Высота точки поиска над основанием игрока, м. Совпадает с центром капсулы CharacterController.")]
        [SerializeField] private float probeHeight = 0.9f;

        [Header("Ссылки")]
        [SerializeField] private FirstPersonController movement;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InteractionPrompt prompt;

        private InputSystem_Actions _actions;
        private readonly Collider[] _hits = new Collider[16];
        private Interactable _focused;
        private Interactable _busyWith;

        /// <summary>Контроллер ходьбы. Через него объекты приостанавливают управление.</summary>
        public FirstPersonController Movement => movement;

        /// <summary>Камера, висящая на игроке. Нужна, чтобы её погасить на время сидения.</summary>
        public Camera PlayerCamera => playerCamera;

        /// <summary>
        /// Камера, которая рендерит прямо сейчас. Своё поле, а не Camera.main:
        /// тот кэшируется и внутри кадра переключения отдаёт устаревшее значение.
        /// </summary>
        public Camera ActiveCamera { get; private set; }

        public void SetActiveCamera(Camera camera) => ActiveCamera = camera;

        /// <summary>Объект забирает игрока себе: поиск других целей прекращается.</summary>
        public void BeginExclusive(Interactable owner) => _busyWith = owner;

        /// <summary>Объект отпускает игрока.</summary>
        public void EndExclusive(Interactable owner)
        {
            if (_busyWith == owner)
            {
                _busyWith = null;
            }
        }

        private void Awake()
        {
            if (movement == null)
            {
                movement = GetComponent<FirstPersonController>();
            }

            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
            }

            if (movement == null || playerCamera == null || prompt == null)
            {
                Debug.LogError($"{nameof(PlayerInteractor)}: не заданы контроллер, камера или подсказка. Взаимодействие отключено.", this);
                enabled = false;
                return;
            }

            ActiveCamera = playerCamera;
            _actions = new InputSystem_Actions();
        }

        private void OnEnable() => _actions?.Player.Enable();

        private void OnDisable()
        {
            _actions?.Player.Disable();
            prompt?.Hide();
        }

        private void OnDestroy() => _actions?.Dispose();

        private void Update()
        {
            // Нажатие читаем ровно один раз за кадр. Иначе объект, который в этом же кадре
            // забрал игрока себе, тут же получил бы второе нажатие — игрок сел бы и сразу встал.
            var pressed = _actions.Player.Interact.WasPressedThisFrame();

            if (_busyWith != null)
            {
                prompt.Hide();

                if (pressed)
                {
                    _busyWith.Interact(this);
                }

                return;
            }

            var target = FindNearest();

            if (target != _focused)
            {
                _focused?.OnFocusExit();
                _focused = target;
                _focused?.OnFocusEnter();
            }

            if (_focused == null)
            {
                prompt.Hide();
                return;
            }

            prompt.Show(_focused.PromptAnchor.position, _focused.PromptText);

            if (pressed)
            {
                _focused.Interact(this);
            }
        }

        /// <summary>
        /// Ближайший доступный объект в радиусе. Запрос каждый кадр вместо OnTriggerEnter:
        /// игрок ходит на CharacterController без Rigidbody, а запрос не зависит от порядка
        /// событий и бесплатно даёт «ближайший из нескольких» — это понадобится на пульте.
        /// </summary>
        private Interactable FindNearest()
        {
            var center = transform.position + Vector3.up * probeHeight;
            var count = Physics.OverlapSphereNonAlloc(center, searchRadius, _hits, ~0, QueryTriggerInteraction.Collide);

            Interactable best = null;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < count; i++)
            {
                // Коллайдер может висеть на ребёнке, а компонент — на корне объекта.
                var candidate = _hits[i].GetComponentInParent<Interactable>();

                if (candidate == null || !candidate.CanInteract)
                {
                    continue;
                }

                var distance = Vector3.SqrMagnitude(candidate.PromptAnchor.position - center);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * probeHeight, searchRadius);
        }
    }
}
