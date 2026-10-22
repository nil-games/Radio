using Radio.Player;
using UnityEngine;

namespace Radio.Dream
{
    /// <summary>
    /// Управление во сне (Сон-Изнанка, GDD 02.21б): бег, прыжок, двойной прыжок.
    /// Отдельно от квартирного FirstPersonController: там спокойная ходьба без прыжков,
    /// здесь платформинг, и смешивать их — значит обвешать оба условиями.
    /// </summary>
    /// <remarks>
    /// Прощающий прыжок: прыгнуть можно чуть после края платформы («койот») и нажать
    /// чуть раньше приземления (буфер) — от первого лица края видны плохо, и без этого
    /// платформинг раздражает.
    /// </remarks>
    [RequireComponent(typeof(CharacterController))]
    public sealed class DreamController : MonoBehaviour
    {
        [Header("Бег")]
        [SerializeField] private float runSpeed = 5f;

        [Tooltip("Разгон на земле, м/с².")]
        [SerializeField] private float groundAcceleration = 40f;

        [Tooltip("Разгон в воздухе, м/с²: меньше, чем на земле, но направление поправить можно.")]
        [SerializeField] private float airAcceleration = 18f;

        [Header("Прыжок")]
        [Tooltip("Высота прыжка, м.")]
        [SerializeField] private float jumpHeight = 1.3f;

        [Tooltip("Высота второго прыжка в воздухе, м.")]
        [SerializeField] private float doubleJumpHeight = 1.1f;

        [Tooltip("Двойной прыжок открыт (навык сна).")]
        [SerializeField] private bool doubleJumpUnlocked = true;

        [SerializeField] private float gravity = -25f;

        [Tooltip("Сколько можно прыгнуть после схода с края, с.")]
        [SerializeField] private float coyoteTime = 0.12f;

        [Tooltip("Сколько помнится нажатие прыжка до приземления, с.")]
        [SerializeField] private float jumpBuffer = 0.12f;

        [Header("Обзор")]
        [SerializeField] private float lookSensitivity = 0.12f;
        [SerializeField] private float pitchLimit = 85f;
        [SerializeField] private Transform cameraPivot;

        private CharacterController _controller;
        private InputSystem_Actions _actions;
        private Vector3 _horizontal;
        private float _vertical;
        private float _pitch;
        private float _sinceGrounded;
        private float _sinceJumpPressed = 99f;
        private bool _usedDoubleJump;

        public bool DoubleJumpUnlocked
        {
            get => doubleJumpUnlocked;
            set => doubleJumpUnlocked = value;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (cameraPivot == null)
            {
                var camera = GetComponentInChildren<Camera>();
                cameraPivot = camera != null ? camera.transform : null;
            }

            _actions = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            _actions.Player.Enable();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            _actions.Player.Disable();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDestroy() => _actions?.Dispose();

        /// <summary>Перенести игрока (возврат к точке после падения).</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
            _controller.enabled = true;
            _horizontal = Vector3.zero;
            _vertical = 0f;
        }

        private void Update()
        {
            Look();
            Move();
        }

        private void Look()
        {
            if (cameraPivot == null || Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            var look = _actions.Player.Look.ReadValue<Vector2>() * lookSensitivity;
            transform.Rotate(Vector3.up, look.x, Space.Self);
            _pitch = Mathf.Clamp(_pitch - look.y, -pitchLimit, pitchLimit);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void Move()
        {
            var dt = Time.deltaTime;
            var input = _actions.Player.Move.ReadValue<Vector2>();
            var wish = transform.right * input.x + transform.forward * input.y;

            if (wish.sqrMagnitude > 1f)
            {
                wish.Normalize();
            }

            var grounded = _controller.isGrounded;
            var accel = grounded ? groundAcceleration : airAcceleration;
            _horizontal = Vector3.MoveTowards(_horizontal, wish * runSpeed, accel * dt);

            if (grounded)
            {
                _sinceGrounded = 0f;
                _usedDoubleJump = false;

                if (_vertical < 0f)
                {
                    _vertical = -2f;
                }
            }
            else
            {
                _sinceGrounded += dt;
            }

            _sinceJumpPressed = _actions.Player.Jump.WasPressedThisFrame() ? 0f : _sinceJumpPressed + dt;

            if (_sinceJumpPressed <= jumpBuffer)
            {
                if (_sinceGrounded <= coyoteTime)
                {
                    Jump(jumpHeight);
                    _sinceGrounded = coyoteTime + 1f;
                }
                else if (doubleJumpUnlocked && !_usedDoubleJump && _actions.Player.Jump.WasPressedThisFrame())
                {
                    _usedDoubleJump = true;
                    Jump(doubleJumpHeight);
                }
            }

            if (!grounded || _vertical > 0f)
            {
                _vertical += gravity * dt;
            }

            _controller.Move((_horizontal + Vector3.up * _vertical) * dt);

            // Удар головой о низ платформы — не висим под потолком.
            if ((_controller.collisionFlags & CollisionFlags.Above) != 0 && _vertical > 0f)
            {
                _vertical = 0f;
            }
        }

        private void Jump(float height)
        {
            _vertical = Mathf.Sqrt(2f * height * -gravity);
            _sinceJumpPressed = 99f;
        }
    }
}
