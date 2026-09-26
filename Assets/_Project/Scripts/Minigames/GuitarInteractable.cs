using Radio.Interaction;
using UnityEngine;

namespace Radio.Minigames
{
    /// <summary>
    /// Гитара в комнате: взаимодействие запускает мини-игру «Гитара».
    /// Пока играет песня, игрок не ходит и не смотрит по сторонам;
    /// когда игра закончилась — управление возвращается.
    /// </summary>
    public sealed class GuitarInteractable : Interactable
    {
        [Header("Мини-игра")]
        [Tooltip("Корень поля мини-игры. Выключен, пока в неё не играют.")]
        [SerializeField] private GameObject gameRoot;

        [SerializeField] private KaraokeGame game;

        [SerializeField] private KaraokeSong song;

        private PlayerInteractor _interactor;

        public override bool CanInteract => base.CanInteract && game != null && !game.IsRunning;

        protected override void Awake()
        {
            base.Awake();

            if (gameRoot == null || game == null || song == null)
            {
                Debug.LogError($"{nameof(GuitarInteractable)}: не заданы поле, мини-игра или песня.", this);
                enabled = false;
                return;
            }

            gameRoot.SetActive(false);
            game.Finished += HandleFinished;
        }

        private void OnDestroy()
        {
            if (game != null)
            {
                game.Finished -= HandleFinished;
            }
        }

        public override void Interact(PlayerInteractor interactor)
        {
            PlayThen("Interact", () =>
            {
                gameRoot.SetActive(true);

                if (!game.Begin(song))
                {
                    gameRoot.SetActive(false);
                    return;
                }

                TakeOverPlayer(interactor);
            });
        }

        private void TakeOverPlayer(PlayerInteractor interactor)
        {
            _interactor = interactor;

            // Тот же счётчик владельцев, что у кресла, диалога и «Провода»:
            // игра отпустит только своё и не поднимет игрока из кресла.
            _interactor.AddInputBlock(this);

            if (_interactor.Movement != null)
            {
                _interactor.Movement.AddSuspendRequest(this);
            }
        }

        private void HandleFinished()
        {
            gameRoot.SetActive(false);

            if (_interactor == null)
            {
                return;
            }

            _interactor.RemoveInputBlock(this);

            if (_interactor.Movement != null)
            {
                _interactor.Movement.RemoveSuspendRequest(this);
            }

            _interactor = null;
        }
    }
}
