using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Radio.Minigames
{
    /// <summary>
    /// Правила мини-игры «Провод»: сигнал бежит по проводу не останавливаясь,
    /// и на каждой точке нужно успеть нажать клавишу, которая стоит на этом месте.
    /// </summary>
    public sealed class WirePuzzleGame : MonoBehaviour
    {
        private enum State
        {
            Idle,
            Running,
            Failed,
            Won,
        }

        [Header("Ссылки")]
        [SerializeField] private WirePuzzleView view;

        [Header("Уровень")]
        [Tooltip("Если пусто, уровень передаётся при запуске.")]
        [SerializeField] private WirePuzzle puzzle;

        [Header("Поведение")]
        [Tooltip("Пауза после промаха перед перезапуском, с.")]
        [SerializeField] private float restartDelay = 0.9f;

        [Tooltip("Разрешить бросить мини-игру по Esc. По умолчанию выключено: " +
                 "выйти можно, только пройдя. Включается, если проверка покажет, " +
                 "что уровень слишком злой.")]
        [SerializeField] private bool allowAbort;

        private Vector2[] _path;
        private Key[] _keys;
        private float[] _arrival;      // расстояние до каждой точки от старта
        private float _totalLength;

        private State _state = State.Idle;
        private float _distance;
        private int _nextPoint;
        private float _stateTimer;

        /// <summary>Игрок прошёл провод целиком.</summary>
        public event Action Won;

        /// <summary>Игрок бросил мини-игру. Приходит только если разрешён выход.</summary>
        public event Action Aborted;

        public void Begin(WirePuzzle level)
        {
            if (level != null)
            {
                puzzle = level;
            }

            if (puzzle == null || view == null)
            {
                Debug.LogError($"{nameof(WirePuzzleGame)}: не задан уровень или поле.", this);
                return;
            }

            if (!puzzle.TryBuildPath(out _path, out _keys))
            {
                Debug.LogError($"{nameof(WirePuzzleGame)}: в проводе меньше двух клеток.", this);
                return;
            }

            MeasurePath();
            view.Build(puzzle);
            Restart();
        }

        private void MeasurePath()
        {
            _arrival = new float[_path.Length];
            _arrival[0] = 0f;

            for (var i = 1; i < _path.Length; i++)
            {
                _arrival[i] = _arrival[i - 1] + Vector2.Distance(_path[i - 1], _path[i]);
            }

            _totalLength = _arrival[_path.Length - 1];
        }

        private void Restart()
        {
            _distance = 0f;
            _nextPoint = 1;
            _stateTimer = 0f;
            _state = State.Running;

            view.SetProgress(0);
            view.SetBeacon(_path[0]);
            view.SetStatus("Сигнал пошёл. Жми клавишу на каждом узле.", Color.white);
        }

        private void Update()
        {
            switch (_state)
            {
                case State.Running:
                    Run();
                    break;
                case State.Failed:
                    Wait(restartDelay, Restart);
                    break;
                case State.Won:
                    Wait(puzzle.FinishDelay, Finish);
                    break;
            }
        }

        private void Wait(float delay, Action next)
        {
            _stateTimer += Time.unscaledDeltaTime;

            if (_stateTimer >= delay)
            {
                _stateTimer = 0f;
                next();
            }
        }

        private void Run()
        {
            // unscaledDeltaTime, чтобы мини-игра пережила возможную паузу игры.
            _distance = Mathf.Min(_totalLength, _distance + puzzle.Speed * Time.unscaledDeltaTime);
            view.SetBeacon(PositionAt(_distance));
            view.SetProgress(_nextPoint - 1);

            ReadInput();

            if (_state != State.Running)
            {
                return;
            }

            // Окно задано в секундах, но сравнивать удобнее по расстоянию: скорость
            // постоянная, и лишнего пересчёта времени не нужно.
            var window = puzzle.HitWindow * puzzle.Speed;

            if (_distance > _arrival[_nextPoint] + window)
            {
                Fail("Пропустил узел.");
            }
        }

        private void ReadInput()
        {
            var keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (allowAbort && keyboard.escapeKey.wasPressedThisFrame)
            {
                _state = State.Idle;
                Aborted?.Invoke();
                return;
            }

            // Перебираем только клавиши поля: Shift, пробел и прочее к проводу
            // отношения не имеют, и ронять из-за них прохождение незачем.
            foreach (var cell in KeyboardGrid.Cells)
            {
                if (!keyboard[cell.Key].wasPressedThisFrame)
                {
                    continue;
                }

                Press(cell.Key);
                return;
            }
        }

        private void Press(Key key)
        {
            var window = puzzle.HitWindow * puzzle.Speed;
            var inWindow = Mathf.Abs(_distance - _arrival[_nextPoint]) <= window;

            if (!inWindow)
            {
                Fail("Рано.");
                return;
            }

            if (key != _keys[_nextPoint])
            {
                Fail("Не та клавиша.");
                return;
            }

            _nextPoint++;
            view.SetProgress(_nextPoint - 1);

            if (_nextPoint < _path.Length)
            {
                view.SetStatus("Есть контакт.", Color.white);
                return;
            }

            _state = State.Won;
            _stateTimer = 0f;
            view.SetStatus("Сигнал прошёл.", new Color(0.45f, 0.95f, 0.55f));
        }

        private void Fail(string reason)
        {
            _state = State.Failed;
            _stateTimer = 0f;
            view.SetStatus(reason + " Сигнал сорвался.", new Color(1f, 0.45f, 0.45f));
        }

        private void Finish()
        {
            _state = State.Idle;
            Won?.Invoke();
        }

        /// <summary>Точка на ломаной по пройденному расстоянию.</summary>
        private Vector2 PositionAt(float distance)
        {
            for (var i = 1; i < _path.Length; i++)
            {
                if (distance > _arrival[i])
                {
                    continue;
                }

                var segment = _arrival[i] - _arrival[i - 1];
                var t = segment <= 0.0001f ? 0f : (distance - _arrival[i - 1]) / segment;
                return Vector2.Lerp(_path[i - 1], _path[i], t);
            }

            return _path[_path.Length - 1];
        }
    }
}
