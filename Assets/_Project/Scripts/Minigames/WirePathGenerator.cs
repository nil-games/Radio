using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Radio.Minigames
{
    /// <summary>
    /// Собирает случайный провод по клеткам клавиатуры.
    /// </summary>
    /// <remarks>
    /// Случайность тут с правилами, иначе провод выходит неиграбельным: два узла
    /// подряд на соседних клавишах не дают времени среагировать, а три узла на одной
    /// прямой сливаются в отрезок, на котором средний узел не разглядеть.
    /// </remarks>
    public static class WirePathGenerator
    {
        /// <summary>
        /// Строит путь из <paramref name="pointCount"/> узлов плюс стартовая клетка.
        /// Возвращает false, если правила не удалось соблюсти даже после послаблений.
        /// </summary>
        /// <param name="seed">0 — каждый раз новый провод, иначе повторяемый.</param>
        public static bool TryGenerate(int pointCount, int seed, float minStep, float maxStep, out Key[] keys)
        {
            keys = null;

            if (pointCount < 1)
            {
                return false;
            }

            var random = seed == 0 ? new System.Random() : new System.Random(seed);
            var cells = KeyboardGrid.Cells;

            // Несколько попыток целиком: правила могут завести в клетку, из которой
            // следующий узел уже не выбрать, и начать заново дешевле, чем отступать назад.
            for (var attempt = 0; attempt < 24; attempt++)
            {
                // С каждой третьей неудачей ослабляем требования к длине отрезка,
                // иначе на тесных настройках генератор упирается навсегда.
                var relax = attempt / 3 * 0.5f;
                var min = Mathf.Max(1f, minStep - relax);
                var max = maxStep + relax;

                if (TryBuild(random, cells, pointCount, min, max, out keys))
                {
                    return true;
                }
            }

            Debug.LogError($"{nameof(WirePathGenerator)}: не удалось собрать провод из {pointCount} узлов.");
            return false;
        }

        private static bool TryBuild(System.Random random, IReadOnlyList<KeyboardGrid.Cell> cells,
            int pointCount, float minStep, float maxStep, out Key[] keys)
        {
            var used = new List<KeyboardGrid.Cell>(pointCount + 1) { cells[random.Next(cells.Count)] };
            var candidates = new List<KeyboardGrid.Cell>(cells.Count);

            for (var step = 0; step < pointCount; step++)
            {
                var current = used[used.Count - 1];
                candidates.Clear();

                foreach (var cell in cells)
                {
                    if (!IsGoodNext(cell, current, used, minStep, maxStep))
                    {
                        continue;
                    }

                    candidates.Add(cell);
                }

                if (candidates.Count == 0)
                {
                    keys = null;
                    return false;
                }

                used.Add(candidates[random.Next(candidates.Count)]);
            }

            keys = new Key[used.Count];

            for (var i = 0; i < used.Count; i++)
            {
                keys[i] = used[i].Key;
            }

            return true;
        }

        private static bool IsGoodNext(KeyboardGrid.Cell candidate, KeyboardGrid.Cell current,
            List<KeyboardGrid.Cell> used, float minStep, float maxStep)
        {
            // Клавишу дважды не берём: иначе игрок жмёт одно и то же и путается,
            // где он сейчас на проводе.
            foreach (var cell in used)
            {
                if (cell.Key == candidate.Key)
                {
                    return false;
                }
            }

            var direction = candidate.Center - current.Center;
            var distance = direction.magnitude;

            if (distance < minStep || distance > maxStep)
            {
                return false;
            }

            if (used.Count < 2)
            {
                return true;
            }

            // Три узла на одной прямой: средний потеряется внутри отрезка.
            var previous = (current.Center - used[used.Count - 2].Center).normalized;
            return Mathf.Abs(Vector2.Dot(previous, direction / distance)) < 0.98f;
        }
    }
}
