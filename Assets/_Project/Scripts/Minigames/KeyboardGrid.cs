using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Radio.Minigames
{
    /// <summary>
    /// Раскладка клавиатуры как сетка клеток: четыре ряда от цифр до нижнего.
    /// Единственное место, где описана геометрия клавиатуры.
    /// </summary>
    /// <remarks>
    /// Клавиши хранятся кодами <see cref="Key"/>, а не символами. В Input System этот код
    /// позиционный: Key.Q — это клавиша на месте Q при любой раскладке операционной системы.
    /// Поэтому ЙЦУКЕН, QWERTY и любая другая раскладка отрабатывают одинаково,
    /// и перекодировать ничего не нужно.
    /// </remarks>
    public static class KeyboardGrid
    {
        public readonly struct Cell
        {
            public readonly Key Key;
            public readonly int Row;
            public readonly int Column;
            public readonly string Cyrillic;
            public readonly string Latin;

            public Cell(Key key, int row, int column, string cyrillic, string latin)
            {
                Key = key;
                Row = row;
                Column = column;
                Cyrillic = cyrillic;
                Latin = latin;
            }

            /// <summary>
            /// Центр клетки в клетках же. X вправо, Y вниз и со знаком минус —
            /// так же, как считает UI, у которого начало координат сверху слева.
            /// </summary>
            public Vector2 Center => new Vector2(
                Column + RowOffsets[Row] + 0.5f,
                -(Row + 0.5f));
        }

        /// <summary>
        /// Сдвиг каждого ряда вправо, в клетках. Числа не выдуманы: на стандартной
        /// клавиатуре 1 стоит на 1u от края, Q на 1.5u, A на 1.75u, Z на 2.25u.
        /// Разница с рядом цифр и даёт эту лесенку.
        /// </summary>
        public static readonly float[] RowOffsets = { 0f, 0.5f, 0.75f, 1.25f };

        public const int RowCount = 4;

        private static readonly Cell[] All = BuildLayout();

        public static IReadOnlyList<Cell> Cells => All;

        /// <summary>Ширина поля в клетках: по самому правому краю всех рядов.</summary>
        public static float Width
        {
            get
            {
                var width = 0f;

                foreach (var cell in All)
                {
                    width = Mathf.Max(width, cell.Column + RowOffsets[cell.Row] + 1f);
                }

                return width;
            }
        }

        public static bool TryGet(Key key, out Cell cell)
        {
            foreach (var candidate in All)
            {
                if (candidate.Key != key)
                {
                    continue;
                }

                cell = candidate;
                return true;
            }

            cell = default;
            return false;
        }

        private static Cell[] BuildLayout()
        {
            var cells = new List<Cell>(46);

            AddRow(cells, 0,
                new[]
                {
                    Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6,
                    Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0, Key.Minus, Key.Equals, Key.Backspace,
                },
                new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "Bksp" },
                new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "" });

            AddRow(cells, 1,
                new[]
                {
                    Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y,
                    Key.U, Key.I, Key.O, Key.P, Key.LeftBracket, Key.RightBracket,
                },
                new[] { "Й", "Ц", "У", "К", "Е", "Н", "Г", "Ш", "Щ", "З", "Х", "Ъ" },
                new[] { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]" });

            AddRow(cells, 2,
                new[]
                {
                    Key.A, Key.S, Key.D, Key.F, Key.G, Key.H,
                    Key.J, Key.K, Key.L, Key.Semicolon, Key.Quote,
                },
                new[] { "Ф", "Ы", "В", "А", "П", "Р", "О", "Л", "Д", "Ж", "Э" },
                new[] { "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'" });

            AddRow(cells, 3,
                new[]
                {
                    Key.Z, Key.X, Key.C, Key.V, Key.B,
                    Key.N, Key.M, Key.Comma, Key.Period, Key.Slash,
                },
                new[] { "Я", "Ч", "С", "М", "И", "Т", "Ь", "Б", "Ю", "." },
                new[] { "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/" });

            return cells.ToArray();
        }

        private static void AddRow(List<Cell> cells, int row, Key[] keys, string[] cyrillic, string[] latin)
        {
            for (var column = 0; column < keys.Length; column++)
            {
                cells.Add(new Cell(keys[column], row, column, cyrillic[column], latin[column]));
            }
        }
    }
}
