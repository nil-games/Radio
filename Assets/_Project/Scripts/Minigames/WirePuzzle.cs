using UnityEngine;
using UnityEngine.InputSystem;

namespace Radio.Minigames
{
    /// <summary>
    /// Сложность мини-игры «Провод»: сколько клавиш нажать и как быстро бежит сигнал.
    /// </summary>
    /// <remarks>
    /// По умолчанию провод собирается случайно под заданную сложность, поэтому один
    /// и тот же ассет годится на всю ночь: наизусть его не выучить. Заданный вручную
    /// путь оставлен для сюжетных мест, где провод должен быть тем самым.
    /// </remarks>
    [CreateAssetMenu(fileName = "WirePuzzle", menuName = "Радио/Мини-игра «Провод»")]
    public sealed class WirePuzzle : ScriptableObject
    {
        public enum PathSource
        {
            /// <summary>Провод собирается случайно под заданную сложность.</summary>
            Random,

            /// <summary>Провод прописан клавишами вручную.</summary>
            Fixed,
        }

        [Header("Сложность")]
        [Tooltip("Сколько клавиш нужно нажать за прохождение. Стартовая клетка не считается.")]
        [SerializeField] private int pointCount = 5;

        [Tooltip("Скорость сигнала, клеток в секунду.")]
        [SerializeField] private float speed = 2f;

        [Tooltip("Насколько рано и насколько поздно засчитывается нажатие, с. " +
                 "Окно симметричное: столько же до узла, сколько и после.")]
        [SerializeField] private float hitWindow = 0.25f;

        [Header("Провод")]
        [SerializeField] private PathSource source = PathSource.Random;

        [Tooltip("Кратчайший и самый длинный отрезок между узлами, в клетках. " +
                 "Нижняя граница нужна, чтобы на соседние клавиши не оставалось " +
                 "меньше мгновения, верхняя — чтобы сигнал не полз через всё поле.")]
        [SerializeField] private float minStep = 2.5f;

        [SerializeField] private float maxStep = 7f;

        [Tooltip("0 — каждый запуск даёт новый провод. Любое другое число повторяет " +
                 "один и тот же: удобно, когда нужно поймать конкретный случай.")]
        [SerializeField] private int seed;

        [Tooltip("Путь вручную. Используется, только если источник — Fixed. " +
                 "Первая клавиша задаёт старт, остальные становятся узлами.")]
        [SerializeField]
        private Key[] waypoints =
        {
            Key.Digit1, Key.Digit5, Key.C, Key.Comma, Key.L, Key.Quote,
        };

        [Header("Прочее")]
        [Tooltip("Сколько ждать после победы, прежде чем закрыть поле, с. " +
                 "Нужно, чтобы игрок увидел, что сигнал дошёл до конца.")]
        [SerializeField] private float finishDelay = 0.8f;

        public float Speed => Mathf.Max(0.1f, speed);

        public float HitWindow => Mathf.Max(0.02f, hitWindow);

        public float FinishDelay => Mathf.Max(0f, finishDelay);

        public int PointCount => Mathf.Max(1, pointCount);

        /// <summary>
        /// Готовит провод: случайный под сложность или прописанный вручную.
        /// Зовётся один раз на запуск мини-игры, а не на каждую попытку — провалившись,
        /// игрок должен проходить тот же провод, иначе выучить его невозможно в принципе.
        /// </summary>
        public bool TryCreatePath(out Vector2[] path, out Key[] keys)
        {
            path = null;
            keys = null;

            var source = SelectKeys();

            if (source == null || source.Length < 2)
            {
                return false;
            }

            var points = new System.Collections.Generic.List<Vector2>(source.Length);
            var used = new System.Collections.Generic.List<Key>(source.Length);

            foreach (var key in source)
            {
                if (!KeyboardGrid.TryGet(key, out var cell))
                {
                    Debug.LogError($"{name}: клавиши {key} нет в раскладке поля, узел пропущен.", this);
                    continue;
                }

                points.Add(cell.Center);
                used.Add(key);
            }

            path = points.ToArray();
            keys = used.ToArray();
            return path.Length >= 2;
        }

        private Key[] SelectKeys()
        {
            if (source == PathSource.Fixed)
            {
                return waypoints;
            }

            return WirePathGenerator.TryGenerate(PointCount, seed, minStep, maxStep, out var generated)
                ? generated
                : null;
        }

        private void OnValidate()
        {
            if (source == PathSource.Fixed && waypoints != null && waypoints.Length < 2)
            {
                Debug.LogWarning($"{name}: в проводе меньше двух клеток, играть не во что.", this);
            }

            if (maxStep < minStep)
            {
                maxStep = minStep;
            }
        }
    }
}
