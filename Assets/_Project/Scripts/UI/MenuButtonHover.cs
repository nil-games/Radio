using UnityEngine;
using UnityEngine.EventSystems;

namespace Radio.UI
{
    /// <summary>
    /// Показывает маркер рядом с кнопкой меню, пока курсор над ней.
    /// Вешается на каждую кнопку.
    /// </summary>
    public sealed class MenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Ссылки")]
        [Tooltip("Маркер слева от кнопки. Включается, пока курсор над плашкой.")]
        [SerializeField] private GameObject marker;

        private void Awake()
        {
            if (marker == null)
            {
                Debug.LogError($"{nameof(MenuButtonHover)}: не задан маркер наведения.", this);
                enabled = false;
                return;
            }

            marker.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData) => SetMarker(true);

        public void OnPointerExit(PointerEventData eventData) => SetMarker(false);

        private void OnDisable()
        {
            // Если меню спрячут, пока курсор над кнопкой, OnPointerExit уже не придёт,
            // и при следующем показе маркер останется висеть.
            SetMarker(false);
        }

        private void SetMarker(bool visible)
        {
            if (marker != null)
            {
                marker.SetActive(visible);
            }
        }
    }
}
