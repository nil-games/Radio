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

        private sealed class Pending
        {
            public NightSchedule.Entry Entry;
            public TimedEvent Event;
        }

        // Очередь нужна потому, что одно действие игрока может перешагнуть сразу две отметки,
        // а диалоги обязаны идти по очереди, а не наложиться друг на друга.
        private readonly Queue<NightSchedule.Entry> _queue = new Queue<NightSchedule.Entry>();
        private readonly List<Pending> _events = new List<Pending>();

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

            foreach (var entry in schedule.Entries)
            {
                if (entry.night != time.Night || string.IsNullOrWhiteSpace(entry.yarnNode))
                {
                    continue;
                }

                var timed = new TimedEvent(entry.when);

                if (!timed.IsValid)
                {
                    Debug.LogError($"{nameof(ScheduleRunner)}: у события {entry.id} время «{entry.when.at}» " +
                                   "не в формате ЧЧ:ММ.", this);
                    continue;
                }

                _events.Add(new Pending { Entry = entry, Event = timed });
            }

            // Сортируем по времени: перешагнув сразу 01:10 и 01:20, игрок должен услышать
            // их в том порядке, в котором они стоят в ночи, а не в порядке строк таблицы.
            _events.Sort((a, b) => a.Event.Minutes.CompareTo(b.Event.Minutes));

            time.DebugJumped += OnDebugJumped;
            dialogue.DialogueFinished += StartNextIfIdle;
        }

        private void OnDestroy()
        {
            if (time != null)
            {
                time.DebugJumped -= OnDebugJumped;
            }

            if (dialogue != null)
            {
                dialogue.DialogueFinished -= StartNextIfIdle;
            }
        }

        private void Update()
        {
            var queued = false;

            foreach (var pending in _events)
            {
                if (pending.Event.Poll(time))
                {
                    _queue.Enqueue(pending.Entry);
                    queued = true;
                }
            }

            if (queued)
            {
                StartNextIfIdle();
            }
        }

        /// <summary>Отладочный перевод часов: разговоры более раннего времени уже не начнутся.</summary>
        private void OnDebugJumped(int minutes)
        {
            foreach (var pending in _events)
            {
                pending.Event.SkipIfBefore(minutes);
            }

            // Из очереди тоже: там могли ждать разговоры, отменённые только что.
            var kept = new List<NightSchedule.Entry>();

            foreach (var entry in _queue)
            {
                if (entry.when.TryGetTotalMinutes(out var mark) && mark >= minutes)
                {
                    kept.Add(entry);
                }
            }

            _queue.Clear();

            foreach (var entry in kept)
            {
                _queue.Enqueue(entry);
            }
        }

        private void StartNextIfIdle()
        {
            if (_queue.Count == 0 || dialogue.IsRunning)
            {
                return;
            }

            dialogue.StartDialogue(_queue.Dequeue().yarnNode);
        }
    }
}
