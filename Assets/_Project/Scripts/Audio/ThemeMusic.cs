using System.Collections.Generic;
using UnityEngine;

namespace Radio.Audio
{
    /// <summary>
    /// Фоновая музыка квартиры: тихо играет случайные треки темы и уступает место любой
    /// другой музыке — радио, кассете, эфиру. Та замолчала — тема плавно возвращается
    /// с того же места.
    /// </summary>
    /// <remarks>
    /// «Другая музыка» — это клипы из плейлистов-глушителей (папки Music и Efir). Какая
    /// музыка играет, узнаём, просматривая звучащие источники, а не подписываясь на каждый
    /// проигрыватель: радио, кассетник, эфир и караоке сделаны разными компонентами,
    /// и новый проигрыватель музыки иначе пришлось бы не забыть подключить сюда.
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ThemeMusic : MonoBehaviour
    {
        [SerializeField] private MusicPlaylist theme;

        [Tooltip("Плейлисты, чья музыка глушит тему. Треки самой темы в них не считаются.")]
        [SerializeField] private MusicPlaylist[] blockers = System.Array.Empty<MusicPlaylist>();

        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.1f;

        [Tooltip("Сколько длится затихание и возвращение темы, с.")]
        [SerializeField] private float fadeTime = 1f;

        [Tooltip("Как часто проверять, не играет ли другая музыка, с.")]
        [SerializeField] private float checkInterval = 0.25f;

        private readonly HashSet<AudioClip> _blocking = new HashSet<AudioClip>();
        private AudioSource _source;
        private AudioClip _current;
        private float _nextCheck;
        private bool _blocked;
        private bool _paused;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.volume = 0f;

            foreach (var playlist in blockers)
            {
                if (playlist == null)
                {
                    continue;
                }

                foreach (var clip in playlist.Tracks)
                {
                    if (clip != null)
                    {
                        _blocking.Add(clip);
                    }
                }
            }

            // Тема лежит внутри Music: свои треки глушить её не должны.
            if (theme != null)
            {
                foreach (var clip in theme.Tracks)
                {
                    _blocking.Remove(clip);
                }
            }
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextCheck)
            {
                _nextCheck = Time.unscaledTime + checkInterval;
                _blocked = OtherMusicPlaying();
            }

            var step = volume / Mathf.Max(0.01f, fadeTime) * Time.unscaledDeltaTime;

            if (_blocked)
            {
                _source.volume = Mathf.MoveTowards(_source.volume, 0f, step);

                if (_source.volume <= 0f && _source.isPlaying)
                {
                    _source.Pause();
                    _paused = true;
                }

                return;
            }

            if (_paused)
            {
                _source.UnPause();
                _paused = false;
            }
            else if (!_source.isPlaying)
            {
                PlayNext();
            }

            _source.volume = Mathf.MoveTowards(_source.volume, volume, step);
        }

        private bool OtherMusicPlaying()
        {
            foreach (var other in FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude))
            {
                if (other != _source && other.isPlaying && other.clip != null && _blocking.Contains(other.clip))
                {
                    return true;
                }
            }

            return false;
        }

        private void PlayNext()
        {
            _current = theme != null ? theme.PickRandom(_current) : null;

            if (_current == null)
            {
                enabled = false;
                return;
            }

            _source.clip = _current;
            _source.volume = 0f;
            _source.Play();
        }
    }
}
