using System;
using System.Collections.Generic;
using UnityEngine;

namespace Radio.World
{
    /// <summary>
    /// Все задачи игрока с их текстами. Когда задача появляется и когда снимается,
    /// решает сюжет командами task_add и task_done — здесь только «что написано».
    /// </summary>
    /// <remarks>
    /// Тексты собраны в одном месте, а не разбросаны по узлам .yarn: одна и та же задача
    /// может ставиться из разных веток, и поправить формулировку нужно один раз.
    /// </remarks>
    [CreateAssetMenu(fileName = "Tasks", menuName = "Радио/Задачи игрока")]
    public sealed class TaskCatalog : ScriptableObject
    {
        [Serializable]
        public struct Task
        {
            [Tooltip("Имя для команд в диалогах: task_add answer_phone. Латиницей и без пробелов.")]
            public string id;

            [Tooltip("Что видит игрок.")]
            public string text;

            [Tooltip("К какому времени смены относится задача, «ЧЧ:ММ». Нужно отладке: при переводе " +
                     "часов вперёд задачи более раннего времени снимаются. Пусто — не снимается никогда.")]
            public string time;
        }

        [SerializeField] private Task[] tasks;

        [Tooltip("Задачи, которые стоят в списке с начала смены.")]
        [SerializeField] private string[] startTasks;

        public string[] StartTasks => startTasks ?? Array.Empty<string>();

        public bool TryGetText(string id, out string text)
        {
            foreach (var task in tasks ?? Array.Empty<Task>())
            {
                if (string.Equals(task.id, id, StringComparison.OrdinalIgnoreCase))
                {
                    text = task.text;
                    return true;
                }
            }

            text = null;
            return false;
        }

        /// <summary>Задачи, которые относятся ко времени раньше указанной отметки.</summary>
        public IEnumerable<string> IdsBefore(int minutes)
        {
            foreach (var task in tasks ?? Array.Empty<Task>())
            {
                if (TryParseTime(task.time, out var at) && at < minutes)
                {
                    yield return task.id;
                }
            }
        }

        /// <summary>
        /// Время задачи сквозным счётом через все ночи: «ЧЧ:ММ» — первая ночь,
        /// «ЧЧ:ММ:НН» — ночь НН.
        /// </summary>
        private static bool TryParseTime(string clock, out int minutes) =>
            new GameTimeMark(clock).TryGetTotalMinutes(out minutes);
    }
}
