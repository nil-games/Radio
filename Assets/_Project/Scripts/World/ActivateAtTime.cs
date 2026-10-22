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
    /// Отметка сравнивается сквозным счётом через все ночи: «02:00» — это 02:00 первой
    /// ночи, «02:00:02» — второй. Так после загрузки сохранения второй ночи вещь,
    /// появившаяся в первую, на месте (GDD 02.12).
    /// </remarks>
    public sealed class ActivateAtTime : MonoBehaviour
    {
        [Tooltip("Отметка времени: «ЧЧ:ММ» — первая ночь, «ЧЧ:ММ:НН» — ночь НН. " +
                 "Включает, когда часы дошли до неё или прошли дальше.")]
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
            if (!new GameTimeMark(at).TryGetTotalMinutes(out _mark))
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

            if (GameSession.Current != null)
            {
                GameSession.Current.Restored += Check;
            }

            // Смена могла начаться уже после отметки — например, с отладочным временем.
            Check();
        }

        private void OnDestroy()
        {
            if (time != null)
            {
                time.TimeAdvanced -= OnTimeAdvanced;
            }

            if (GameSession.Current != null)
            {
                GameSession.Current.Restored -= Check;
            }
        }

        private void OnTimeAdvanced(int from, int to) => Check();

        private void Check()
        {
            if (_done || time.TotalMinutes < _mark)
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
