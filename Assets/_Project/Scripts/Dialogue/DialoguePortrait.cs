using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Yarn.Unity;

namespace Radio.Dialogue
{
    /// <summary>
    /// Собеседник на экране: анимированный персонаж по центру кадра, пока идёт разговор.
    /// Пока он виден, панель диалога переезжает в правую половину экрана, чтобы не закрывать его.
    /// </summary>
    /// <remarks>
    /// Персонаж — видео с альфа-каналом (WebM VP8, импорт с Keep Alpha). Зелёный фон вырезается
    /// заранее, при подготовке файла, а не шейдером в игре: так край чище и ничего не нужно
    /// подбирать под освещение каждой сцены.
    /// Команды Yarn: &lt;&lt;character id&gt;&gt; — показать, &lt;&lt;character_hide&gt;&gt; — убрать.
    /// В конце разговора персонаж убирается сам.
    /// </remarks>
    public sealed class DialoguePortrait : MonoBehaviour
    {
        [Serializable]
        public struct Character
        {
            [Tooltip("Имя для команды character в диалоге. Латиницей и без пробелов.")]
            public string id;

            [Tooltip("Видео персонажа с прозрачным фоном.")]
            public VideoClip clip;
        }

        [Header("Ссылки")]
        [SerializeField] private DialogueRunner runner;

        [SerializeField] private DialogueController dialogue;

        [Tooltip("Canvas диалога. Персонаж ставится в него первым, то есть под панелями.")]
        [SerializeField] private RectTransform canvasRoot;

        [Tooltip("Панели, которые уезжают вправо, пока персонаж на экране.")]
        [SerializeField] private RectTransform[] panels;

        [Header("Персонажи")]
        [SerializeField] private Character[] characters;

        [Header("Раскладка")]
        [Tooltip("Какую часть высоты экрана занимает персонаж: низ и верх, от 0 до 1.")]
        [SerializeField] private Vector2 verticalSpan = new Vector2(0.04f, 0.92f);

        [Tooltip("Куда по горизонтали встают панели, пока персонаж на экране, в долях ширины экрана.")]
        [SerializeField] private float panelAnchorX = 0.8f;

        [Tooltip("Ширина панелей, пока персонаж на экране, пикселей. Обычная ширина " +
                 "залезала бы на персонажа: он стоит по центру и занимает почти половину кадра.")]
        [SerializeField] private float panelWidthAside = 560f;

        private struct PanelLayout
        {
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 Position;
            public Vector2 Size;
        }

        private PanelLayout[] _original;
        private RawImage _image;
        private AspectRatioFitter _fitter;
        private VideoPlayer _player;
        private RenderTexture _texture;
        private bool _shown;

        private void Awake()
        {
            if (runner == null)
            {
                runner = GetComponent<DialogueRunner>();
            }

            if (dialogue == null)
            {
                dialogue = GetComponent<DialogueController>();
            }

            if (runner == null || canvasRoot == null)
            {
                Debug.LogError($"{nameof(DialoguePortrait)}: не заданы Dialogue Runner или Canvas. " +
                               "Персонажи в диалогах не покажутся.", this);
                enabled = false;
                return;
            }

            RememberPanels();
            BuildImage();

            runner.AddCommandHandler("character", (Action<string>)Show);
            runner.AddCommandHandler("character_hide", (Action)Hide);

            if (dialogue != null)
            {
                dialogue.DialogueFinished += Hide;
            }
        }

        private void OnDestroy()
        {
            if (dialogue != null)
            {
                dialogue.DialogueFinished -= Hide;
            }

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }

