using UnityEngine;
using Yarn.Unity;

namespace Radio.Dialogue
{
    /// <summary>
    /// Кассетник, которым управляет диалог: <c>&lt;&lt;play_tape 103FM 0.5&gt;&gt;</c> ставит
    /// запись, а функция <c>tape_playing()</c> говорит, играет ли она ещё.
    /// </summary>
    /// <remarks>
    /// Команда не ждёт конца записи: пока она играет, диалог показывает выбор с погашенными
    /// вариантами, а вернуть их — забота <see cref="TapeGatedOptionsPresenter"/>.
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    public sealed class TapeDeck : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private DialogueRunner runner;

        [Header("Записи")]
        [Tooltip("Что можно поставить из диалога. В команде запись называется по имени клипа.")]
        [SerializeField] private AudioClip[] tapes;

        private AudioSource _source;

        /// <summary>Играет ли запись прямо сейчас.</summary>
        public bool IsPlaying => _source != null && _source.isPlaying;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;

            if (runner == null)
            {
                runner = GetComponent<DialogueRunner>();
            }

            if (runner == null)
            {
                Debug.LogError($"{nameof(TapeDeck)}: не задан Dialogue Runner. " +
                               "Кассеты из диалога ставиться не будут.", this);
                enabled = false;
                return;
            }

            // Регистрируем в Awake: к первому диалогу команда и функция обязаны быть на месте.
            runner.AddCommandHandler("play_tape", (System.Action<string, float>)Play);
            runner.AddFunction("tape_playing", (System.Func<bool>)(() => IsPlaying));
        }

        /// <summary>Поставить запись: play_tape 103FM 0.5 — громкость от 0 до 1.</summary>
        private void Play(string tapeName, float volume)
        {
            var clip = Find(tapeName);

            if (clip == null)
            {
                Debug.LogError($"{nameof(TapeDeck)}: записи «{tapeName}» нет в списке кассет.", this);
                return;
            }

            _source.clip = clip;
            _source.volume = Mathf.Clamp01(volume);
            _source.Play();
        }

        private AudioClip Find(string tapeName)
        {
            if (tapes == null)
            {
                return null;
            }

            foreach (var tape in tapes)
            {
                if (tape != null && tape.name == tapeName)
                {
                    return tape;
                }
            }

            return null;
        }
    }
}
