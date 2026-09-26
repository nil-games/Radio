using System;
using UnityEngine;

namespace Radio.World
{
    /// <summary>
    /// Сюжетные шаги ночей и флаги, которыми отмечается их завершение. Нужны отладке:
    /// при переводе часов вперёд все шаги более раннего времени считаются пройденными.
    /// </summary>
    /// <remarks>
    /// Флаги здесь те же, что ставит сама игра по ходу шага: разговор, мини-игра, визит.
    /// Добавляя в ночь новый шаг, впиши сюда его время и флаги завершения — иначе
    /// перепрыгнуть его отладкой не получится, и дальше мир будет считать, что шага не было.
    /// </remarks>
    [CreateAssetMenu(fileName = "StoryBeats", menuName = "Радио/Сюжетные шаги ночей")]
    public sealed class StoryBeats : ScriptableObject
    {
        [Serializable]
        public struct Beat
        {
            [Tooltip("Для логов и отладки. В игре не показывается.")]
            public string id;

            [Tooltip("Номер ночи, 1–7.")]
            public int night;

            [Tooltip("К какому времени смены относится шаг, «ЧЧ:ММ».")]
            public string at;

            [Tooltip("Флаги мира, которые ставятся в true, когда шаг пройден.")]
            public string[] doneFlags;
        }

        [SerializeField] private Beat[] beats = Array.Empty<Beat>();

        public Beat[] Beats => beats;
    }
}
