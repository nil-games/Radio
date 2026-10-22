using System;
using System.Collections.Generic;
using Radio.Interaction;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Radio.World
{
    /// <summary>
    /// Снимок игры (GDD 02.12): часы, флаги мира, задачи, сработавшие события и где
    /// стоит игрок. Им пользуются и сохранение на диск, и переход в мини-локацию.
    /// </summary>
    /// <remarks>
    /// Состояние объектов сцены отдельно не хранится: объекты пишут его во флаги
    /// <c>OBJ_*</c> и читают обратно по <see cref="GameSession.Restored"/>. Так новому
    /// объекту, чтобы сохраняться, не нужно трогать этот класс.
    /// Списки вместо словарей: JsonUtility словари не сериализует.
    /// </remarks>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>Растёт, когда старые файлы перестают подходить: такие не загружаются.</summary>
        public const int CurrentVersion = 1;

        [Serializable]
        public struct BoolFlag
        {
            public string name;
            public bool value;
        }

        [Serializable]
        public struct NumberFlag
        {
            public string name;
            public float value;
        }

        [Serializable]
        public struct TextFlag
        {
            public string name;
            public string value;
        }

        public int version = CurrentVersion;
        public string scene;
        public string savedAt;
        public int night = 1;
        public int minutes;

        public List<BoolFlag> bools = new List<BoolFlag>();
        public List<NumberFlag> numbers = new List<NumberFlag>();
        public List<TextFlag> texts = new List<TextFlag>();

        [Tooltip("Активные задачи в порядке появления.")]
        public List<string> tasks = new List<string>();

        public List<string> firedEvents = new List<string>();
        public List<string> skippedEvents = new List<string>();

        public bool hasPlayer;
        public Vector3 playerPosition;
        public float playerYaw;

        /// <summary>Снять снимок с текущей сцены.</summary>
        public static SaveData Capture()
        {
            var data = new SaveData
            {
                scene = SceneManager.GetActiveScene().name,
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            };

            var session = GameSession.Current;

            if (session != null)
            {
                data.night = session.Time.Night;
                data.minutes = session.Time.Minutes;

                foreach (var pair in session.World.Bools)
                {
                    data.bools.Add(new BoolFlag { name = pair.Key, value = pair.Value });
                }

                foreach (var pair in session.World.Numbers)
                {
                    data.numbers.Add(new NumberFlag { name = pair.Key, value = pair.Value });
                }

                foreach (var pair in session.World.Texts)
                {
                    data.texts.Add(new TextFlag { name = pair.Key, value = pair.Value });
                }
            }

            var taskLog = UnityEngine.Object.FindAnyObjectByType<TaskLog>();

            if (taskLog != null)
            {
                foreach (var entry in taskLog.Active)
                {
                    data.tasks.Add(entry.Id);
                }
            }

            TimedEvent.CollectDone(data.firedEvents, data.skippedEvents);

            var player = UnityEngine.Object.FindAnyObjectByType<PlayerInteractor>();

            if (player != null)
            {
                data.hasPlayer = true;
                data.playerPosition = player.transform.position;
                data.playerYaw = player.transform.eulerAngles.y;
            }

            return data;
        }
    }
}
