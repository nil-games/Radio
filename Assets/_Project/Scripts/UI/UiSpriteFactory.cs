using UnityEngine;

namespace Radio.UI
{
    /// <summary>
    /// Рисует простые фигуры интерфейса в текстуру. Спрайты генерируются кодом,
    /// чтобы прицел не зависел от арта и не тащил файлы в репозиторий.
    /// </summary>
    public static class UiSpriteFactory
    {
        /// <summary>
        /// Кольцо с мягким краем. Радиусы задаются долей от половины стороны:
        /// innerRadius = 0 даёт залитый круг, 0.6 — тонкое кольцо.
        /// </summary>
        public static Sprite CreateRing(int size, float innerRadius, float outerRadius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"Ring_{size}_{innerRadius:0.00}_{outerRadius:0.00}"
            };

            var pixels = new Color32[size * size];
            var half = size * 0.5f;

            // Ширина размытия края в пикселях. Без неё круг получается ступенчатым:
            // прицел мелкий, и лесенка на нём особенно заметна.
            var feather = 1.5f / half;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // Сдвиг на полпикселя — иначе центр фигуры окажется между пикселями.
                    var dx = (x + 0.5f - half) / half;
                    var dy = (y + 0.5f - half) / half;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);

                    var outer = Mathf.Clamp01((outerRadius - distance) / feather);
                    var inner = innerRadius <= 0f ? 1f : Mathf.Clamp01((distance - innerRadius) / feather);
                    var alpha = Mathf.Clamp01(outer * inner);

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
        }
    }
}
