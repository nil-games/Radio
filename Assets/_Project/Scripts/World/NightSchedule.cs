using System;
using UnityEngine;

namespace Radio.World
{
    /// <summary>
    /// Расписание одной ночи: какие разговоры начинаются сами при достижении отметки времени.
    /// </summary>
    /// <remarks>
    /// Условий и флагов в таблице намеренно нет, хотя ГДД 08.1 перечисляет их среди полей
    /// события. Они пишутся внутри самого узла обычным условием Yarn: иначе логика ночи
    /// расползлась бы по двум местам с разным синтаксисом, и на вопрос «почему звонок не
    /// прозвучал» пришлось бы отвечать, глядя сразу в таблицу и в текст.
    /// Таблица отвечает только на вопрос «когда».
    /// </remarks>
    [CreateAssetMenu(fileName = "NightSchedule", menuName = "Радио/Расписание ночи")]
    public sealed class NightSchedule : ScriptableObject
    {
        /// <summary>По какому таймеру срабатывает событие.</summary>
        public enum TriggerKind
        {
            /// <summary>По игровым часам смены. Они стоят, пока игрок не сделает действие.</summary>
            GameTime,

            /// <summary>
            /// По реальным секундам от начала смены. Для событий, которые должны случиться,
            /// даже если игрок просто ходит по квартире и ничего не трогает.
            /// </summary>
            RealSeconds,
        }

        [Serializable]
        public struct Entry
        {
            [Tooltip("Для логов и отладки. В игре не показывается.")]
            public string id;

            [Tooltip("Номер ночи, 1–7.")]
            public int night;

            public TriggerKind trigger;

            [Tooltip("Только для игрового времени. Час отметки: 0–6, смена идёт с 00:00 до 06:00.")]
            public int hour;

            [Tooltip("Только для игрового времени.")]
            public int minute;

            [Tooltip("Только для реального времени: через сколько секунд после начала смены.")]
            public float afterSeconds;

            [Tooltip("Узел .yarn, который запустится при достижении отметки.")]
            public string yarnNode;

            [Tooltip("Сработать не более одного раза за запуск.")]
            public bool once;

            /// <summary>Отметка в минутах от начала смены.</summary>
            public int Minutes => hour * 60 + minute;

            public string Clock => $"{hour:00}:{minute:00}";
        }

        [Tooltip("Строки расписания. Порядок неважен: они сортируются по времени сами.")]
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public Entry[] Entries => entries;
    }
}
