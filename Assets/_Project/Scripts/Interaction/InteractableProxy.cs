using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// Указатель на интерактив, который живёт на другом объекте.
    /// Вешается на саму геометрию предмета, когда коллайдеры и компонент-сценарий
    /// разнесены: луч прицела попадает в коллайдер, а отсюда находит сценарий.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractableProxy : MonoBehaviour
    {
        [Tooltip("Сценарий, который отработает при взаимодействии с этой геометрией.")]
        [SerializeField] private Interactable target;

        public Interactable Target => target;

        private void Awake()
        {
            if (target == null)
            {
                Debug.LogError($"{nameof(InteractableProxy)}: не задан сценарий. Объект не будет реагировать на прицел.", this);
            }
        }
    }
}
