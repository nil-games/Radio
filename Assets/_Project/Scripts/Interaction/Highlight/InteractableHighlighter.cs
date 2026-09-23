using UnityEngine;

namespace Radio.Interaction.Highlight
{
    /// <summary>
    /// Подсветка объекта под прицелом. Техника сменная — наружу торчат только Show и Hide,
    /// поэтому обводку можно заменить на любую другую, не трогая остальную систему.
    /// </summary>
    public abstract class InteractableHighlighter : MonoBehaviour
    {
        [Header("Подсветка")]
        [Tooltip("Корень видимой части предмета. Подсвечиваются все рендереры под ним. " +
                 "Если пусто — берётся сам объект. У блокаутной мебели пивот часто далеко " +
                 "от геометрии, поэтому это именно ссылка, а не источник координат.")]
        [SerializeField] private Transform visualRoot;

        /// <summary>Рендереры предмета, собранные один раз при старте.</summary>
        protected Renderer[] Renderers { get; private set; }

        protected virtual void Awake()
        {
            var root = visualRoot != null ? visualRoot : transform;

            // Собираем один раз: у пульта 41 рендерер, искать их каждый кадр нельзя.
            // Выключенные тоже берём — иначе предмет, у которого часть деталей скрыта,
            // потеряет кусок обводки, когда они включатся.
            Renderers = root.GetComponentsInChildren<Renderer>(true);

            if (Renderers.Length == 0)
            {
                Debug.LogError($"{nameof(InteractableHighlighter)}: под указанным корнем нет рендереров. Подсветка отключена.", this);
                enabled = false;
            }
        }

        /// <summary>Объект под прицелом.</summary>
        public abstract void Show();

        /// <summary>Прицел ушёл с объекта.</summary>
        public abstract void Hide();
    }
}
