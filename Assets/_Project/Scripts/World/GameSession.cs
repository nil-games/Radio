using UnityEngine;

namespace Radio.World
{
    /// <summary>
    /// Состояние текущей смены: часы и флаги мира. Один объект на сцену.
    /// </summary>
    /// <remarks>
    /// Держит статическую ссылку на себя, хотя в проекте принято связывать всё ссылками
    /// в инспекторе. Причина в том, что диалоговая система живёт в отдельном префабе:
    /// ссылка из префаба на объект сцены не сохраняется, и её пришлось бы проставлять
    /// заново при каждой пересборке. Ссылки в инспекторе всё равно имеют приоритет —
    /// статика только подстраховывает.
    /// </remarks>
    [RequireComponent(typeof(TimeManager))]
    [RequireComponent(typeof(WorldState))]
    public sealed class GameSession : MonoBehaviour
    {
        /// <summary>Сессия текущей сцены. Пусто, пока сцена не загрузилась.</summary>
        public static GameSession Current { get; private set; }

        private TimeManager _time;
        private WorldState _world;

        public TimeManager Time => _time;

        public WorldState World => _world;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError($"{nameof(GameSession)}: в сцене их уже два. Лишний выключен.", this);
                enabled = false;
                return;
            }

            Current = this;
            _time = GetComponent<TimeManager>();
            _world = GetComponent<WorldState>();
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }
    }
}
