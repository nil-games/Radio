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
    /// Таблица отвечает только на вопрос «когда» — и всегда по часам смены,
    /// при необходимости с задержкой в реальных секундах после отметки.
    /// </remarks>
    [CreateAssetMenu(fileName = "NightSchedule", menuName = "Радио/Расписание ночи")]
    public sealed class NightSchedule : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Для логов и отладки. В игре не показывается.")]
            public string id;

            [Tooltip("Номер ночи, 1–7.")]
            public int night;

            [Tooltip("Когда: отметка на часах смены (00:00–06:00) и задержка в реальных секундах после неё.")]
            public GameTimeMark when;

            [Tooltip("Узел .yarn, который запустится при достижении отметки.")]
            public string yarnNode;
        }

        [Tooltip("Строки расписания. Порядок неважен: они сортируются по времени сами.")]
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public Entry[] Entries => entries;
    }
}
