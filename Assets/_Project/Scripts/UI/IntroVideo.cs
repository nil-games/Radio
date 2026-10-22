using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Radio.UI
{
    /// <summary>
    /// Вступительный ролик между меню и игрой: во весь экран поверх меню, со звуком.
    /// Пока он идёт, игровая сцена грузится в фоне и включается, как только ролик кончился.
    /// </summary>
    /// <remarks>
    /// Экран ролика строится кодом, а не лежит в префабе: он нужен один раз за запуск,
    /// и держать в меню выключенный холст с плеером ради этого незачем.
    /// Загрузка идёт параллельно с роликом: иначе после минуты видео игрок ещё
    /// смотрел бы на чёрный экран, пока грузится квартира.
    /// </remarks>
    public sealed class IntroVideo : MonoBehaviour
    {
        [SerializeField] private VideoClip clip;

        [Tooltip("Что спрятать на время ролика: холст меню, музыку меню.")]
        [SerializeField] private GameObject[] hideDuring = Array.Empty<GameObject>();

        [Tooltip("Шрифт подсказки о пропуске. Обязан содержать кириллицу.")]
        [SerializeField] private TMP_FontAsset font;

        [Header("Пропуск")]
        [Tooltip("Можно ли пропустить ролик пробелом, Enter или Esc.")]
        [SerializeField] private bool skippable = true;

        [SerializeField] private string skipHint = "Пробел — пропустить";

        [Tooltip("Сколько секунд после начала пропуск ещё не работает: чтобы нажатие, " +
                 "которым игрок жал «Играть», не проглотило ролик.")]
        [SerializeField] private float skipDelay = 0.5f;

        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        private VideoPlayer _player;
        private RenderTexture _texture;
        private AsyncOperation _loading;
        private float _startedAt;
        private bool _running;
        private bool _finishing;

        /// <summary>Показать ролик и по его окончании включить сцену <paramref name="sceneName"/>.</summary>
        public void Play(string sceneName)
        {
            if (_running)
            {
                return;
            }

            _loading = SceneManager.LoadSceneAsync(sceneName);

            if (_loading == null)
            {
                Debug.LogError($"{nameof(IntroVideo)}: сцены {sceneName} нет в сборке.", this);
                return;
            }

            _loading.allowSceneActivation = false;

            if (clip == null)
            {
                // Без ролика просто переходим: игра не должна застревать в меню.
                Debug.LogWarning($"{nameof(IntroVideo)}: не задан ролик, сразу в игру.", this);
                _loading.allowSceneActivation = true;
                return;
            }

            foreach (var go in hideDuring)
            {
                if (go != null)
                {
                    go.SetActive(false);
                }
            }

            BuildScreen();
            _running = true;
            _startedAt = Time.unscaledTime;
        }

        private void BuildScreen()
        {
            var canvasGo = new GameObject("IntroScreen", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);

            // Поверх всего меню.
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var black = CreateRect("Black", canvasGo.transform);
            Stretch(black);
            black.gameObject.AddComponent<Image>().color = Color.black;

            // Ролик вписывается целиком: вступление — не фон, обрезать его края нельзя.
            var videoRect = CreateRect("Video", canvasGo.transform);
            Stretch(videoRect);
            var fitter = videoRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)clip.width / clip.height;

            var image = videoRect.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0f);

            _texture = new RenderTexture((int)clip.width, (int)clip.height, 0) { name = "IntroVideo" };
            image.texture = _texture;

            _player = videoRect.gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = false;
            _player.source = VideoSource.VideoClip;
            _player.clip = clip;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _texture;
            _player.audioOutputMode = VideoAudioOutputMode.Direct;
            _player.SetDirectAudioVolume(0, volume);
            _player.prepareCompleted += p =>
            {
                image.color = Color.white;
                p.Play();
            };
            _player.loopPointReached += _ => Finish();
            _player.Prepare();

            if (skippable && font != null && !string.IsNullOrEmpty(skipHint))
            {
                var hint = CreateRect("SkipHint", canvasGo.transform);
                hint.anchorMin = hint.anchorMax = new Vector2(1f, 0f);
                hint.pivot = new Vector2(1f, 0f);
                hint.anchoredPosition = new Vector2(-40f, 30f);
                hint.sizeDelta = new Vector2(600f, 40f);

                var label = hint.gameObject.AddComponent<TextMeshProUGUI>();
                label.font = font;
                label.text = skipHint;
                label.fontSize = 22f;
                label.color = new Color(1f, 1f, 1f, 0.35f);
                label.alignment = TextAlignmentOptions.BottomRight;
                label.raycastTarget = false;
            }
        }

        private void Update()
        {
            if (!_running || _finishing || !skippable || Time.unscaledTime - _startedAt < skipDelay)
            {
                return;
            }

            var keyboard = Keyboard.current;

            if (keyboard != null
                && (keyboard.spaceKey.wasPressedThisFrame
                    || keyboard.enterKey.wasPressedThisFrame
                    || keyboard.escapeKey.wasPressedThisFrame))
            {
                Finish();
            }
        }

        private void Finish()
        {
            if (_finishing)
            {
                return;
            }

            _finishing = true;

            if (_player != null)
            {
                _player.Stop();
            }

            // Сцена включится, как только догрузится; если уже готова — сразу.
            _loading.allowSceneActivation = true;
        }

        private void OnDestroy()
        {
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
