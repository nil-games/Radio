using System;
using System.Collections.Generic;
using UnityEngine;

namespace Radio.Broadcast
{
    /// <summary>Как строить фигуру, если у оси вайба нет (GDD 02.22).</summary>
    public enum ShapeMode
    {
        [Tooltip("Вершина = max(значение, b). Пустая ось почти в центре, фигуры — звёзды.")]
        Underlay,

        [Tooltip("Вершина = значение + k. Фигуры — полные пятиугольники, как в Dispatch.")]
        Base,
    }

    /// <summary>Где остановилась точка проверки.</summary>
    public enum CheckOutcome
    {
        Fail,
        Success,

        /// <summary>Успех за счёт странной кассеты: в красной части фигуры, +1 Хтонь.</summary>
        StrangeSuccess,
    }

    /// <summary>
    /// Математика проверки заявки на пятиугольнике вайбов (GDD 02.22): фигуры, площадь
    /// пересечения, шанс и траектория точки, которая этот шанс показывает.
    /// </summary>
    /// <remarks>
    /// Фигуры звёздные относительно центра, но невыпуклые, поэтому всё считается по пяти
    /// секторам между соседними осями: в секторе каждая фигура — треугольник с вершиной в
    /// центре, а пересечение двух треугольников уже выпуклое и считается отсечением.
    /// Координаты — в единицах вайба: ось длины 3 = вайб 3.
    /// </remarks>
    public static class VibeRadar
    {
        public const int Axes = 5;

        /// <summary>Порядок осей по кругу: Протест соседствует с Тоской.</summary>
        public static readonly string[] Names = { "Тоска", "Драйв", "Нежность", "Ностальгия", "Протест" };

        private static readonly float SectorSin = Mathf.Sin(72f * Mathf.Deg2Rad);

