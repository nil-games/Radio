using Radio.World;
using UnityEngine;
using Yarn.Unity;

namespace Radio.Dialogue
{
    /// <summary>
    /// Команды, которые диалог может отдать игре: сдвинуть часы смены.
    /// </summary>
    /// <remarks>
    /// Регистрируются через AddCommandHandler, а не атрибутом YarnCommand: атрибут ищет
    /// в сцене объект по имени и годится для «повернуть вот этот предмет», а здесь речь
    /// о состоянии всей смены, у которого никакого объекта в кадре нет.
    /// </remarks>
    public sealed class RadioCommands : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private DialogueRunner runner;

        private void Awake()
        {
            if (runner == null)
            {
                runner = GetComponent<DialogueRunner>();
            }

            if (runner == null)
            {
                Debug.LogError($"{nameof(RadioCommands)}: не задан Dialogue Runner. " +
                               "Команды времени не будут работать.", this);
                enabled = false;
                return;
            }

            // Регистрируем в Awake: к первому диалогу команды обязаны быть на месте,
            // иначе Yarn сообщит о неизвестной команде и просто пойдёт дальше.
            runner.AddCommandHandler("advance_time", (System.Action<int>)AdvanceTime);
            runner.AddCommandHandler("advance_time_to", (System.Action<int, int>)AdvanceTimeTo);
        }

        /// <summary>Сдвинуть часы смены вперёд: advance_time 15.</summary>
        private void AdvanceTime(int minutes)
        {
            var time = Time;

            if (time != null)
            {
                time.AdvanceBy(minutes);
            }
        }

        /// <summary>Довести часы до отметки: advance_time_to 1 10 — это 01:10.</summary>
        private void AdvanceTimeTo(int hour, int minute)
        {
            var time = Time;

            if (time != null)
            {
                time.AdvanceTo(hour, minute);
            }
        }

        private TimeManager Time
        {
            get
            {
                var session = GameSession.Current;

                if (session != null)
                {
                    return session.Time;
                }

                Debug.LogError($"{nameof(RadioCommands)}: в сцене нет {nameof(GameSession)}, " +
                               "сдвинуть время некому.", this);
                return null;
            }
        }
    }
}
