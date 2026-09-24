using System.Collections.Generic;
using Radio.Dialogue;
using UnityEngine;

namespace Radio.World
{
    /// <summary>
    /// Следит за часами смены и сам запускает разговоры, привязанные к отметкам времени.
    /// Это те события, которые случаются без участия игрока: звонок, помеха в эфире, стук в дверь.
    /// </summary>
    public sealed class ScheduleRunner : MonoBehaviour
    {
        [Header("Данные")]
        [SerializeField] private NightSchedule schedule;

        [Header("Ссылки")]
        [Tooltip("Если пусто, берутся из сцены.")]
        [SerializeField] private TimeManager time;

        [SerializeField] private DialogueController dialogue;

        // Очередь нужна потому, что одно действие игрока может перешагнуть сразу две отметки,
        // а диалоги обязаны идти по очереди, а не наложиться друг на друга.
        private readonly Queue<NightSchedule.Entry> _pending = new Queue<NightSchedule.Entry>();
        private readonly HashSet<string> _fired = new HashSet<string>();

        // Отсчёт реального времени ведём от включения, а не от Time.time: сцена могла
        // грузиться долго, и секунды загрузки в смену не входят.
        private float _startedAt;

        // Именно Start, а не Awake: ссылки берутся из GameSession, а порядок Awake
        // между объектами Unity не гарантирует — в Awake сессии могло ещё не быть.
        private void Start()
        {
            if (time == null)
            {
                var session = GameSession.Current;
                time = session != null ? session.Time : null;
            }

            if (dialogue == null)
            {
                dialogue = FindFirstObjectByType<DialogueController>();
            }

            if (schedule == null || time == null || dialogue == null)
            {
                Debug.LogError($"{nameof(ScheduleRunner)}: не заданы расписание, часы или диалоги. " +
                               "События по времени отключены.", this);
                enabled = false;
                return;
            }

            time.TimeAdvanced += OnTimeAdvanced;
            dialogue.DialogueFinished += StartNextIfIdle;
            _startedAt = Time.time;
        }

        /// <summary>
        /// Реальное время. Нужно для событий, которые должны случиться, даже если игрок
        /// просто ходит по квартире: игровые часы в это время стоят и сами ничего не запустят.
        /// </summary>
        private void Update()
        {
            var elapsed = Time.time - _startedAt;
            var queued = false;

            foreach (var entry in schedule.Entries)
            {
                if (entry.trigger != NightSchedule.TriggerKind.RealSeconds
                    || entry.night != time.Night
                    || string.IsNullOrWhiteSpace(entry.yarnNode))
                {
                    continue;
                }

                if (elapsed < entry.afterSeconds || _fired.Contains(Key(entry)))
                {
                    continue;
                }

                // Отмечаем независимо от флага once: по реальному времени отметка
                // проходится один раз, и без этого событие запускалось бы каждый кадр.
                _fired.Add(Key(entry));
                _pending.Enqueue(entry);
                queued = true;
            }

            if (queued)
            {
                StartNextIfIdle();
            }
        }

        private void OnDestroy()
        {
            if (time != null)
            {
                time.TimeAdvanced -= OnTimeAdvanced;
            }

            if (dialogue != null)
            {
                dialogue.DialogueFinished -= StartNextIfIdle;
            }
        }

        /// <summary>
        /// Отметки берутся полуинтервалом (откуда, докуда]: начало исключено, потому что
        /// на нём событие уже отработало предыдущим сдвигом.
        /// </summary>
        private void OnTimeAdvanced(int from, int to)
        {
            var matched = new List<NightSchedule.Entry>();

            foreach (var entry in schedule.Entries)
            {
                if (entry.trigger != NightSchedule.TriggerKind.GameTime
                    || entry.night != time.Night
                    || string.IsNullOrWhiteSpace(entry.yarnNode))
                {
                    continue;
                }

                var mark = entry.Minutes;

                if (mark <= from || mark > to)
                {
                    continue;
                }

                if (entry.once && _fired.Contains(Key(entry)))
                {
                    continue;
                }

                matched.Add(entry);
            }

            // Сортируем по времени: перешагнув сразу 01:10 и 01:20, игрок должен услышать
            // их в том порядке, в котором они стоят в ночи, а не в порядке строк таблицы.
            matched.Sort((a, b) => a.Minutes.CompareTo(b.Minutes));

            foreach (var entry in matched)
            {
                _fired.Add(Key(entry));
                _pending.Enqueue(entry);
            }

            StartNextIfIdle();
        }

        private void StartNextIfIdle()
        {
            if (_pending.Count == 0 || dialogue.IsRunning)
            {
                return;
            }

            var entry = _pending.Dequeue();
            dialogue.StartDialogue(entry.yarnNode);
        }

        private static string Key(NightSchedule.Entry entry) =>
            entry.night + "/" + (string.IsNullOrWhiteSpace(entry.id) ? entry.yarnNode : entry.id);
    }
}
