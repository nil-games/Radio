using UnityEngine;

namespace Radio.World
{
    /// <summary>
    /// Включает объекты, когда часы смены доходят до отметки. Так вещи появляются
    /// в квартире по ходу ночи: банка Зины — ровно тогда, когда визит закончился.
    /// </summary>
    /// <remarks>
    /// Висит не на самих объектах, а на ком-то включённом: выключенный объект не получает
    /// ни Start, ни событий и сам себя включить не может.
    /// </remarks>
    public sealed class ActivateAtTime : MonoBehaviour
    {
        [Tooltip("Отметка времени, «ЧЧ:ММ». Включает, когда часы дошли до неё или прошли дальше.")]
        [SerializeField] private string at = "01:00";

        [Tooltip("Объекты, которые включаются. В сцене их нужно оставить выключенными.")]
        [SerializeField] private GameObject[] targets = System.Array.Empty<GameObject>();

        [Tooltip("Если пусто, берутся из GameSession в сцене.")]
        [SerializeField] private TimeManager time;

        private int _mark;
        private bool _done;

        // Start, а не Awake: сессия заводится в своём Awake, и порядок между ними не гарантирован.
        private void Start()
        {
            if (!TryParse(at, out _mark))
            {
                Debug.LogError($"{nameof(ActivateAtTime)}: время «{at}» не в формате ЧЧ:ММ.", this);
                enabled = false;
                return;
            }

            if (time == null && GameSession.Current != null)
            {
                time = GameSession.Current.Time;
            }

            if (time == null)
            {
                Debug.LogError($"{nameof(ActivateAtTime)}: нет часов смены.", this);
                enabled = false;
                return;
            }

            time.TimeAdvanced += OnTimeAdvanced;

            // Смена могла начаться уже после отметки — например, с отладочным временем.
            OnTimeAdvanced(0, time.Minutes);
        }

        private void OnDestroy()
        {
            if (time != null)
            {
                time.TimeAdvanced -= OnTimeAdvanced;
            }
        }

        private void OnTimeAdvanced(int from, int to)
        {
            if (_done || to < _mark)
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

        private static bool TryParse(string clock, out int minutes)
        {
            minutes = 0;
            var parts = clock.Split(':');

            if (parts.Length != 2
                || !int.TryParse(parts[0], out var hour)
                || !int.TryParse(parts[1], out var minute))
            {
                return false;
            }

            minutes = hour * 60 + minute;
            return true;
        }
    }
}
