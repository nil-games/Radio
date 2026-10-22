using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// Дверь, которая распахивается по команде: поворачивается вокруг петель на заданный угол.
    /// </summary>
    /// <remarks>
    /// Ось петель вычисляется по краю модели, а не задаётся отдельным объектом: у двери из
    /// модели пивот стоит там, где его оставил экспорт, и вращать её вокруг пивота — значит
    /// крутить полотно посередине. Какой край петельный и куда открывать, задаётся в инспекторе.
    /// </remarks>
    public sealed class DoorOpener : MonoBehaviour
    {
        [Tooltip("Петли на дальнем краю по ширине двери (иначе — на ближнем).")]
        [SerializeField] private bool hingeOnMaxSide;

        [Tooltip("Угол открывания, градусов. Знак — в какую сторону.")]
        [SerializeField] private float openAngle = 95f;

        [SerializeField] private float duration = 1.2f;

        [Tooltip("Звук открывания. Пусто — без звука.")]
        [SerializeField] private AudioClip openSound;

        private Vector3 _hinge;
        private float _turned;
        private bool _opening;

        public bool IsOpen { get; private set; }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            _opening = true;
            _hinge = HingePoint();

            if (openSound != null)
            {
                AudioSource.PlayClipAtPoint(openSound, _hinge);
            }
        }

        /// <summary>
        /// Сразу открытая, без звука и поворота во времени: восстановление сохранения,
        /// дверь уже была открыта.
        /// </summary>
        public void OpenInstant()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            _opening = false;
            _hinge = HingePoint();
            transform.RotateAround(_hinge, Vector3.up, openAngle - _turned);
            _turned = openAngle;
        }

        private void Update()
        {
            if (!_opening)
            {
                return;
            }

            var step = openAngle / Mathf.Max(0.01f, duration) * Time.deltaTime;

            if (Mathf.Abs(_turned + step) >= Mathf.Abs(openAngle))
            {
                step = openAngle - _turned;
                _opening = false;
            }

            transform.RotateAround(_hinge, Vector3.up, step);
            _turned += step;
        }

        /// <summary>
        /// Петли — на вертикальном крае самой широкой горизонтальной стороны модели:
        /// у двери это ширина полотна, а не его толщина.
        /// </summary>
        private Vector3 HingePoint()
        {
            var renderer = GetComponent<Renderer>();

            if (renderer == null)
            {
                return transform.position;
            }

            var bounds = renderer.bounds;
            var alongX = bounds.size.x >= bounds.size.z;
            var point = bounds.center;

            if (alongX)
            {
                point.x = hingeOnMaxSide ? bounds.max.x : bounds.min.x;
            }
            else
            {
                point.z = hingeOnMaxSide ? bounds.max.z : bounds.min.z;
            }

            return point;
        }
    }
}
