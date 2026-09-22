using TMPro;
using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// Всплывающая подсказка в мире. Один экземпляр на сцену: переезжает к тому объекту,
    /// который сейчас в фокусе, поэтому каждому новому объекту нужен лишь пустой анкер.
    /// </summary>
    public class InteractionPrompt : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Корень видимой части. Включается и выключается целиком, чтобы арт можно было заменить.")]
        [SerializeField] private GameObject visual;

        [Tooltip("Текст подсказки.")]
        [SerializeField] private TextMeshPro label;

        [Tooltip("Игрок. У него спрашиваем активную камеру, чтобы развернуть надпись к ней.")]
        [SerializeField] private PlayerInteractor owner;

        private Vector3 _worldPosition;
        private bool _shown;

        private void Awake()
        {
            if (visual == null || label == null)
            {
                Debug.LogError($"{nameof(InteractionPrompt)}: не заданы корень или текст подсказки.", this);
                enabled = false;
                return;
            }

            visual.SetActive(false);
        }

        public void Show(Vector3 worldPosition, string text)
        {
            _worldPosition = worldPosition;

            if (label.text != text)
            {
                label.text = text;
            }

            if (!_shown)
            {
                _shown = true;
                visual.SetActive(true);
            }
        }

        public void Hide()
        {
            if (!_shown)
            {
                return;
            }

            _shown = false;
            visual.SetActive(false);
        }

        // LateUpdate, а не Update: к этому моменту взаимодействие уже могло переключить камеру.
        private void LateUpdate()
        {
            if (!_shown)
            {
                return;
            }

            var camera = owner != null && owner.ActiveCamera != null ? owner.ActiveCamera : Camera.main;

            if (camera == null)
            {
                return;
            }

            transform.position = _worldPosition;

            // Текст рисуется в локальный +Z, поэтому разворачиваем «от камеры», а не «на камеру».
            transform.rotation = Quaternion.LookRotation(_worldPosition - camera.transform.position, Vector3.up);
        }
    }
}
