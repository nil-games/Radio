using System;
using System.Collections.Generic;
using UnityEngine;

namespace Radio.World
{
    /// <summary>
    /// Когда случается событие: отметка на часах смены и, по желанию, задержка
    /// в реальных секундах после того, как часы до неё дошли.
    /// </summary>
    /// <remarks>
    /// Все события ночи привязаны к часам смены, а не к секундам от загрузки сцены
    /// и не к одним флагам: только так их можно проверять отладочным переводом часов.
    /// Задержка нужна событиям, которые наступают не сразу: телефон звонит
    /// через пару секунд после начала смены, а не в тот же кадр.
    /// </remarks>
    [Serializable]
    public struct GameTimeMark
    {
        [Tooltip("Отметка на часах смены: «ЧЧ:ММ» — первая ночь, «ЧЧ:ММ:НН» — ночь НН. " +
                 "01:00:02 — час второй ночи.")]
        public string at;

        [Tooltip("Сколько реальных секунд подождать после того, как часы дошли до отметки.")]
        public float delaySeconds;

        [Tooltip("Ждать, пока игрок не встанет: не сидит за столом, не разговаривает, не играет " +
                 "в мини-игру. «Начинается, когда на часах 01:00 и игрок встал из-за стола». " +
                 "Задержка тогда отсчитывается от момента, когда он встал.")]
        public bool afterStandUp;

        public GameTimeMark(string at, float delaySeconds = 0f, bool afterStandUp = false)
        {
            this.at = at;
            this.delaySeconds = delaySeconds;
            this.afterStandUp = afterStandUp;
        }

        /// <summary>Минут от начала своей ночи: 01:30:02 — это 90.</summary>
        public bool TryGetMinutes(out int minutes) => TryParse(at, out minutes, out _);

        /// <summary>
        /// Минут от начала первой ночи, сквозь все ночи: 01:30:02 — это 360 + 90.
        /// Без номера ночи отметка считается отметкой первой ночи.
        /// </summary>
        public bool TryGetTotalMinutes(out int total)
        {
            total = 0;

            if (!TryParse(at, out var minutes, out var night))
            {
                return false;
            }

            total = (night - 1) * TimeManager.ShiftEndMinutes + minutes;
            return true;
        }

        /// <summary>
        /// Разбор «ЧЧ:ММ» или «ЧЧ:ММ:НН». Номер ночи по умолчанию — 1: все отметки,
        /// записанные до появления второй ночи, относятся к первой.
        /// </summary>
        public static bool TryParse(string value, out int minutes, out int night)
        {
            minutes = 0;
            night = 1;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var parts = value.Split(':');

            if (parts.Length < 2 || parts.Length > 3
                || !int.TryParse(parts[0], out var hour) || !int.TryParse(parts[1], out var minute))
            {
                return false;
            }

            if (parts.Length == 3 && (!int.TryParse(parts[2], out night) || night < 1))
            {
                return false;
            }

            minutes = hour * 60 + minute;
            return true;
        }

        public override string ToString() =>
            (delaySeconds > 0f ? $"{at} + {delaySeconds:0.#} с" : at) + (afterStandUp ? ", когда встал" : string.Empty);
    }

    /// <summary>
    /// Ход одного события по его отметке: ждёт часов, отсчитывает задержку,
    /// срабатывает один раз. Отладочный перевод часов дальше отметки отменяет его.
    /// </summary>
    /// <remarks>
    /// Состояние живёт здесь, в компоненте, а не в самой отметке: отметки лежат
    /// в ассетах, а ассет один на все запуски — состояние в нём пережило бы выход
    /// из игры в редакторе.
    /// Время сравнивается сквозным счётом через все ночи (<see cref="TimeManager.TotalMinutes"/>):
    /// событие 01:00 первой ночи не сработает второй раз в 01:00 второй, а событие
    /// второй ночи не сработает в первой.
    /// У события есть постоянный ключ: по нему сохранение (02.12) запоминает, какие
    /// события уже сработали, и после загрузки они не повторяются.
    /// </remarks>
    public sealed class TimedEvent
    {
        // Все события загруженной сцены по ключам. Чистится при загрузке сцены (GameSession).
        private static readonly Dictionary<string, TimedEvent> All = new Dictionary<string, TimedEvent>();

        private readonly int _minutes;
        private readonly float _delay;
        private readonly bool _afterStandUp;
        private float _reachedAt = -1f;

