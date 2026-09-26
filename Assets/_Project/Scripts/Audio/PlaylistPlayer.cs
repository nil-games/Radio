using UnityEngine;

namespace Radio.Audio
{
    /// <summary>
    /// Играет случайные треки плейлиста, пока объект включён. Трек доиграл —
    /// сразу следующий случайный. Выключили объект — музыка оборвалась.
    /// </summary>
    /// <remarks>
    /// Привязка к включению объекта, а не к командам: поле мини-игры и так
    /// включается на старте и гаснет в конце, и музыка идёт ровно столько же
    /// без единой строчки в самой игре.
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    public sealed class PlaylistPlayer : MonoBehaviour
    {
        [SerializeField] private MusicPlaylist playlist;

        [SerializeField] private AudioSource source;

        private AudioClip _current;
        private bool _playing;

        private void Awake()
        {
            if (source == null)
            {
                source = GetComponent<AudioSource>();
            }

            // Переключением треков управляет этот компонент: петля не дала бы треку
            // закончиться, а автостарт сыграл бы клип, выставленный в инспекторе.
            source.playOnAwake = false;
            source.loop = false;
        }

        private void OnEnable() => PlayNext();

        private void OnDisable()
        {
            _playing = false;

            if (source != null)
            {
                source.Stop();
            }
        }

        private void Update()
        {
            // AudioListener.pause глушит звук, не снимая isPlaying, но на всякий случай
            // проверяем и его: иначе пауза игры листала бы треки.
            if (_playing && !source.isPlaying && !AudioListener.pause)
            {
                PlayNext();
            }
        }

        private void PlayNext()
        {
            _current = playlist != null ? playlist.PickRandom(_current) : null;

            if (_current == null)
            {
                _playing = false;
                Debug.LogWarning($"{nameof(PlaylistPlayer)}: в плейлисте нет треков.", this);
                return;
            }

            source.clip = _current;
            source.Play();
            _playing = true;
        }
    }
}
