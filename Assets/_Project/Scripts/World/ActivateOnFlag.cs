using UnityEngine;

namespace Radio.World
{
    /// <summary>
    /// Включает объекты, когда флаг мира становится true: кошку впустили — в квартире
    /// появляются она сама, миска и корм.
    /// </summary>
    /// <remarks>
    /// Висит не на самих объектах, а на ком-то включённом: выключенный объект не получает
    /// ни Start, ни событий и сам себя включить не может. Срабатывает один раз.
    /// </remarks>
    public sealed class ActivateOnFlag : MonoBehaviour
    {
        [Tooltip("Флаг мира. Как только он true, объекты включаются.")]
        [SerializeField] private string flag = "cat_inside";

        [Tooltip("Объекты, которые включаются. В сцене их нужно оставить выключенными.")]
        [SerializeField] private GameObject[] targets = System.Array.Empty<GameObject>();

        private WorldState _world;
        private bool _done;

        // Start, а не Awake: сессия заводится в своём Awake, и порядок между ними не гарантирован.
        private void Start()
        {
            _world = GameSession.Current != null ? GameSession.Current.World : null;

            if (_world == null)
            {
                Debug.LogError($"{nameof(ActivateOnFlag)}: нет состояния мира.", this);
                enabled = false;
                return;
            }

            _world.FlagChanged += OnFlagChanged;

            // Флаг мог быть поставлен раньше, чем мы подписались.
            OnFlagChanged(flag);
        }

        private void OnDestroy()
        {
            if (_world != null)
            {
                _world.FlagChanged -= OnFlagChanged;
            }
        }

        private void OnFlagChanged(string name)
        {
            if (_done || name != flag || !_world.TryGet<bool>(flag, out var value) || !value)
            {
                return;
            }

            _done = true;

            foreach (var target in targets)
            {
                if (target != null)
                {
                    target.SetActive(true);
                }
            }
        }
    }
}
