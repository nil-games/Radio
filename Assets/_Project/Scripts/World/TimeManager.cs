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

        /// <summary>Часы дошли до 06:00.</summary>
        public event Action ShiftEnded;

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
