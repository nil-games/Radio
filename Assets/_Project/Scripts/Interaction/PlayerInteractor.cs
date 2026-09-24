using System.Collections.Generic;
using Radio.Player;
using Radio.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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

        // Кто сейчас запрещает наводиться и нажимать. Набор, а не флаг: запретов
        // может быть несколько сразу, и снявший свой не должен отпускать чужой.
        private readonly HashSet<object> _inputBlocks = new HashSet<object>();

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

        /// <summary>
        /// Полностью запретить прицеливание и нажатия: игрок разговаривает и отвечает мышью.
        /// Отличается от BeginExclusive тем, что не передаёт нажатие никому.
        /// </summary>
        public void AddInputBlock(object owner) => _inputBlocks.Add(owner);

        /// <summary>Снять свой запрет. Ввод вернётся, когда снимут все.</summary>
        public void RemoveInputBlock(object owner) => _inputBlocks.Remove(owner);

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

            // Страхуемся от сцены, сохранённой с выключенной камерой игрока:
            // такое случается, если её погасили во время теста и сохранились.
            // Без этого при старте не рендерит ни одна камера.
            playerCamera.enabled = true;

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

            if (_inputBlocks.Count > 0)
            {
                // Нажатие здесь намеренно проглатывается: во время разговора клавиша
                // взаимодействия не должна поднимать игрока из кресла.
                SetFocus(null);
                crosshair.SetVisible(false);
                return;
            }

            if (_busyWith != null)
            {
                // В режиме сидения курсор свободен, и неподвижный прицел в центре экрана
                // означал бы неправду: наводятся мышью.
                crosshair.SetVisible(false);

                // Подсветка приборов на столе идёт от курсора, а не от направления взгляда:
                // голова сидящего игрока не поворачивается, и целиться ею не получится.
                SetFocus(TryFindTargetUnderCursor(out var seated) ? seated : null);

                // За столом предметы выбирают мышью. Клик по интерфейсу сюда не доходит:
                // иначе нажатие на стрелку поворота заодно трогало бы предмет под ней.
                var mouse = Mouse.current;
                var clicked = mouse != null
                              && mouse.leftButton.wasPressedThisFrame
                              && !IsPointerOverUI();

                if (clicked && _focused != null)
                {
                    _focused.Interact(this);
                }
                else if (pressed)
                {
                    // Клавиша взаимодействия по-прежнему поднимает из кресла.
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
        private static bool IsPointerOverUI() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        /// <summary>
        /// Ищет цель под курсором. Нужен в режиме за столом, где курсор свободен.
        /// </summary>
        private bool TryFindTargetUnderCursor(out Interactable target)
        {
            target = null;

            var camera = ActiveCamera;
            var mouse = Mouse.current;

            if (camera == null || mouse == null)
            {
                return false;
            }

            var screenPoint = mouse.position.ReadValue();

            // Курсор, уведённый за край окна, дал бы луч мимо экрана и подсветил бы
            // случайный предмет за кадром.
            if (screenPoint.x < 0f || screenPoint.y < 0f
                || screenPoint.x > Screen.width || screenPoint.y > Screen.height)
            {
                return false;
            }

            return TryFindTarget(camera.ScreenPointToRay(screenPoint), out target);
        }

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

            // RaycastNonAlloc порядок не гарантирует, а нам важно именно ближайшее:
            // попаданий не больше восьми, поэтому сортировка вставками здесь дешевле любой другой.
            for (var i = 1; i < count; i++)
            {
                var moved = _hits[i];
                var j = i - 1;

                while (j >= 0 && _hits[j].distance > moved.distance)
                {
                    _hits[j + 1] = _hits[j];
                    j--;
                }

                _hits[j + 1] = moved;
            }

            for (var i = 0; i < count; i++)
            {
                // Собственная капсула игрока: луч начинается внутри неё.
                if (_hits[i].collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                var candidate = ResolveInteractable(_hits[i].collider);

                // Кресло, в котором игрок сидит, стол не загораживает: он в нём сидит,
                // а не смотрит на него. Иначе за столом подсвечивалось бы только кресло.
                if (candidate != null && candidate == _busyWith)
                {
                    continue;
                }

                // Всё остальное ближайшее решает исход: непригодный предмет или стена
                // именно что заслоняют цель, и искать за ними нечего.
                if (candidate == null || !candidate.CanInteract)
                {
                    return false;
                }

                // Приборы на столе работают только в режиме за столом. Пока игрок ходит
                // по комнате, они не должны ни подсвечиваться, ни нажиматься.
                if (_busyWith == null && !candidate.AvailableWhileWalking)
                {
                    return false;
                }

                // Единственная проверка радиуса: от неё зависят и реакция прицела, и обводка,
                // и возможность нажать, поэтому разойтись они не могут.
                if (_hits[i].distance > candidate.InteractionRadius)
                {
                    return false;
                }

                target = candidate;
                return true;
            }

            return false;
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
