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
        }

        private void StandUp(PlayerInteractor interactor)
        {
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
