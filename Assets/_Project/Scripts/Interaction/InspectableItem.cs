using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Radio.Interaction
{
    /// <summary>
    /// «3D просмотр»: предмет, который можно взять и рассмотреть. По нажатию он подлетает к камере,
    /// зажатой кнопкой мыши его вращают, клавиша выхода кладёт его обратно на место.
    /// </summary>
    /// <remarks>
    /// Пока предмет в руках, ввод игрока заблокирован через <see cref="PlayerInteractor.AddInputBlock"/>,
    /// а не взят в «исключительный» режим: в исключительном режиме клик мышью уходит предмету
    /// под курсором, и первый же поворот рассматриваемой вещи положил бы её обратно.
    /// Поэтому клавишу выхода предмет читает сам.
    /// <para/>
    /// Подсказка по управлению строится кодом, как и остальной интерфейс проекта.
    /// <para/>
    /// Как добавить новый предмет: Add Component → Радио → 3D просмотр, плюс коллайдер
    /// (без него на предмет не навестись) и, по желанию, OutlineHighlighter для обводки.
    /// Поворот в руках (Hold Euler) подбирается под модель, чтобы она ложилась лицевой стороной.
    /// </remarks>
    [AddComponentMenu("Радио/3D просмотр")]
    public sealed class InspectableItem : Interactable
    {
        [Header("3D просмотр")]
        [Tooltip("На каком расстоянии от камеры держать предмет, м.")]
        [SerializeField] private float holdDistance = 0.35f;

        [Tooltip("Сколько длится перелёт к камере и обратно, с.")]
        [SerializeField] private float moveTime = 0.35f;

        [Tooltip("Можно ли вращать предмет мышью. Выключено — предмет только подносится к камере " +
                 "и держится ровно, как задано в Hold Euler: для записок и листков, где важна лицевая сторона.")]
        [SerializeField] private bool allowRotation = true;

        [Tooltip("Скорость вращения мышью, градусов на пиксель.")]
        [SerializeField] private float rotateSpeed = 0.3f;

        [Tooltip("Поворот предмета в руках относительно камеры. Подбирается под модель, " +
                 "чтобы в руки она ложилась лицевой стороной.")]
        [SerializeField] private Vector3 holdEuler;

        [Tooltip("Клавиша, которой предмет кладут обратно.")]
        [SerializeField] private Key exitKey = Key.F;

        [Header("Подсказка")]
        [Tooltip("Шрифт подсказки. Обязан содержать кириллицу.")]
        [SerializeField] private TMP_FontAsset font;

        [SerializeField] private string hint = "Зажмите ЛКМ и двигайте мышь — вращать   ·   F — положить";

        [Tooltip("Подсказка, когда вращение выключено.")]
        [SerializeField] private string hintWithoutRotation = "F — положить";

        [SerializeField] private int sortingOrder = 60;

        private PlayerInteractor _interactor;
        private Transform _parent;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private Quaternion _heldRotation;

        private Vector3 _fromPosition;
        private Quaternion _fromRotation;
        private float _moveStartedAt;
        private int _startedFrame;
        private State _state;
        private GameObject _hintCanvas;
        private CursorLockMode _cursorLock;
        private bool _cursorVisible;

        private enum State
        {
            Resting,
            Lifting,
            Held,
            Returning,
        }

        public override bool CanInteract => base.CanInteract && _state == State.Resting;

        public override void Interact(PlayerInteractor interactor)
        {
            if (_state != State.Resting)
            {
                return;
            }

            _interactor = interactor;
            _startedFrame = Time.frameCount;

            // Запоминаем, где лежал предмет, в координатах родителя: если стол или полка
            // сдвинутся, пока предмет в руках, он вернётся туда, где его взяли.
            _parent = transform.parent;
            _homePosition = transform.localPosition;
            _homeRotation = transform.localRotation;

            // Курсор запоминаем до паузы контроллера: сидящему за столом его нужно вернуть
            // свободным, идущему — захваченным.
            _cursorLock = Cursor.lockState;
            _cursorVisible = Cursor.visible;

            interactor.AddInputBlock(this);

            if (interactor.Movement != null)
            {
                interactor.Movement.AddSuspendRequest(this);
            }

            // Курсор не нужен: предмет вращают движением мыши, а не наведением.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _heldRotation = Quaternion.Euler(holdEuler);
            BeginMove(State.Lifting);
            ShowHint(true);
        }

        private void Update()
        {
            switch (_state)
            {
                case State.Lifting:
                    MoveTowards(HoldPosition(), HoldRotation(), State.Held);
                    break;

                case State.Held:
                    UpdateHeld();
                    break;

                case State.Returning:
                    MoveTowards(HomePosition(), HomeRotation(), State.Resting);
                    break;
            }
        }

        private void UpdateHeld()
        {
            var mouse = Mouse.current;

            if (allowRotation && mouse != null && mouse.leftButton.isPressed)
            {
                // Вращаем вокруг осей камеры, а не самого предмета: мышь вправо — предмет
                // поворачивается вправо на экране, как бы его ни повернули до этого.
                var delta = mouse.delta.ReadValue() * rotateSpeed;
                var cameraTransform = CameraTransform();
                var spin = Quaternion.AngleAxis(-delta.x, cameraTransform.up)
                           * Quaternion.AngleAxis(delta.y, cameraTransform.right);
                _heldRotation = Quaternion.Inverse(cameraTransform.rotation) * spin
                                * cameraTransform.rotation * _heldRotation;
            }

            transform.SetPositionAndRotation(HoldPosition(), HoldRotation());

            // Нажатие, которым предмет взяли, в этом же кадре его не кладёт.
            var keyboard = Keyboard.current;

            if (Time.frameCount != _startedFrame && keyboard != null && keyboard[exitKey].wasPressedThisFrame)
            {
                ShowHint(false);
                BeginMove(State.Returning);
            }
        }

        private void BeginMove(State state)
        {
            _state = state;
            _fromPosition = transform.position;
            _fromRotation = transform.rotation;
            _moveStartedAt = Time.time;
        }

        private void MoveTowards(Vector3 position, Quaternion rotation, State next)
        {
            var t = moveTime > 0f ? Mathf.Clamp01((Time.time - _moveStartedAt) / moveTime) : 1f;
            var eased = Mathf.SmoothStep(0f, 1f, t);
            transform.SetPositionAndRotation(
                Vector3.Lerp(_fromPosition, position, eased),
                Quaternion.Slerp(_fromRotation, rotation, eased));

            if (t < 1f)
            {
                return;
            }

            _state = next;

            if (next == State.Resting)
            {
                transform.localPosition = _homePosition;
                transform.localRotation = _homeRotation;
                ReleasePlayer();
            }
        }

        private void ReleasePlayer()
        {
            if (_interactor == null)
            {
                return;
            }

            _interactor.RemoveInputBlock(this);

            if (_interactor.Movement != null)
            {
                _interactor.Movement.RemoveSuspendRequest(this);
            }

            // Курсор — как был до осмотра: контроллер сам его не трогает, если игрок
            // по-прежнему сидит за столом.
            Cursor.lockState = _cursorLock;
            Cursor.visible = _cursorVisible;

            _interactor = null;
        }

        private Transform CameraTransform()
        {
            var camera = _interactor != null ? _interactor.ActiveCamera : null;
            return camera != null ? camera.transform : Camera.main.transform;
        }

        private Vector3 HoldPosition()
        {
            var cameraTransform = CameraTransform();
            return cameraTransform.position + cameraTransform.forward * holdDistance;
        }

        private Quaternion HoldRotation() => CameraTransform().rotation * _heldRotation;

        private Vector3 HomePosition() => _parent != null ? _parent.TransformPoint(_homePosition) : _homePosition;

        private Quaternion HomeRotation() => _parent != null ? _parent.rotation * _homeRotation : _homeRotation;

        private void OnDisable()
        {
            ShowHint(false);

            if (_state != State.Resting)
            {
                _state = State.Resting;
                transform.localPosition = _homePosition;
                transform.localRotation = _homeRotation;
                ReleasePlayer();
            }
        }

        private void ShowHint(bool visible)
        {
            if (!visible)
            {
                if (_hintCanvas != null)
                {
                    _hintCanvas.SetActive(false);
                }

                return;
            }

            if (_hintCanvas == null)
            {
                BuildHint();
            }

            _hintCanvas.SetActive(true);
        }

        private void BuildHint()
        {
            // Холст отдельный и не дочерний предмету: предмет двигается, а подсказка
            // должна стоять на месте внизу экрана.
            _hintCanvas = new GameObject(name + "_InspectHint", typeof(RectTransform));
            var canvas = _hintCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = _hintCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject("Hint", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_hintCanvas.transform, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 48f);
            rect.sizeDelta = new Vector2(1600f, 60f);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = allowRotation ? hint : hintWithoutRotation;
            label.fontSize = 30f;
            label.color = Color.white;
            label.outlineColor = Color.black;
            label.outlineWidth = 0.2f;
            label.alignment = TextAlignmentOptions.Bottom;
            label.raycastTarget = false;
        }
    }
}
