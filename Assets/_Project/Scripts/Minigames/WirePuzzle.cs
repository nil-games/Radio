using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Radio.Minigames
{
    /// <summary>
    /// Уровень мини-игры «Провод»: по каким клеткам идёт провод и с какой скоростью
    /// по нему бежит сигнал.
    /// </summary>
    /// <remarks>
    /// Весь уровень — это список клавиш. Первая задаёт, откуда стартует маячок,
    /// каждая следующая — точка, на которой нужно нажать. Провод рисуется прямыми
    /// отрезками между центрами клеток, поэтому изломы и концы получаются сами,
    /// а нарисовать новый провод значит вписать несколько клавиш.
    /// </remarks>
    [CreateAssetMenu(fileName = "WirePuzzle", menuName = "Радио/Мини-игра «Провод»")]
    public sealed class WirePuzzle : ScriptableObject
    {
        [Header("Провод")]
        [Tooltip("Клетки по порядку. Первая — старт, остальные — точки нажатия.")]
        [SerializeField]
        private Key[] waypoints =
        {
            Key.Digit1, Key.Digit5, Key.Z, Key.B, Key.H, Key.Quote,
        };

        [Header("Сложность")]
        [Tooltip("Скорость сигнала, клеток в секунду.")]
        [SerializeField] private float speed = 3.5f;

        [Tooltip("Насколько рано и насколько поздно засчитывается нажатие, с. " +
                 "Окно симметричное: столько же до точки, сколько и после.")]
        [SerializeField] private float hitWindow = 0.22f;

        [Tooltip("Сколько ждать после победы, прежде чем закрыть поле, с. " +
                 "Нужно, чтобы игрок увидел, что сигнал дошёл до конца.")]
        [SerializeField] private float finishDelay = 0.8f;

        public Key[] Waypoints => waypoints;

        public float Speed => Mathf.Max(0.1f, speed);

        public float HitWindow => Mathf.Max(0.02f, hitWindow);

        public float FinishDelay => Mathf.Max(0f, finishDelay);

        /// <summary>Точек, на которых нужно нажать: все клетки, кроме стартовой.</summary>
        public int PointCount => Mathf.Max(0, waypoints.Length - 1);

        /// <summary>
        /// Центры клеток провода. Клавиша, которой нет в раскладке, пропускается:
        /// иначе одна опечатка в уровне рвала бы провод пополам без единой жалобы.
        /// </summary>
        public bool TryBuildPath(out Vector2[] path, out Key[] keys)
        {
            var points = new System.Collections.Generic.List<Vector2>(waypoints.Length);
            var used = new System.Collections.Generic.List<Key>(waypoints.Length);

            foreach (var key in waypoints)
            {
                if (!KeyboardGrid.TryGet(key, out var cell))
                {
                    Debug.LogError($"{name}: клавиши {key} нет в раскладке поля, точка пропущена.", this);
                    continue;
                }

                points.Add(cell.Center);
                used.Add(key);
            }

            path = points.ToArray();
            keys = used.ToArray();
            return path.Length >= 2;
        }

        private void OnValidate()
        {
            if (waypoints != null && waypoints.Length < 2)
            {
                Debug.LogWarning($"{name}: в проводе меньше двух клеток, играть не во что.", this);
            }
        }
    }
}
