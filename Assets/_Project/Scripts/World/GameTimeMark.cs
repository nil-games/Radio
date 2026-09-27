using System;
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

        public GameTimeMark(string at, float delaySeconds = 0f)
        {
            this.at = at;
            this.delaySeconds = delaySeconds;
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
            delaySeconds > 0f ? $"{at} + {delaySeconds:0.#} с" : at;
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
    /// </remarks>
    public sealed class TimedEvent
    {
        private readonly int _minutes;
        private readonly float _delay;
        private float _reachedAt = -1f;

        public TimedEvent(GameTimeMark mark)
        {
            IsValid = mark.TryGetTotalMinutes(out _minutes);
            _delay = Mathf.Max(0f, mark.delaySeconds);
        }

        public bool IsValid { get; }

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