        /// <summary>Команда из диалога: character babka.</summary>
        public void Show(string id)
        {
            if (!TryFind(id, out var clip))
            {
                Debug.LogError($"{nameof(DialoguePortrait)}: персонажа {id} нет в таблице.", this);
                return;
            }

            PrepareTexture(clip);

            _fitter.aspectRatio = clip.height > 0 ? (float)clip.width / clip.height : 0.75f;

            // Картинку показываем только когда готов первый кадр: иначе на мгновение
            // мелькнёт пустой прямоугольник или прошлый собеседник.
            _image.enabled = false;
            _player.clip = clip;
            _player.Prepare();

            SetPanelsAside(true);
            _shown = true;
        }

        /// <summary>Команда из диалога: character_hide. Зовётся и в конце разговора.</summary>
        public void Hide()
        {
            if (!_shown)
            {
                return;
            }

            _shown = false;
            _player.Stop();
            _image.enabled = false;
            SetPanelsAside(false);
        }

        private void HandlePrepared(VideoPlayer player)
        {
            if (!_shown)
            {
                return;
            }

            player.Play();
            _image.enabled = true;
        }

        private bool TryFind(string id, out VideoClip clip)
        {
            foreach (var character in characters ?? Array.Empty<Character>())
            {
                if (string.Equals(character.id, id, StringComparison.OrdinalIgnoreCase) && character.clip != null)
                {
                    clip = character.clip;
                    return true;
                }
            }

            clip = null;
            return false;
        }

        private void PrepareTexture(VideoClip clip)
        {
            var width = (int)Mathf.Max(1, clip.width);
            var height = (int)Mathf.Max(1, clip.height);

            if (_texture != null && _texture.width == width && _texture.height == height)
            {
                return;
            }

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }

            // ARGB32, а не формат по умолчанию: без альфа-канала в текстуре прозрачный
            // фон видео превратился бы в чёрный прямоугольник.
            _texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = "DialoguePortrait" };
            _texture.Create();

            _player.targetTexture = _texture;
            _image.texture = _texture;
        }

        private void BuildImage()
        {
            var go = new GameObject("Portrait", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvasRoot, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = new Vector2(0.5f, verticalSpan.x);
            rect.anchorMax = new Vector2(0.5f, verticalSpan.y);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            _image = go.AddComponent<RawImage>();
            _image.raycastTarget = false;
            _image.enabled = false;

            // Ширину считает сам Unity по высоте и пропорциям клипа: персонаж не растягивается
            // ни на широком мониторе, ни в окне редактора.
            _fitter = go.AddComponent<AspectRatioFitter>();
            _fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;

            _player = go.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = true;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.audioOutputMode = VideoAudioOutputMode.None;
            _player.waitForFirstFrame = true;
            _player.prepareCompleted += HandlePrepared;
        }

        private void RememberPanels()
        {
            panels ??= Array.Empty<RectTransform>();
            _original = new PanelLayout[panels.Length];

            for (var i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null)
                {
                    continue;
                }

                _original[i] = new PanelLayout
                {
                    AnchorMin = panels[i].anchorMin,
                    AnchorMax = panels[i].anchorMax,
                    Position = panels[i].anchoredPosition,
                    Size = panels[i].sizeDelta,
                };
            }
        }

        private void SetPanelsAside(bool aside)
        {
            for (var i = 0; i < panels.Length; i++)
            {
                var panel = panels[i];

                if (panel == null)
                {
                    continue;
                }

                var original = _original[i];

                if (!aside)
                {
                    panel.anchorMin = original.AnchorMin;
                    panel.anchorMax = original.AnchorMax;
                    panel.anchoredPosition = original.Position;
                    panel.sizeDelta = original.Size;
                    continue;
                }

                // Двигаем только горизонталь: высоту и отступ снизу панели выбирают себе сами.
                panel.anchorMin = new Vector2(panelAnchorX, original.AnchorMin.y);
                panel.anchorMax = new Vector2(panelAnchorX, original.AnchorMax.y);
                panel.anchoredPosition = new Vector2(0f, original.Position.y);
                panel.sizeDelta = new Vector2(Mathf.Min(original.Size.x, panelWidthAside), original.Size.y);
            }
        }
    }
}
