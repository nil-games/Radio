using UnityEngine;

namespace Radio.Player
{
    /// <summary>
    /// Управление персонажем от первого лица: перемещение на WASD, обзор мышью.
    /// Требует CharacterController на том же объекте и камеру-потомка на уровне глаз.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Перемещение")]
        [Tooltip("Скорость обычной ходьбы, м/с. Персонаж на работе, а не на пробежке — держим спокойный темп.")]
        [SerializeField] private float walkSpeed = 2.2f;

        [Tooltip("Время выхода на полную скорость и остановки, с. Убирает рывки при старте и стопе.")]
        [SerializeField] private float acceleration = 12f;

        [Tooltip("Гравитация, м/с². Нужна, чтобы персонаж прижимался к полу и корректно шёл по ступеням и наклонам.")]
        [SerializeField] private float gravity = -19.62f;

        [Header("Обзор")]
        [Tooltip("Чувствительность мыши, градусов на единицу ввода.")]
        [SerializeField] private float lookSensitivity = 0.12f;

        [Tooltip("Предел наклона головы вверх и вниз, градусов. Не даёт вывернуть шею и увидеть мир вверх ногами.")]
        [SerializeField] private float pitchLimit = 85f;

        [Tooltip("Инвертировать вертикальную ось обзора.")]
        [SerializeField] private bool invertY;

        [Header("Ссылки")]
        [Tooltip("Камера на уровне глаз. Если не задана, будет найдена среди потомков.")]
        [SerializeField] private Transform cameraPivot;

        private CharacterController _controller;
        private InputSystem_Actions _actions;

        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _pitch;
        private bool _suspended;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (cameraPivot == null)
            {
                var childCamera = GetComponentInChildren<Camera>();
                if (childCamera != null)
                {
                    cameraPivot = childCamera.transform;
                }
            }

            if (cameraPivot == null)
            {
                Debug.LogError($"{nameof(FirstPersonController)}: не найдена камера. Обзор мышью работать не будет.", this);
                enabled = false;
                return;
            }

            _actions = new InputSystem_Actions();
            _pitch = cameraPivot.localEulerAngles.x;
        }

        private void OnEnable()
        {
            _actions?.Player.Enable();
            SetCursorLocked(!_suspended);
        }

        private void OnDisable()
        {
            _actions?.Player.Disable();
            SetCursorLocked(false);
        }

        private void OnDestroy()
        {
            _actions?.Dispose();
        }

        /// <summary>Приостановлено ли управление: игрок сидит, смотрит в меню и т.п.</summary>
        public bool IsSuspended => _suspended;

        /// <summary>
        /// Приостанавливает ходьбу и обзор, не выключая компонент.
        /// enabled = false здесь не годится: OnDisable снял бы всю карту Player вместе
        /// с действием Interact, которым игрок и выходит обратно.
        /// </summary>
        public void SetSuspended(bool suspended)
        {
            if (_suspended == suspended)
            {
                return;
            }

            _suspended = suspended;

            // Гасим инерцию: иначе после вставания персонаж доедет разгон, набранный до посадки.
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = suspended ? 0f : -2f;

            SetCursorLocked(!suspended);
        }

        private void Update()
        {
            if (_suspended)
            {
                return;
            }

            HandleLook();
            HandleMove();
        }

        private void HandleLook()
        {
            // В редакторе курсор освобождается по Esc, поэтому обзор крутим только когда он захвачен.
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            var look = _actions.Player.Look.ReadValue<Vector2>() * lookSensitivity;

            // Поворот корпуса вокруг вертикальной оси — вместе с ним едет и камера.
            transform.Rotate(Vector3.up, look.x, Space.Self);

            // Наклон головы накапливаем отдельно, иначе на границе диапазона углы «схлопываются».
            _pitch += invertY ? look.y : -look.y;
            _pitch = Mathf.Clamp(_pitch, -pitchLimit, pitchLimit);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void HandleMove()
        {
            var input = _actions.Player.Move.ReadValue<Vector2>();
            var wish = transform.right * input.x + transform.forward * input.y;

            // Нормализуем только при перегрузе по длине: иначе геймпадный полунаклон стика превратится в полный ход.
            if (wish.sqrMagnitude > 1f)
            {
                wish.Normalize();
            }

            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity,
                wish * walkSpeed,
                acceleration * Time.deltaTime);

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                // Небольшой прижим к полу вместо нуля: с чистым нулём isGrounded моргает на стыках коллайдеров.
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            var velocity = _horizontalVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
