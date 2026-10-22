using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Radio.Broadcast
{
    /// <summary>
    /// Кирпичи интерфейса эфира. Экран строится кодом, а не расставляется в префабе
    /// (так принято в проекте): десятки карточек, собранных мышью, разъедутся при первой
    /// же правке, а здесь раскладка видна целиком.
    /// </summary>
    internal static class BroadcastUi
    {
        public static readonly Color Background = new Color(0.03f, 0.04f, 0.035f, 0.97f);
        public static readonly Color Panel = new Color(0.08f, 0.1f, 0.09f, 1f);
        public static readonly Color Card = new Color(0.14f, 0.16f, 0.15f, 1f);
        public static readonly Color CardHover = new Color(0.2f, 0.23f, 0.21f, 1f);
        public static readonly Color Text = new Color(0.92f, 0.9f, 0.84f, 1f);
        public static readonly Color Muted = new Color(0.62f, 0.62f, 0.58f, 1f);
        public static readonly Color Amber = new Color(0.95f, 0.66f, 0.18f, 1f);
        public static readonly Color Red = new Color(0.9f, 0.25f, 0.22f, 1f);
        public static readonly Color Green = new Color(0.45f, 0.8f, 0.4f, 1f);
        public static readonly Color AdBlue = new Color(0.35f, 0.6f, 0.9f, 1f);

        public static TMP_FontAsset Font;

        private static Sprite _circle;
        private static Sprite _ring;

        /// <summary>Сплошной круг — для точки и мигания.</summary>
        public static Sprite Circle => _circle ??= MakeCircle(128, 0f);

        /// <summary>Кольцо — для круглого таймера (Image, Filled, Radial360).</summary>
        public static Sprite Ring => _ring ??= MakeCircle(128, 0.72f);

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Положение в пикселях холста 1920×1080 от левого верхнего угла родителя.</summary>
        public static RectTransform Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static Image Box(Transform parent, string name, Color color)
        {
            var image = Rect(parent, name).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>
        /// Рамка из четырёх полос поверх краёв. Не компонент Outline: тот рисует копию всей
        /// картинки со сдвигом, и у полупрозрачной карточки она просвечивает цветом рамки.
        /// </summary>
        public static void Border(Graphic graphic, Color color, float width = 2f)
        {
            var frame = graphic.transform.Find("Frame");

            if (frame == null)
            {
                frame = Rect(graphic.transform, "Frame");
                Stretch((RectTransform)frame);

                foreach (var side in new[] { "Top", "Bottom", "Left", "Right" })
                {
                    var edge = Rect(frame, side).gameObject.AddComponent<Image>();
                    edge.raycastTarget = false;
                    var rect = edge.rectTransform;

                    switch (side)
                    {
                        case "Top":
                            rect.anchorMin = new Vector2(0f, 1f);
                            rect.anchorMax = Vector2.one;
                            rect.pivot = new Vector2(0.5f, 1f);
                            break;
                        case "Bottom":
                            rect.anchorMin = Vector2.zero;
                            rect.anchorMax = new Vector2(1f, 0f);
                            rect.pivot = new Vector2(0.5f, 0f);
                            break;
                        case "Left":
                            rect.anchorMin = Vector2.zero;
                            rect.anchorMax = new Vector2(0f, 1f);
                            rect.pivot = new Vector2(0f, 0.5f);
                            break;
                        default:
                            rect.anchorMin = new Vector2(1f, 0f);
                            rect.anchorMax = Vector2.one;
                            rect.pivot = new Vector2(1f, 0.5f);
                            break;
                    }

                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = side == "Top" || side == "Bottom" ? new Vector2(0f, width) : new Vector2(width, 0f);
                }
            }

            foreach (Transform edge in frame)
            {
                edge.GetComponent<Image>().color = color;
            }
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float size,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var label = Rect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();

            if (Font != null)
            {
                label.font = Font;
            }

            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        public static Button Button(Transform parent, string name, string text, Action onClick, float size = 30f)
        {
            var image = Box(parent, name, Card);
            Border(image, Amber, 2f);
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());

            var label = Label(image.transform, "Text", text, size, Text, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return button;
        }

        public static void SetButtonText(Button button, string text) =>
            button.GetComponentInChildren<TextMeshProUGUI>().text = text;

        /// <summary>Круглый таймер: кольцо, которое убывает по часовой стрелке.</summary>
        public static Image RadialTimer(Transform parent, string name)
        {
            var back = Box(parent, name, new Color(1f, 1f, 1f, 0.12f));
            back.sprite = Ring;

            var fill = Box(back.transform, "Fill", Green);
            fill.sprite = Ring;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = false;
            Stretch(fill.rectTransform);
            return fill;
        }

        public static void SetTimer(Image fill, float progress)
        {
            fill.fillAmount = progress;
            fill.color = Color.Lerp(Red, Green, Mathf.InverseLerp(0.15f, 0.6f, progress));
        }

        public static string VibeLine(int[] vibes)
        {
            var parts = new System.Collections.Generic.List<string>();

            for (var i = 0; i < VibeRadar.Axes; i++)
            {
                if (vibes[i] > 0)
                {
                    parts.Add($"{VibeRadar.Names[i]} {vibes[i]}");
                }
            }

            return parts.Count == 0 ? "—" : string.Join(" · ", parts);
        }

        public static string Clock(int minutes) => $"{minutes / 60 % 24:00}:{minutes % 60:00}";

        private static Sprite MakeCircle(int size, float inner)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = inner > 0f ? "BroadcastRing" : "BroadcastCircle",
            };

            var half = size * 0.5f;
            var pixels = new Color32[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
                    var outer = Mathf.Clamp01((1f - d) * half);
                    var innerEdge = inner > 0f ? Mathf.Clamp01((d - inner) * half) : 1f;
                    var a = (byte)(255f * outer * innerEdge);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
