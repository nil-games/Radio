using Radio.UI;
using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// Место, куда игрок садится: кресло за пультом. Переключает камеру на вид сидящего
    /// и освобождает курсор, чтобы дальше можно было тыкать в приборы на столе.
    /// </summary>
    public class SitPoint : Interactable
    {
        [Header("Посадка")]
        [Tooltip("Камера сидящего игрока. В сцене должна быть выключена — её включает этот скрипт.")]
        [SerializeField] private Camera seatCamera;

        [Tooltip("Куда поставить игрока при вставании. Если пусто, игрок остаётся там, где стоял.")]
        [SerializeField] private Transform standUpPoint;

        [Header("Режим за столом")]
        [Tooltip("Поворот взгляда между половинами углового стола. Необязателен: " +
                 "без него кресло просто не поворачивается.")]
        [SerializeField] private DeskView deskView;

        [Tooltip("Интерфейс режима за столом: стрелки поворота и подсказки.")]
        [SerializeField] private DeskModeUI deskUI;

        private bool _seated;

        /// <summary>Пока игрок сидит, он занят этим креслом: следующее нажатие поднимет его.</summary>
        public override bool IsExclusive => true;

        protected override void Awake()
        {
            base.Awake();

            if (seatCamera == null)
            {
                Debug.LogError($"{nameof(SitPoint)}: не задана камера сидящего. Посадка отключена.", this);
                enabled = false;
                return;
            }

            // Страхуемся от сохранённого в сцене включённого состояния: двух активных камер быть не должно.
            seatCamera.enabled = false;

            if (deskUI != null)
            {
                deskUI.TurnLeftRequested += OnTurnLeftRequested;
                deskUI.TurnRightRequested += OnTurnRightRequested;
                deskUI.Hide();
            }
        }

        private void OnDestroy()
        {
            if (deskUI != null)
            {
                deskUI.TurnLeftRequested -= OnTurnLeftRequested;
                deskUI.TurnRightRequested -= OnTurnRightRequested;
            }
        }

        private void OnTurnLeftRequested()
        {
            // Клик мог прийти, пока игрок уже встал: панель гасится не мгновенно.
            if (!_seated || deskView == null)
            {
                return;
            }

            deskView.TurnLeft();
            RefreshTurnArrows();
        }

        private void OnTurnRightRequested()
        {
            if (!_seated || deskView == null)
            {
                return;
            }

            deskView.TurnRight();
            RefreshTurnArrows();
        }

        /// <summary>
        /// Показывает только тот поворот, который сейчас возможен: половин стола две,
        /// и предлагать несуществующий ход значит обманывать игрока.
        /// </summary>
        private void RefreshTurnArrows()
        {
            if (deskUI == null)
            {
                return;
            }

            if (deskView == null)
            {
                deskUI.SetTurnAvailability(false, false);
                return;
            }

            // Во время поворота прячем обе: повторный клик посреди движения
            // оборвал бы анимацию на середине.
            deskUI.SetTurnAvailability(deskView.CanTurnLeft, deskView.CanTurnRight);
        }

        private void Update()
        {
            // Пока идёт поворот, стрелки скрыты — возвращаем их, когда он закончился.
            if (_seated && deskView != null && !deskView.IsTurning)
            {
                RefreshTurnArrows();
            }
        }

        public override void Interact(PlayerInteractor interactor)
        {
            if (_seated)
            {
                StandUp(interactor);
            }
            else
            {
                SitDown(interactor);
            }
        }

        private void SitDown(PlayerInteractor interactor)
        {
            // Курсором владеет контроллер: он же его и освобождает.
            interactor.Movement.SetSuspended(true);

            // Порядок важен: сперва гасим старую камеру, потом зажигаем новую.
            // Иначе будет кадр с двумя активными MainCamera и недетерминированный Camera.main.
            interactor.PlayerCamera.enabled = false;
            seatCamera.enabled = true;
            interactor.SetActiveCamera(seatCamera);

            interactor.BeginExclusive(this);
            _seated = true;

            if (deskUI != null)
            {
                deskUI.Show();
                RefreshTurnArrows();
            }
        }

        private void StandUp(PlayerInteractor interactor)
        {
            if (deskUI != null)
            {
                deskUI.Hide();
            }

            // Возвращаем исходную ориентацию сразу: иначе в следующий раз игрок
            // сядет лицом ко второй половине стола, хотя подходил к первой.
            if (deskView != null)
            {
                deskView.ResetInstantly();
            }

            seatCamera.enabled = false;
            interactor.PlayerCamera.enabled = true;
            interactor.SetActiveCamera(interactor.PlayerCamera);

            if (standUpPoint != null)
            {
                MovePlayer(interactor, standUpPoint);
            }

            // Наклон камеры контроллер восстановит сам на следующем кадре из своего _pitch.
            interactor.Movement.SetSuspended(false);

            interactor.EndExclusive(this);
            _seated = false;
        }

        /// <summary>
        /// CharacterController сам пишет в Transform, поэтому на время телепорта его нужно выключить,
        /// иначе он вернёт игрока обратно.
        /// </summary>
        private static void MovePlayer(PlayerInteractor interactor, Transform destination)
        {
            var controller = interactor.GetComponent<CharacterController>();

            if (controller != null)
            {
                controller.enabled = false;
            }

            interactor.transform.SetPositionAndRotation(destination.position, destination.rotation);

            if (controller != null)
            {
                controller.enabled = true;
            }
        }
    }
}
