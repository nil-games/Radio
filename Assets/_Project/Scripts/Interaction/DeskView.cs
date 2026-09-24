using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// Поворот взгляда сидящего игрока между фиксированными половинами углового стола.
    /// Позиция 0 — исходная, позиция 1 — повёрнутая влево на 90 градусов.
    /// </summary>
    public sealed class DeskView : MonoBehaviour
    {
        [Header("Поворот")]
        [Tooltip("Что поворачиваем. Обычно камера сидящего игрока.")]
        [SerializeField] private Transform pivot;

        [Tooltip("Угол поворота к соседней половине стола, градусов. " +
                 "Положительное значение — поворот влево.")]
        [SerializeField] private float turnAngle = 90f;

        [Tooltip("Сколько длится поворот, с. Мгновенный переход дезориентирует, " +
                 "слишком долгий раздражает при частых переключениях.")]
        [SerializeField] private float turnDuration = 0.35f;

        private Quaternion _baseRotation;
        private Quaternion _fromRotation;
        private Quaternion _toRotation;
        private float _progress = 1f;
        private int _position;

        /// <summary>Текущая половина стола: 0 — исходная, 1 — левая.</summary>
        public int Position => _position;

        /// <summary>Идёт ли поворот прямо сейчас.</summary>
        public bool IsTurning => _progress < 1f;

        /// <summary>
        /// Пока true, поворот запрещён и стрелки гаснут сами: камеру забрал кто-то
        /// другой — например, игрок смотрит в монитор.
        /// </summary>
        public bool Locked { get; set; }

        public bool CanTurnLeft => !Locked && !IsTurning && _position == 0;

        public bool CanTurnRight => !Locked && !IsTurning && _position == 1;

        private void Awake()
        {
            if (pivot == null)
            {
                Debug.LogError($"{nameof(DeskView)}: не задано, что поворачивать. Поворот отключён.", this);
                enabled = false;
                return;
            }

            // Запоминаем исходную мировую ориентацию: поворачивать нужно вокруг
            // мировой вертикали, а не вокруг оси родителя — у кресла есть наклон,
            // и поворот в его системе координат завалил бы горизонт.
            _baseRotation = pivot.rotation;
            _fromRotation = _baseRotation;
            _toRotation = _baseRotation;
        }

        public void TurnLeft()
        {
            if (CanTurnLeft)
            {
                GoTo(1);
            }
        }

        public void TurnRight()
        {
            if (CanTurnRight)
            {
                GoTo(0);
            }
        }

        /// <summary>Мгновенно вернуть исходную ориентацию: игрок встал из-за стола.</summary>
        public void ResetInstantly()
        {
            _position = 0;
            _progress = 1f;
            _fromRotation = _baseRotation;
            _toRotation = _baseRotation;

            if (pivot != null)
            {
                pivot.rotation = _baseRotation;
            }
        }

        private void GoTo(int position)
        {
            _position = position;
            _fromRotation = pivot.rotation;
            _toRotation = Quaternion.AngleAxis(-turnAngle * position, Vector3.up) * _baseRotation;
            _progress = 0f;
        }

        private void LateUpdate()
        {
            if (_progress >= 1f)
            {
                return;
            }

            // unscaledDeltaTime: поворот должен работать и когда игра на паузе.
            _progress = Mathf.Min(1f, _progress + Time.unscaledDeltaTime / Mathf.Max(0.0001f, turnDuration));

            // SmoothStep убирает рывки на старте и в конце — поворот головы,
            // а не щелчок переключателя.
            pivot.rotation = Quaternion.Slerp(_fromRotation, _toRotation, Mathf.SmoothStep(0f, 1f, _progress));
        }
    }
}