        /// <summary>
        /// Занят ли игрок: сидит, разговаривает, играет. Подставляет PlayerInteractor —
        /// часы смены живут в мире и про игрока сами не знают.
        /// </summary>
        public static Func<bool> PlayerBusy;

        /// <param name="key">
        /// Постоянное имя события для сохранения: одно и то же при каждой загрузке сцены.
        /// Обычно — <see cref="KeyFor"/> от компонента-владельца.
        /// </param>
        public TimedEvent(GameTimeMark mark, string key)
        {
            IsValid = mark.TryGetTotalMinutes(out _minutes);
            _delay = Mathf.Max(0f, mark.delaySeconds);
            _afterStandUp = mark.afterStandUp;
            Key = key;

            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (All.ContainsKey(key))
            {
                Debug.LogError($"{nameof(TimedEvent)}: ключ «{key}» занят дважды — сохранение их перепутает.");
            }

            All[key] = this;
        }

        public bool IsValid { get; }

        public string Key { get; }

        /// <summary>Отметка в минутах от начала первой ночи, сквозным счётом.</summary>
        public int Minutes => _minutes;

        /// <summary>Событие уже сработало.</summary>
        public bool Fired { get; private set; }

        /// <summary>Событие отменено отладочным переводом часов.</summary>
        public bool Skipped { get; private set; }

        /// <summary>
        /// Опрашивается каждый кадр. Возвращает true ровно один раз — в кадр, когда
        /// событие должно случиться.
        /// </summary>
        public bool Poll(TimeManager time)
        {
            if (!IsValid || Fired || Skipped || time == null)
            {
                return false;
            }

            if (time.TotalMinutes < _minutes)
            {
                _reachedAt = -1f;
                return false;
            }

            // Ждём, пока игрок встанет; задержка считается уже от этого момента.
            if (_afterStandUp && PlayerBusy != null && PlayerBusy())
            {
                _reachedAt = -1f;
                return false;
            }

            if (_reachedAt < 0f)
            {
                _reachedAt = Time.time;
            }

            if (Time.time - _reachedAt < _delay)
            {
                return false;
            }

            Fired = true;
            return true;
        }

        /// <summary>
        /// Ключ события по компоненту-владельцу: тип, путь в иерархии и номер отметки
        /// внутри компонента. Переименование объекта в сцене ломает старые сохранения
        /// только для его событий.
        /// </summary>
        public static string KeyFor(Component owner, int index = 0)
        {
            var path = owner.transform.name;

            for (var parent = owner.transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            // Несколько одинаковых компонентов на одном объекте различаются по порядку.
            var same = owner.GetComponents(owner.GetType());
            var ordinal = System.Array.IndexOf(same, owner);

            return $"{owner.GetType().Name}:{path}#{ordinal}:{index}";
        }

        /// <summary>Новая сцена: события прошлой сцены забываются.</summary>
        public static void ForgetAll() => All.Clear();

        /// <summary>Ключи сработавших и отменённых событий — для сохранения.</summary>
        public static void CollectDone(List<string> fired, List<string> skipped)
        {
            foreach (var pair in All)
            {
                if (pair.Value.Fired)
                {
                    fired.Add(pair.Key);
                }
                else if (pair.Value.Skipped)
                {
                    skipped.Add(pair.Key);
                }
            }
        }

        /// <summary>
        /// Загрузка: отметить события сработавшими или отменёнными, ничего не запуская.
        /// Последствия события (гость у двери, окно коридора) компоненты выводят сами
        /// из «сработало» и флагов, как и в обычной игре.
        /// </summary>
        public static void RestoreDone(IEnumerable<string> fired, IEnumerable<string> skipped)
        {
            foreach (var key in fired)
            {
                if (All.TryGetValue(key, out var timed))
                {
                    timed.Fired = true;
                }
            }

            foreach (var key in skipped)
            {
                if (All.TryGetValue(key, out var timed))
                {
                    timed.Skipped = true;
                }
            }
        }

        /// <summary>
        /// Отладочный перевод часов на отметку <paramref name="minutes"/> (сквозной счёт
        /// через все ночи, как в <see cref="TimeManager.DebugJumped"/>). События более
        /// раннего времени отменяются, даже если уже сработали: компонент по этому
        /// признаку гасит то, что ещё звучит. Возвращает true, если событие отменено.
        /// </summary>
        public bool SkipIfBefore(int minutes)
        {
            if (!IsValid || Skipped || _minutes >= minutes)
            {
                return false;
            }

            Skipped = true;
            return true;
        }
    }
}
