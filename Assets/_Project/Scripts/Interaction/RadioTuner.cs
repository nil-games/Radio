using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Radio.Interaction
{
    /// <summary>
    /// Радиоприёмник в квартире. По нажатию игрок встаёт перед ним и смотрит через отдельную
    /// камеру; Q и E крутят ручку настройки, частота бежит по шкале на экране.
    /// Повторное нажатие взаимодействия отпускает игрока.
    /// R включает и выключает радио — и у самого радио, и просто наведя на него прицел.
    /// </summary>
    public sealed class RadioTuner : Interactable
    {
        [Header("Вид")]
        [Tooltip("Камера перед радио. В сцене должна быть выключена — её включает этот скрипт.")]
        [SerializeField] private Camera tuningCamera;

        [SerializeField] private RadioDialView dial;

        [Tooltip("Звук радио. Если пусто, берётся с этого же объекта.")]
        [SerializeField] private RadioReceiver receiver;

        [Header("Ручка")]
        [Tooltip("Большая ручка настройки. Пивот должен стоять на её оси.")]
        [SerializeField] private Transform knob;

        [Tooltip("Лицевая сторона радио — ось, вокруг которой крутится ручка. " +
                 "Если пусто, берётся направление от радио к камере.")]
        [SerializeField] private Transform front;

        [Tooltip("На сколько градусов поворачивается ручка на один мегагерц.")]
        [SerializeField] private float knobDegreesPerMHz = 26f;

        [Header("Частота, МГц")]
        [SerializeField] private float minFrequency = 87.5f;

        [SerializeField] private float maxFrequency = 108f;

        [SerializeField] private float startFrequency = 100f;

        [Tooltip("Шаг одного короткого нажатия.")]
        [SerializeField] private float step = 0.1f;

        [Tooltip("Скорость, когда клавишу держат, МГц в секунду.")]
        [SerializeField] private float holdSpeed = 2.5f;

        [Tooltip("Через сколько секунд удержания начинается плавный ход.")]
        [SerializeField] private float holdDelay = 0.3f;

        private Quaternion _knobBase;
        private Vector3 _knobAxis;
        private PlayerInteractor _interactor;
        private PlayerInteractor _player;
        private bool _tuning;
        private float _heldFor;
        private float _frequency;

        /// <summary>Текущая частота, округлённая до шага.</summary>
        public float Frequency => Mathf.Round(_frequency / step) * step;

        /// <summary>Частота сменилась. Задел под станции: их слушатель подпишется здесь.</summary>
        public event Action<float> FrequencyChanged;

        /// <summary>Пока игрок стоит у радио, он занят им: следующее нажатие отпустит его.</summary>
        public override bool IsExclusive => true;

        protected override void Awake()
        {
            base.Awake();

            if (tuningCamera == null || dial == null)
            {
                Debug.LogError($"{nameof(RadioTuner)}: не заданы камера или экран. Радио отключено.", this);
                enabled = false;
                return;
            }

            // Страхуемся от сохранённого в сцене включённого состояния: двух активных камер быть не должно.
            tuningCamera.enabled = false;
            dial.Hide();

            _frequency = Mathf.Clamp(startFrequency, minFrequency, maxFrequency);

            if (receiver == null)
            {
                receiver = GetComponent<RadioReceiver>();
            }

            if (receiver != null)
            {
                receiver.PowerChanged += dial.SetPower;
            }

            if (knob != null)
            {
                _knobBase = knob.localRotation;

                // Ось считаем в координатах самой ручки: у импортированной модели оси повёрнуты
                // как угодно, а «вокруг себя, лицом к игроку» — единственное, что тут важно.
                var worldAxis = front != null
                    ? front.forward
                    : (tuningCamera.transform.position - knob.position).normalized;
                _knobAxis = knob.InverseTransformDirection(worldAxis).normalized;
            }

            ApplyKnob();
        }

        public override void Interact(PlayerInteractor interactor)
        {
            if (_tuning)
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
            interactor.PlayerCamera.enabled = false;
            tuningCamera.enabled = true;
            interactor.SetActiveCamera(tuningCamera);

            interactor.BeginExclusive(this);
            _interactor = interactor;
            _tuning = true;
            _heldFor = 0f;

            dial.Show(minFrequency, maxFrequency);
            dial.SetFrequency(Frequency);
            dial.SetPower(receiver != null && receiver.IsOn);
        }

        private void Leave(PlayerInteractor interactor)
        {
            dial.Hide();

            tuningCamera.enabled = false;
            interactor.PlayerCamera.enabled = true;
            interactor.SetActiveCamera(interactor.PlayerCamera);

            interactor.Movement.RemoveSuspendRequest(this);
            interactor.EndExclusive(this);

            _interactor = null;
            _tuning = false;
        }

        private void OnDestroy()
        {
            if (receiver != null && dial != null)
            {
                receiver.PowerChanged -= dial.SetPower;
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (keyboard[Key.R].wasPressedThisFrame && CanTogglePower())
            {
                receiver.Toggle();
            }

            if (!_tuning)
            {
                return;
            }

            // Коды позиционные: клавиша на месте Q при любой раскладке, как и в мини-играх.
            var down = keyboard[Key.Q].isPressed;
            var up = keyboard[Key.E].isPressed;
            var direction = (up ? 1 : 0) - (down ? 1 : 0);

            if (direction == 0)
            {
                _heldFor = 0f;
                return;
            }

            var before = Frequency;

            if (keyboard[Key.Q].wasPressedThisFrame || keyboard[Key.E].wasPressedThisFrame)
            {
                // Короткое нажатие — ровно один шаг: так можно попасть точно в нужную станцию.
                _frequency = Frequency + direction * step;
                _heldFor = 0f;
            }
            else
            {
                _heldFor += Time.deltaTime;

                if (_heldFor >= holdDelay)
                {
                    _frequency += direction * holdSpeed * Time.deltaTime;
                }
            }

            _frequency = Mathf.Clamp(_frequency, minFrequency, maxFrequency);
            ApplyKnob();

            if (Mathf.Approximately(before, Frequency))
            {
                return;
            }

            dial.SetFrequency(Frequency);
            FrequencyChanged?.Invoke(Frequency);
        }

        /// <summary>
        /// Выключатель работает у самого радио и в комнате, когда прицел наведён на радио:
        /// подошёл, нажал — и не нужно вставать к нему, чтобы заглушить.
        /// </summary>
        private bool CanTogglePower()
        {
            if (receiver == null)
            {
                return false;
            }

            if (_tuning)
            {
                return true;
            }

            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerInteractor>();
            }

            return _player != null && _player.Focused == this;
        }

        private void ApplyKnob()
        {
            if (knob == null)
            {
                return;
            }

            // Больше частота — ручка дальше по часовой, если смотреть спереди. Система координат
            // Unity левая: положительный угол вокруг оси, смотрящей на зрителя, — как раз по часовой.
            var angle = (_frequency - minFrequency) * knobDegreesPerMHz;
            knob.localRotation = _knobBase * Quaternion.AngleAxis(angle, _knobAxis);
        }

        private void OnValidate()
        {
            if (maxFrequency < minFrequency)
            {
                maxFrequency = minFrequency;
            }

            step = Mathf.Max(0.01f, step);
        }
    }
}
