using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Radio.Broadcast
{
    /// <summary>
    /// Пятиугольник вайбов в окне результата: сетка, фигуры заявки и кассет, точка.
    /// </summary>
    /// <remarks>
    /// Рисуется своим мешем, без спрайтов: фигуры меняются каждый кадр анимации, а
    /// звёздную фигуру проще всего собрать веером треугольников из центра.
    /// Координаты фигур — в единицах вайба; <see cref="Reach"/> задаёт, какой вайб
    /// приходится на край окна.
    /// </remarks>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RadarGraphic : MaskableGraphic
    {
        public sealed class Shape
        {
            public float[] Radii;
            public Color Fill;
            public Color Outline;
            public float Scale = 1f;
        }

        public Color GridColor = new Color(1f, 1f, 1f, 0.12f);
        public Color DotColor = Color.white;

        /// <summary>Обводка точки: на ней точка видна на любом фоне, даже чёрная.</summary>
        public Color DotOutline = new Color(1f, 1f, 1f, 0.9f);

        /// <summary>
        /// Сколько единиц вайба помещается от центра до края. Меняется во время проверки:
        /// поле приближается к фигурам, а деления шкалы остаются в своих значениях.
        /// </summary>
        public float Reach = 4f;

        /// <summary>Шаг колец сетки и её предел: кольца 2, 4, 6, 8, 10.</summary>
        public float GridStep = 2f;

        public float GridMax = 10f;

        public readonly List<Shape> Shapes = new List<Shape>();

        public bool ShowDot;
        public Vector2 Dot;
        public float DotRadius = 9f;

        public void Refresh() => SetVerticesDirty();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var rect = rectTransform.rect;
            var center = rect.center;
            var pixels = Mathf.Min(rect.width, rect.height) * 0.5f / Mathf.Max(0.01f, Reach);

            // Сетка: кольца в своих значениях шкалы (2, 4, 6…) — при приближении часть колец
            // уходит за край, а оставшиеся раздвигаются; край поля рисуется отдельно.
            var edge = Mathf.Min(Reach, GridMax);

            for (var r = GridStep; r <= edge + 1e-3f; r += GridStep)
            {
                Ring(vh, center, r * pixels, GridColor);
            }

            if (Mathf.Abs(Mathf.Repeat(edge, GridStep)) > 1e-3f)
            {
                Ring(vh, center, edge * pixels, GridColor);
            }

            for (var i = 0; i < VibeRadar.Axes; i++)
            {
                Line(vh, center, center + VibeRadar.Axis(i) * Reach * pixels, 1.5f, GridColor);
            }

            foreach (var shape in Shapes)
            {
                if (shape?.Radii == null || shape.Scale <= 0f)
                {
                    continue;
                }

                var points = new Vector2[VibeRadar.Axes];

                for (var i = 0; i < VibeRadar.Axes; i++)
                {
                    // Сумма нескольких кассет может вылезти за шкалу — прижимаем к краю.
                    var radius = Mathf.Min(shape.Radii[i], Reach * 1.05f);
                    points[i] = center + VibeRadar.Axis(i) * radius * pixels * shape.Scale;
                }

                if (shape.Fill.a > 0f)
                {
                    for (var i = 0; i < VibeRadar.Axes; i++)
                    {
                        Triangle(vh, center, points[i], points[(i + 1) % VibeRadar.Axes], shape.Fill);
                    }
                }

                if (shape.Outline.a > 0f)
                {
                    for (var i = 0; i < VibeRadar.Axes; i++)
                    {
                        Line(vh, points[i], points[(i + 1) % VibeRadar.Axes], 3f, shape.Outline);
                    }
                }
            }

            if (ShowDot)
            {
                var c = center + Dot * pixels;
                Disc(vh, c, DotRadius + 2.5f, DotOutline);
                Disc(vh, c, DotRadius, DotColor);
            }
        }

        private static void Ring(VertexHelper vh, Vector2 center, float radius, Color color)
        {
            for (var i = 0; i < VibeRadar.Axes; i++)
            {
                var a = center + VibeRadar.Axis(i) * radius;
                var b = center + VibeRadar.Axis((i + 1) % VibeRadar.Axes) * radius;
                Line(vh, a, b, 1.5f, color);
            }
        }

        private static void Disc(VertexHelper vh, Vector2 c, float radius, Color color)
        {
            const int segments = 24;

            for (var i = 0; i < segments; i++)
            {
                var a0 = i * Mathf.PI * 2f / segments;
                var a1 = (i + 1) * Mathf.PI * 2f / segments;
                Triangle(vh, c,
                    c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius,
                    c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius,
                    color);
            }
        }

        private static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            var start = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            var direction = b - a;

            if (direction.sqrMagnitude < 1e-6f)
            {
                return;
            }

            var normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
            var start = vh.currentVertCount;
            vh.AddVert(a - normal, color, Vector2.zero);
            vh.AddVert(a + normal, color, Vector2.zero);
            vh.AddVert(b + normal, color, Vector2.zero);
            vh.AddVert(b - normal, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
