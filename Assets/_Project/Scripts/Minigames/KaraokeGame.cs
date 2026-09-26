using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Radio.Minigames
{
    /// <summary>
    /// Правила мини-игры «Гитара»: играет песня, и на каждое слово нужно вовремя
    /// нажать его первую букву. Провала нет — песня идёт до конца, затем итог.
    /// Выйти можно в любой момент кликом мыши.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class KaraokeGame : MonoBehaviour
    {
        public enum Judgement
        {
            None,
            Perfect,
            Good,
            Miss,
        }

        private enum State
        {
            Idle,
            Playing,
            Results,
        }

        [Header("Ссылки")]
        [SerializeField] private KaraokeView view;

        [Tooltip("Если пусто, берётся с этого же объекта.")]
        [SerializeField] private AudioSource audioSource;

        private KaraokeSong _song;
        private KaraokeSong.Note[] _notes;
        private Judgement[] _judged;
        private readonly HashSet<Key> _keys = new HashSet<Key>();

        private State _state = State.Idle;
        private float _time;
        private int _firstPending;
        private bool _mouseHeld;

        private int _perfect;
        private int _good;
        private int _streak;
        private int _bestStreak;

        /// <summary>Игрок вышел: сам, кликом, или после итога. Звук уже остановлен.</summary>
        public event Action Finished;

        public bool IsRunning => _state != State.Idle;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }

        public bool Begin(KaraokeSong song)
        {
            if (song == null || view == null)
            {
                Debug.LogError($"{nameof(KaraokeGame)}: не задана песня или поле.", this);
                return false;
            }

            if (!song.TryBuild(out _notes, out var lines))
            {
                return false;
            }

            _song = song;
            _judged = new Judgement[_notes.Length];
            _keys.Clear();

            foreach (var note in _notes)
            {
                _keys.Add(note.Key);
            }

            _time = 0f;
            _firstPending = 0;
            _perfect = 0;
            _good = 0;
            _streak = 0;
            _bestStreak = 0;

            // Кнопка, которую держали в момент запуска, не должна сразу же и выкинуть:
            // выход срабатывает только на новое нажатие.
            _mouseHeld = IsMouseHeld();

            view.Build(_notes, lines, song.LeadTime);
            view.SetScore(0, _notes.Length, 0);
            view.SetStatus(string.Empty, Color.white);

            audioSource.clip = song.Clip;
            audioSource.time = 0f;
            audioSource.Play();

            _state = State.Playing;
            return true;
        }

        /// <summary>Прервать игру снаружи, без события: например, если выключают сцену.</summary>
        public void Stop()
        {
            audioSource.Stop();
            _state = State.Idle;
        }

        private void Update()
        {
            if (_state == State.Idle)
            {
                return;
            }

            if (ExitClicked())
            {
                Exit();
                return;
            }

            if (_state != State.Playing)
            {
                return;
            }

            AdvanceTime();
            ExpireMissedNotes();
            ReadKeys();
            view.Render(_time, _firstPending);

            if (_firstPending >= _notes.Length
                && _time > _notes[_notes.Length - 1].Time + _song.OutroDelay)
            {
                ShowResults();
            }
        }

        private bool ExitClicked()
        {
            // Фронт нажатия ловим сами, а не через wasPressedThisFrame: тот теряет клик,
            // если событие обработалось не в игровом обновлении Input System.
            var held = IsMouseHeld();
            var clicked = held && !_mouseHeld;
            _mouseHeld = held;
            return clicked;
        }

        private static bool IsMouseHeld()
        {
            var mouse = Mouse.current;
            return mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed);
        }

        private void AdvanceTime()
        {
            // Время берём из самого звука, а не копим из deltaTime: накопленное за три минуты
            // разошлось бы с музыкой, и попадать пришлось бы не в слово, а в свою ошибку.
            if (audioSource.isPlaying && audioSource.clip != null)
            {
                _time = audioSource.timeSamples / (float)audioSource.clip.frequency;
                return;
            }

            // Песня кончилась — дальше идём по часам, чтобы досчитать хвост до итога.
            _time += Time.unscaledDeltaTime;
        }

        private void ExpireMissedNotes()
        {
            var window = _song.HitWindow;

            for (var i = _firstPending; i < _notes.Length; i++)
            {
                if (_notes[i].Time + window >= _time)
                {
                    break;
                }

                if (_judged[i] == Judgement.None)
                {
                    Judge(i, Judgement.Miss);
                }
            }

            SkipJudged();
        }

        private void ReadKeys()
        {
            var keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            // Перебираем только буквы этой песни: остальные клавиши к ней отношения не имеют,
            // и ронять серию из-за случайного Shift незачем.
            foreach (var key in _keys)
            {
                if (keyboard[key].wasPressedThisFrame)
                {
                    Press(key);
                }
            }
        }

        private void Press(Key key)
        {
            var window = _song.HitWindow;

            // Засчитываем самую раннюю неотыгранную ноту с этой буквой в окне. Так тесно
            // стоящие слова не крадут попадание друг у друга: у соседей обычно разные буквы.
            for (var i = _firstPending; i < _notes.Length; i++)
            {
                var delta = _time - _notes[i].Time;

                if (delta < -window)
                {
                    break;
                }

                if (_judged[i] != Judgement.None || _notes[i].Key != key || delta > window)
                {
                    continue;
                }

                Judge(i, Mathf.Abs(delta) <= _song.PerfectWindow ? Judgement.Perfect : Judgement.Good);
                SkipJudged();
                return;
            }

            _streak = 0;
            view.SetStatus("Мимо", KaraokeView.MissColor);
            view.SetScore(_perfect + _good, _notes.Length, _streak);
        }

        private void Judge(int index, Judgement judgement)
        {
            _judged[index] = judgement;

            switch (judgement)
            {
                case Judgement.Perfect:
                    _perfect++;
                    _streak++;
                    view.SetStatus("Точно", KaraokeView.PerfectColor);
                    break;
                case Judgement.Good:
                    _good++;
                    _streak++;
                    view.SetStatus("Хорошо", KaraokeView.GoodColor);
                    break;
                case Judgement.Miss:
                    _streak = 0;
                    view.SetStatus("Пропуск", KaraokeView.MissColor);
                    break;
            }

            _bestStreak = Mathf.Max(_bestStreak, _streak);
            view.MarkNote(index, judgement);
            view.SetScore(_perfect + _good, _notes.Length, _streak);
        }

        private void SkipJudged()
        {
            while (_firstPending < _notes.Length && _judged[_firstPending] != Judgement.None)
            {
                _firstPending++;
            }
        }

        private void ShowResults()
        {
            _state = State.Results;
            view.ShowResults(_perfect, _good, _notes.Length, _bestStreak);
        }

        private void Exit()
        {
            audioSource.Stop();
            _state = State.Idle;
            Finished?.Invoke();
        }
    }
}
