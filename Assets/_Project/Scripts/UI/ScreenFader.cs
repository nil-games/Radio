using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Radio.UI
{
    /// <summary>
    /// Чёрный экран поверх всего: затемнение при переходах между сценами, загрузке
    /// сохранения и выходе из сна.
    /// </summary>
    /// <remarks>
    /// Холст строится кодом, как и остальной интерфейс проекта. Пока экран прозрачный,
    /// холст выключен, чтобы не мешать ни отрисовке, ни нажатиям.
    /// </remarks>
    public sealed class ScreenFader : MonoBehaviour
    {
        private CanvasGroup _group;

        public float Alpha => _group.alpha;

        /// <summary>Завести затемнение дочерним объектом <paramref name="parent"/>.</summary>
        public static ScreenFader Create(Transform parent, int sortingOrder, float startAlpha)
        {
            var canvasObject = new GameObject("ScreenFader", typeof(RectTransform));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var black = new GameObject("Black", typeof(RectTransform));
            var rect = (RectTransform)black.transform;
            rect.SetParent(canvasObject.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = black.AddComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;

            var fader = canvasObject.AddComponent<ScreenFader>();
            fader._group = canvasObject.AddComponent<CanvasGroup>();
            fader._group.blocksRaycasts = false;
            fader.SetAlpha(startAlpha);
            return fader;
        }

        public void SetAlpha(float alpha)
        {
            _group.alpha = alpha;
            gameObject.SetActive(alpha > 0f);
        }

        /// <summary>Плавно довести затемнение до <paramref name="to"/>.</summary>
        public IEnumerator FadeTo(float to, float seconds)
        {
            gameObject.SetActive(true);
            var from = _group.alpha;

            for (var t = 0f; t < seconds; t += Time.deltaTime)
            {
                _group.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }

            SetAlpha(to);
        }
    }
}
