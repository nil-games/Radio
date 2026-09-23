using Radio.Player;
using Radio.UI;
using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// Ищет интерактивный объект под прицелом, ведёт прицел и передаёт нажатие цели.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("Прицеливание")]
        [Tooltip("Предельная длина луча прицела, м. Отсекает заведомо далёкое раньше, " +
                 "чем дело дойдёт до радиуса конкретного объекта.")]
        [SerializeField] private float aimDistance = 4f;

        [Header("Ссылки")]
        [SerializeField] private FirstPersonController movement;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Crosshair crosshair;

        private InputSystem_Actions _actions;
        private Interactable _focused;
        private Interactable _busyWith;

        // Камера сидит внутри капсулы CharacterController, поэтому луч регулярно
        // задевает самого игрока и нужно перебрать попадания, а не брать первое.
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        /// <summary>Контроллер ходьбы. Через него объекты приостанавливают управление.</summary>
        public FirstPersonController Movement => movement;

        /// <summary>Камера, висящая на игроке. Нужна, чтобы её погасить на время сидения.</summary>
        public Camera PlayerCamera => playerCamera;

        /// <summary>
        /// Камера, которая рендерит прямо сейчас. Своё поле, а не Camera.main:
        /// тот кэшируется и внутри кадра переключения отдаёт устаревшее значение.
        /// </summary>
        public Camera ActiveCamera { get; private set; }

        /// <summary>Объект под прицелом. Нужен для проверок и отладки.</summary>
        public Interactable Focused => _focused;

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

            if (movement == null || playerCamera == null || crosshair == null)
            {
                Debug.LogError($"{nameof(PlayerInteractor)}: не заданы контроллер, камера или прицел. Взаимодействие отключено.", this);
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

            // Иначе подсветка останется висеть на объекте, с которого мы больше не следим.
            SetFocus(null);
        }

        private void OnDestroy() => _actions?.Dispose();

        private void Update()
        {
            // Нажатие читаем ровно один раз за кадр. Иначе объект, который в этом же кадре
            // забрал игрока себе, тут же получил бы второе нажатие — игрок сел бы и сразу встал.
            var pressed = _actions.Player.Interact.WasPressedThisFrame();

            if (_busyWith != null)
            {
                // В режиме сидения курсор свободен, и неподвижный прицел в центре экрана
                // означал бы неправду: наводиться им уже нельзя.
                SetFocus(null);
                crosshair.SetVisible(false);

                if (pressed)
                {
                    _busyWith.Interact(this);
                }

                return;
            }

            crosshair.SetVisible(true);
            SetFocus(TryFindTarget(out var target) ? target : null);
            crosshair.SetFocused(_focused != null);

            if (_focused != null && pressed)
            {
                _focused.Interact(this);
            }
        }

        private bool TryFindTarget(out Interactable target)
        {
            target = null;

            var camera = ActiveCamera;

            if (camera == null)
            {
                return false;
            }

            var ray = new Ray(camera.transform.position, camera.transform.forward);
            return TryFindTarget(ray, out target);
        }

        /// <summary>
        /// Ищет цель вдоль луча. Луч передаётся параметром, чтобы в режиме сидя
        /// сюда же можно было отдать ScreenPointToRay от курсора.
        /// </summary>
        private bool TryFindTarget(Ray ray, out Interactable target)
        {
            target = null;

            // Бьём по всем слоям: перекрытие стеной или мебелью отрабатывает само собой,
            // без отдельной проверки видимости. Триггеры игнорируем — они служат другим
            // целям и не должны ловить прицел.
            var count = Physics.RaycastNonAlloc(ray, _hits, aimDistance, ~0, QueryTriggerInteraction.Ignore);

            if (count == 0)
            {
                return false;
            }

            // RaycastNonAlloc не сортирует, поэтому ближайшее ищем сами — заодно
            // пропуская собственную капсулу игрока, внутри которой начинается луч.
            var nearestDistance = float.MaxValue;
            Collider nearest = null;

            for (var i = 0; i < count; i++)
            {
                if (_hits[i].collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (_hits[i].distance < nearestDistance)
                {
                    nearestDistance = _hits[i].distance;
                    nearest = _hits[i].collider;
                }
            }

            if (nearest == null)
            {
                return false;
            }

            var candidate = ResolveInteractable(nearest);

            if (candidate == null || !candidate.CanInteract)
            {
                return false;
            }

            // Единственная проверка радиуса: от неё зависят и реакция прицела, и обводка,
            // и возможность нажать, поэтому разойтись они не могут.
            if (nearestDistance > candidate.InteractionRadius)
            {
                return false;
            }

            target = candidate;
            return true;
        }

        /// <summary>
        /// Находит сценарий по коллайдеру: либо компонент висит выше по иерархии,
        /// либо на геометрии стоит указатель на сценарий, живущий отдельно.
        /// </summary>
        private static Interactable ResolveInteractable(Component collider)
        {
            var direct = collider.GetComponentInParent<Interactable>();

            if (direct != null)
            {
                return direct;
            }

            var proxy = collider.GetComponentInParent<InteractableProxy>();
            return proxy != null ? proxy.Target : null;
        }

        private void SetFocus(Interactable next)
        {
            if (next == _focused)
            {
                return;
            }

            _focused?.OnFocusExit();
            _focused = next;
            _focused?.OnFocusEnter();
        }

        private void OnDrawGizmosSelected()
        {
            var camera = ActiveCamera != null ? ActiveCamera : playerCamera;

            if (camera == null)
            {
                return;
            }

            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawRay(camera.transform.position, camera.transform.forward * aimDistance);
        }
    }
}
