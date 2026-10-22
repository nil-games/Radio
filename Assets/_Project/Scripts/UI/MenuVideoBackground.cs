using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Radio.UI
{
    /// <summary>
    /// Зацикленное видео на фоне главного меню. Растягивается на весь экран с сохранением
    /// пропорций: лишнее по краям обрезается, а не превращается в чёрные полосы.
    /// </summary>
    /// <remarks>
    /// Текстура для кадров создаётся здесь по размеру ролика, а не лежит ассетом:
    /// заменили видео на другое разрешение — ничего перенастраивать не нужно.
    /// Пока ролик готовится, картинка прозрачна и виден обычный фон меню:
    /// иначе первые доли секунды экран был бы чёрным или с мусором в текстуре.
    /// </remarks>
    [RequireComponent(typeof(RawImage))]
    [RequireComponent(typeof(VideoPlayer))]
    [RequireComponent(typeof(AspectRatioFitter))]
    public sealed class MenuVideoBackground : MonoBehaviour
    {
        [SerializeField] private VideoClip clip;

        [Tooltip("Играть звук ролика. По умолчанию выключено: короткая петля со звуком " +
                 "заметно повторяется, а у меню свои звуки.")]
        [SerializeField] private bool playAudio;

        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.5f;

        private RawImage _image;
        private VideoPlayer _player;
        private RenderTexture _texture;

        private void Awake()
        {
            _image = GetComponent<RawImage>();
            _player = GetComponent<VideoPlayer>();

            var fitter = GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            // Прозрачна до первого кадра — под ней виден обычный фон.
            _image.raycastTarget = false;
            _image.color = new Color(1f, 1f, 1f, 0f);

            if (clip == null)
            {
                Debug.LogWarning($"{nameof(MenuVideoBackground)}: не задан ролик, фон останется обычным.", this);
                enabled = false;
                return;
            }

            fitter.aspectRatio = (float)clip.width / clip.height;

            _texture = new RenderTexture((int)clip.width, (int)clip.height, 0) { name = "MenuVideo" };
            _image.texture = _texture;

            _player.playOnAwake = false;
            _player.isLooping = true;
            _player.source = VideoSource.VideoClip;
            _player.clip = clip;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _texture;
            _player.audioOutputMode = playAudio ? VideoAudioOutputMode.Direct : VideoAudioOutputMode.None;

            if (playAudio)
            {
                _player.SetDirectAudioVolume(0, volume);
            }

            _player.prepareCompleted += OnPrepared;
            _player.Prepare();
        }

        private void OnPrepared(VideoPlayer player)
        {
            _image.color = Color.white;
            player.Play();
        }

        private void OnDestroy()
        {
            if (_player != null)
            {
                _player.prepareCompleted -= OnPrepared;
            }

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }
    }
}
