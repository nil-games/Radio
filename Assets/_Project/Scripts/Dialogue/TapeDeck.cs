using Radio.World;
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
    /// <para/>
    /// Кассетников в сцене может быть несколько: у каждого своё имя в командах. Микшер
    /// зовёт «tape» (play_tape, stop_tape, tape_playing), плеер с песнями — «cassette».
    /// Раздельно, чтобы песня на плеере не гасила варианты микшера, пока играет.
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    public sealed class TapeDeck : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private DialogueRunner runner;

        [Tooltip("Имя в командах диалога: play_<имя>, stop_<имя>, <имя>_playing().")]
        [SerializeField] private string commandName = "tape";

        [Header("Записи")]
        [Tooltip("Что можно поставить из диалога. В команде запись называется по имени клипа.")]
        [SerializeField] private AudioClip[] tapes;

        [Header("Подсказка")]
        [Tooltip("Задача-подсказка, которая висит, пока играет запись: появляется при запуске " +
                 "и снимается, когда запись кончилась или её заглушили. Пусто — никакой.")]
        [SerializeField] private string taskWhilePlaying;

        private AudioSource _source;
        private TaskLog _tasks;
        private bool _hintShown;

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
            runner.AddCommandHandler($"play_{commandName}", (System.Action<string, float>)Play);
            runner.AddCommandHandler($"stop_{commandName}", (System.Action)Stop);
            runner.AddFunction($"{commandName}_playing", (System.Func<bool>)(() => IsPlaying));
        }

        /// <summary>Поставить запись: play_tape 103FM 0.5 — громкость от 0 до 1.</summary>
        private void Play(string tapeName, float volume)
        {
            var clip = Find(tapeName);

            if (clip == null)
            {
                // Заглушка: песни ещё нет. Сюжет идёт дальше, просто без звука — положи
                // клип с этим именем в список Tapes, и он заиграет.
                Debug.LogWarning($"{nameof(TapeDeck)}: записи «{tapeName}» нет в списке кассет " +
                                 $"({commandName}) — заглушка, играет тишина.", this);
                return;
            }

            _source.clip = clip;
            _source.volume = Mathf.Clamp01(volume);
            _source.Play();

            if (!string.IsNullOrWhiteSpace(taskWhilePlaying) && ResolveTasks())
            {
                _tasks.Add(taskWhilePlaying);
                _hintShown = true;
            }
        }

        // Конец записи ловим опросом: у AudioSource нет события «доиграл».
        private void Update()
        {
            if (!_hintShown || IsPlaying)
            {
                return;
            }

            _hintShown = false;

            if (ResolveTasks())
            {
                _tasks.Complete(taskWhilePlaying);
            }
        }

        private bool ResolveTasks()
        {
            if (_tasks == null)
            {
                _tasks = FindAnyObjectByType<TaskLog>();
            }

            return _tasks != null;
        }

        /// <summary>Заглушить запись: stop_tape.</summary>
        private void Stop() => _source.Stop();

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
