using System;
using UnityEngine;

namespace Radio.World
{
    /// <summary>
    /// Часы смены по ГДД 02.2. Смена идёт с 00:00 до 06:00.
    /// Время двигается только от действий игрока: пока он осматривается или думает, часы стоят.
    /// </summary>
    /// <remarks>
    /// Хранится в минутах от начала смены, а не в DateTime: смена короче суток, переходов
    /// через полночь нет, а целое число проще сравнивать в условиях диалогов ($time_minutes).
    /// </remarks>
    public sealed class TimeManager : MonoBehaviour
    {
        /// <summary>06:00 — конец смены.</summary>
        public const int ShiftEndMinutes = 360;

        [Header("Состояние смены")]
        [Tooltip("Номер ночи, 1–7. Меняется в инспекторе, чтобы проверять поздние ночи без прохождения ранних.")]
        [SerializeField] private int night = 1;

        [Tooltip("Минут от 00:00. Смена заканчивается на 360, то есть в 06:00.")]
        [SerializeField] private int minutes;

        /// <summary>
        /// Время сдвинулось. Передаёт отметки «откуда» и «докуда»: расписание разбирает
        /// полуинтервал (from, to], поэтому одна отметка не может сработать дважды.
        /// </summary>
        public event Action<int, int> TimeAdvanced;

        /// <summary>
        /// Только для отладки: часы переведены вручную на отметку — в минутах от начала
        /// первой ночи, сквозным счётом (<see cref="TotalMinutes"/>). Приходит до самого
        /// перевода. События раньше этой отметки должны отмениться, в том числе события
        /// прошлых ночей: звонок, стук, задачи — всё, что относится к прошедшему времени.
        /// </summary>
        public event Action<int> DebugJumped;

        /// <summary>Часы дошли до 06:00.</summary>
        public event Action ShiftEnded;

        /// <summary>
        /// Началась новая ночь: номер уже сменился, часы стоят на 00:00. Передаёт номер ночи.
        /// </summary>
        public event Action<int> NightStarted;

        public int Night
        {
            get => night;
            set => night = Mathf.Clamp(value, 1, 7);
        }

        public int Minutes => minutes;

        public int Hour => minutes / 60;

        public int Minute => minutes % 60;

        public bool ShiftOver => minutes >= ShiftEndMinutes;

        /// <summary>Время для интерфейса: «01:10».</summary>
        public string Clock => $"{Hour:00}:{Minute:00}";

        /// <summary>
        /// Время вместе с ночью для отладки и правок, «ЧЧ:ММ:НН»: 01:00:02 — час второй ночи.
        /// Одних часов мало: 01:00 первой и второй ночи — разные моменты игры. Игроку
        /// не показывается — он видит только <see cref="Clock"/>.
        /// </summary>
        public string DebugClock => $"{Clock}:{night:00}";

        /// <summary>
        /// Минут от начала первой ночи: сквозной счёт через все ночи. Удобен, чтобы
        /// сравнивать моменты разных ночей одним числом.
        /// </summary>
        public int TotalMinutes => (night - 1) * ShiftEndMinutes + minutes;

        /// <summary>Сдвинуть часы вперёд на указанное число минут.</summary>
        public void AdvanceBy(int delta)
        {
            if (delta <= 0)
            {
                // Назад время не идёт. Отрицательный сдвиг — почти всегда опечатка в диалоге,
                // и молча проглотить её значит получить неповторимый баг расписания.
                Debug.LogError($"{nameof(TimeManager)}: сдвиг времени на {delta} мин. Время двигается только вперёд.", this);
                return;
            }

            SetMinutes(minutes + delta);
        }

        /// <summary>
        /// Довести часы до указанной отметки. Если она уже пройдена, время не меняется:
        /// у события своя отметка, и запускать его вторым в ночи не повод откатывать часы.
        /// </summary>
        public void AdvanceTo(int hour, int minute)
        {
            var target = hour * 60 + minute;

            if (target <= minutes)
            {
                return;
            }

            SetMinutes(target);
        }

        /// <summary>
        /// Начать следующую ночь: номер растёт на один, часы встают на 00:00.
        /// Зовётся после сна, когда игрок просыпается.
        /// </summary>
        public void BeginNextNight() => BeginNight(night + 1);

        /// <summary>
        /// Начать ночь с указанным номером: часы встают на 00:00. События прошлой ночи
        /// уже отыграли и повторно не сработают — у каждого своя отметка, пройденная один раз.
        /// </summary>
        public void BeginNight(int value)
        {
            night = Mathf.Clamp(value, 1, 7);
            minutes = 0;
            NightStarted?.Invoke(night);
        }

        /// <summary>
        /// Только для отладки: перейти в начало ночи с указанным номером. События
        /// прошлых ночей, которые ещё не сработали, отменяются.
        /// </summary>
        public void DebugBeginNight(int value)
        {
            var target = Mathf.Clamp(value, 1, 7);
            DebugJumped?.Invoke((target - 1) * ShiftEndMinutes);
            BeginNight(target);
        }

        /// <summary>
        /// Только для отладки: поставить часы на любую отметку, в том числе назад.
        /// Вперёд — как обычный сдвиг, и события на пройденных отметках срабатывают.
        /// Назад — молча: события уже отыграли, и повторно их запускать нечем.
        /// </summary>
        public void DebugSetTime(int hour, int minute)
        {
            var target = Mathf.Clamp(hour * 60 + minute, 0, ShiftEndMinutes);

            // До сдвига: события раньше новой отметки должны отмениться прежде,
            // чем сдвиг даст им сработать.
            DebugJumped?.Invoke((night - 1) * ShiftEndMinutes + target);

            if (target > minutes)
            {
                SetMinutes(target);
                return;
            }

            minutes = target;
        }

        private void SetMinutes(int value)
        {
            var from = minutes;
            minutes = Mathf.Min(value, ShiftEndMinutes);

            if (minutes == from)
            {
                return;
            }

            TimeAdvanced?.Invoke(from, minutes);

            // Конец смены объявляем после расписания: события на отметке 06:00
            // должны успеть отыграть.
            if (from < ShiftEndMinutes && minutes >= ShiftEndMinutes)
            {
                ShiftEnded?.Invoke();
            }
        }
    }
}
