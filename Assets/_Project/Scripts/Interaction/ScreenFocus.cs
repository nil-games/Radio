using UnityEngine;


namespace Radio.Interaction
{
    /// <summary>
    /// Экран, к которому камера подъезжает вплотную: монитор компьютера.
    /// Пока игрок смотрит в него, включается холст с интерфейсом на месте стекла.
    /// </summary>
    /// <remarks>
    /// Камера не подменяется на вторую, а переезжает своя же: вторая камера дала бы
    /// мгновенную склейку, а подъезд должен читаться как движение головы. Исходная
    /// поза запоминается в локальных координатах — кресло поворачивается вместе
    /// с родителем, и мировая поза после поворота стала бы неверной.
    /// </remarks>
    public class ScreenFocus : Interactable
    {
        [Header("Экран")]
        [Tooltip("Холст с интерфейсом. Лежит на месте стекла, выключен, пока в него не смотрят.")]
        [SerializeField] private Canvas screenCanvas;

        [Tooltip("С какого расстояния от холста смотреть, м. Меньше — экран крупнее.")]
        [SerializeField] private float viewDistance = 0.35f;

        [Tooltip("Длительность подъезда и отъезда, с.")]
        [SerializeField] private float travelTime = 0.45f;

        [Header("Ссылки")]
        [Tooltip("Поворот кресла. На время просмотра запирается, иначе стол уедет из-под камеры.")]
        [SerializeField] private DeskView deskView;

        private PlayerInteractor _interactor;
        private Transform _camera;

        private Vector3 _fromPosition;
        private Quaternion _fromRotation;
        private Vector3 _toPosition;
        private Quaternion _toRotation;
        private Vector3 _returnPosition;
        private Quaternion _returnRotation;

        private float _progress = 1f;
        private bool _leaving;

        /// <summary>Игрок сейчас смотрит в экран.</summary>
        public bool IsFocused { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            if (screenCanvas == null)
            {
                Debug.LogError($"{nameof(ScreenFocus)}: не задан холст экрана. Просмотр отключён.", this);
                enabled = false;
                return;
            }

            screenCanvas.gameObject.SetActive(false);
        }

        public override void Interact(PlayerInteractor interactor)
        {
            if (IsFocused || _progress < 1f)
            {
                return;
            }

            Enter(interactor);
        }

        /// <summary>Отъехать от экрана. Зовётся кнопкой закрытия на самом холсте.</summary>
        public void Leave()
        {
            if (!IsFocused || _progress < 1f)
            {
                return;
            }

            // Холст гасим сразу: на отъезде он занимал бы пол-экрана и выглядел
            // как вырванный из монитора прямоугольник.
            screenCanvas.gameObject.SetActive(false);

            _leaving = true;
            _progress = 0f;
            _fromPosition = _camera.localPosition;
            _fromRotation = _camera.localRotation;
        }

        private void Enter(PlayerInteractor interactor)
        {
            _interactor = interactor;
            _camera = interactor.ActiveCamera.transform;

            // Мировому холсту нужна камера, иначе GraphicRaycaster не поймёт,
            // куда игрок целится, и кнопки на экране не нажмутся.
            screenCanvas.worldCamera = interactor.ActiveCamera;

            // Запрещаем всё, что могло бы увести камеру или предмет из-под неё.
            interactor.AddInputBlock(this);

            if (deskView != null)
            {
                deskView.Locked = true;
            }

            var parent = _camera.parent;
            var target = ViewPose();

            _fromPosition = _camera.localPosition;
            _fromRotation = _camera.localRotation;

            // Куда вернуться. Запоминаем здесь, до первого сдвига: после него
            // камера уже стоит у монитора, и запоминать было бы нечего.
            _returnPosition = _fromPosition;
            _returnRotation = _fromRotation;

            // Цель считаем в координатах родителя камеры: дальше кресло стоит на месте,
            // поэтому достаточно посчитать один раз.
            _toPosition = parent != null ? parent.InverseTransformPoint(target.position) : target.position;
            _toRotation = parent != null ? Quaternion.Inverse(parent.rotation) * target.rotation : target.rotation;

            IsFocused = true;
            _leaving = false;
            _progress = 0f;
        }

        /// <summary>
        /// Откуда смотреть: по нормали холста, на заданном расстоянии, лицом к нему.
        /// </summary>
        private (Vector3 position, Quaternion rotation) ViewPose()
        {
            var screen = screenCanvas.transform;
            var normal = -screen.forward;
            var position = screen.position + normal * viewDistance;
            var rotation = Quaternion.LookRotation(-normal, screen.up);
            return (position, rotation);
        }

        private void Update()
        {
            if (_progress >= 1f)
            {
                return;
            }

            _progress = Mathf.Min(1f, _progress + Time.deltaTime / Mathf.Max(0.01f, travelTime));

            // Сглаживание на концах: линейный подъезд к монитору выглядит как рывок.
            var t = Mathf.SmoothStep(0f, 1f, _progress);

            var toPosition = _leaving ? _returnPosition : _toPosition;
            var toRotation = _leaving ? _returnRotation : _toRotation;

            _camera.localPosition = Vector3.Lerp(_fromPosition, toPosition, t);
            _camera.localRotation = Quaternion.Slerp(_fromRotation, toRotation, t);

            if (_progress < 1f)
            {
                return;
            }

            if (_leaving)
            {
                Release();
            }
            else
            {
                screenCanvas.gameObject.SetActive(true);
            }
        }

        private void Release()
        {
            IsFocused = false;

            if (deskView != null)
            {
                deskView.Locked = false;
            }

            if (_interactor != null)
            {
                _interactor.RemoveInputBlock(this);
                _interactor = null;
            }
        }

    }
}