        /// <summary>Направление оси: первая смотрит вверх, дальше по часовой стрелке.</summary>
        public static Vector2 Axis(int i)
        {
            var angle = (90f - i * 72f) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        public static Vector2 Vertex(float[] radii, int i) => Axis(i) * radii[i];

        /// <summary>Расстояния вершин от центра по значениям вайбов.</summary>
        public static float[] Radii(int[] values, ShapeMode mode, float pad)
        {
            var radii = new float[Axes];

            for (var i = 0; i < Axes; i++)
            {
                var value = values != null && i < values.Length ? values[i] : 0;
                radii[i] = mode == ShapeMode.Underlay ? Mathf.Max(value, pad) : value + pad;
            }

            return radii;
        }

        /// <summary>Сумма вайбов нескольких кассет: вайбы складываются (GDD 02.22).</summary>
        public static int[] Sum(IEnumerable<Cassette> cassettes)
        {
            var sum = new int[Axes];

            foreach (var cassette in cassettes)
            {
                for (var i = 0; i < Axes; i++)
                {
                    sum[i] += cassette.Vibes[i];
                }
            }

            return sum;
        }

        public static float Area(float[] radii)
        {
            var area = 0f;

            for (var i = 0; i < Axes; i++)
            {
                area += 0.5f * radii[i] * radii[(i + 1) % Axes] * SectorSin;
            }

            return area;
        }

        public static float IntersectionArea(float[] a, float[] b)
        {
            var area = 0f;

            foreach (var polygon in Intersection(a, b))
            {
                area += PolygonArea(polygon);
            }

            return area;
        }

        /// <summary>Пересечение двух фигур — по выпуклому многоугольнику на сектор.</summary>
        public static List<Vector2[]> Intersection(float[] a, float[] b)
        {
            var result = new List<Vector2[]>(Axes);

            for (var i = 0; i < Axes; i++)
            {
                var clipped = Clip(Sector(a, i), Sector(b, i));

                if (clipped.Count >= 3)
                {
                    result.Add(clipped.ToArray());
                }
            }

            return result;
        }

        /// <summary>Шанс успеха: какая доля фигуры заявки закрыта фигурой кассет.</summary>
        public static float Chance(float[] request, float[] cassettes)
        {
            var total = Area(request);
            return total <= 0f ? 0f : Mathf.Clamp01(IntersectionArea(request, cassettes) / total);
        }

        public static bool Contains(float[] radii, Vector2 point)
        {
            if (radii == null)
            {
                return false;
            }

            if (point.sqrMagnitude < 1e-8f)
            {
                return true;
            }

            var sector = SectorOf(point);
            var a = Vertex(radii, sector);
            var b = Vertex(radii, (sector + 1) % Axes);

            // Внутри треугольника (центр, a, b): с той же стороны ребра ab, что и центр.
            return Cross(b - a, point - a) * Cross(b - a, -a) >= 0f;
        }

        /// <summary>
        /// Траектория точки, которая остановится в нужной области: внутри фигуры кассет при
        /// успехе, в красной части при странном успехе, снаружи при провале.
        /// </summary>
        /// <remarks>
        /// Исход решён заранее броском, а движение подбирается под него: перебираем
        /// стартовые скорости и честно моделируем отскоки, пока точка не остановится где надо.
        /// Так процент на экране равен шансу, а полёт выглядит физическим. Если подобрать не
        /// вышло (область крошечная), точка просто плавно едет из центра в случайную точку
        /// нужной области — фигура звёздная, прямая из центра стенок не пересекает.
        /// </remarks>
        public static List<Vector2> Trajectory(float[] request, float[] all, float[] normal,
            CheckOutcome outcome, System.Random random, float step = 1f / 60f)
        {
            for (var attempt = 0; attempt < 300; attempt++)
            {
                var path = Simulate(request, random, step);

                if (Classify(path[path.Count - 1], all, normal) == outcome)
                {
                    return path;
                }
            }

            return Glide(PickPoint(request, all, normal, outcome, random), step);
        }

        public static CheckOutcome Classify(Vector2 point, float[] all, float[] normal)
        {
            if (normal != null && Contains(normal, point))
            {
                return CheckOutcome.Success;
            }

            return all != null && Contains(all, point) ? CheckOutcome.StrangeSuccess : CheckOutcome.Fail;
        }

        private static List<Vector2> Simulate(float[] request, System.Random random, float step)
        {
            var path = new List<Vector2>(256) { Vector2.zero };
            var reach = Max(request);
            var angle = (float)(random.NextDouble() * Math.PI * 2.0);
            // Скорость и затухание — в долях размера фигуры, поэтому полёт одинаково живой у
            // маленькой и у большой заявки: путь в 3–4,5 размера фигуры (несколько отскоков),
            // остановка примерно через 2,5–3 секунды.
            var speed = reach * (5.5f + (float)random.NextDouble() * 2.5f);
            var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            var position = Vector2.zero;
            const float damping = 1.8f;
            var stop = reach * 0.05f;

            for (var i = 0; i < 1200 && velocity.magnitude > stop; i++)
            {
                var next = position + velocity * step;

                if (!Contains(request, next))
                {
                    // Отскок от стенки фигуры заявки: отражаем скорость от ребра сектора
                    // и сразу летим дальше — без кадра на месте, иначе точка «липнет» к стенке.
                    var sector = SectorOf(next);
                    var a = Vertex(request, sector);
                    var b = Vertex(request, (sector + 1) % Axes);
                    var edge = (b - a).normalized;
                    var normalDir = new Vector2(-edge.y, edge.x);

                    if (Vector2.Dot(normalDir, -a) < 0f)
                    {
                        normalDir = -normalDir;
                    }

                    velocity -= 2f * Vector2.Dot(velocity, normalDir) * normalDir;
                    next = position + velocity * step;

                    if (!Contains(request, next))
                    {
                        next = position;
                    }
                }

                position = next;
                velocity *= Mathf.Exp(-damping * step);
                path.Add(position);
            }

            return path;
        }

        private static Vector2 PickPoint(float[] request, float[] all, float[] normal,
            CheckOutcome outcome, System.Random random)
        {
            var reach = Max(request);

            for (var i = 0; i < 20000; i++)
            {
                var point = new Vector2(
                    (float)(random.NextDouble() * 2.0 - 1.0) * reach,
                    (float)(random.NextDouble() * 2.0 - 1.0) * reach);

                if (Contains(request, point) && Classify(point, all, normal) == outcome)
                {
                    return point;
                }
            }

            return Vector2.zero;
        }

        private static List<Vector2> Glide(Vector2 target, float step)
        {
            var path = new List<Vector2>();
            const float duration = 1.6f;

            for (var t = 0f; t < duration; t += step)
            {
                var k = 1f - Mathf.Pow(1f - t / duration, 3f);
                path.Add(target * k);
            }

            path.Add(target);
            return path;
        }

        private static float Max(float[] radii)
        {
            var max = 0f;

            foreach (var radius in radii)
            {
                max = Mathf.Max(max, radius);
            }

            return Mathf.Max(max, 0.01f);
        }

        private static int SectorOf(Vector2 point)
        {
            // Угол, отсчитанный от первой оси по часовой стрелке.
            var angle = 90f - Mathf.Atan2(point.y, point.x) * Mathf.Rad2Deg;
            angle = Mathf.Repeat(angle, 360f);
            return Mathf.Min(Axes - 1, Mathf.FloorToInt(angle / 72f));
        }

        private static List<Vector2> Sector(float[] radii, int i) =>
            new List<Vector2> { Vector2.zero, Vertex(radii, i), Vertex(radii, (i + 1) % Axes) };

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static float PolygonArea(IReadOnlyList<Vector2> polygon)
        {
            var sum = 0f;

            for (var i = 0; i < polygon.Count; i++)
            {
                sum += Cross(polygon[i], polygon[(i + 1) % polygon.Count]);
            }

            return Mathf.Abs(sum) * 0.5f;
        }

        /// <summary>Отсечение выпуклого многоугольника выпуклым (Sutherland–Hodgman).</summary>
        private static List<Vector2> Clip(List<Vector2> subject, List<Vector2> clipper)
        {
            // Направление обхода отсекателя: «внутри» — с той же стороны, что его центр масс.
            var output = subject;
            var orientation = Mathf.Sign(Cross(clipper[1] - clipper[0], clipper[2] - clipper[0]));

            if (orientation == 0f)
            {
                return new List<Vector2>();
            }

            for (var e = 0; e < clipper.Count && output.Count > 0; e++)
            {
                var a = clipper[e];
                var b = clipper[(e + 1) % clipper.Count];
                var input = output;
                output = new List<Vector2>(input.Count + 2);
                var s = input[input.Count - 1];

                foreach (var p in input)
                {
                    var pIn = orientation * Cross(b - a, p - a) >= -1e-6f;
                    var sIn = orientation * Cross(b - a, s - a) >= -1e-6f;

                    if (pIn)
                    {
                        if (!sIn)
                        {
                            output.Add(Intersect(s, p, a, b));
                        }

                        output.Add(p);
                    }
                    else if (sIn)
                    {
                        output.Add(Intersect(s, p, a, b));
                    }

                    s = p;
                }
            }

            return output;
        }

        private static Vector2 Intersect(Vector2 p, Vector2 q, Vector2 a, Vector2 b)
        {
            var r = q - p;
            var d = b - a;
            var denominator = Cross(r, d);

            if (Mathf.Abs(denominator) < 1e-9f)
            {
                return q;
            }

            var t = Cross(a - p, d) / denominator;
            return p + r * t;
        }
    }
}
