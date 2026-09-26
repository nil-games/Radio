using UnityEngine;

namespace Radio.World
{
    /// <summary>
    /// Только для отладки: при переводе часов вперёд отмечает пройденными все сюжетные шаги
    /// более раннего времени, ставя их флаги завершения.
    /// </summary>
    /// <remarks>
    /// Шаги на самой отметке не трогаются: переведя часы на 00:30, игрок хочет застать
    /// визит Зины, а не обнаружить его уже случившимся.
    /// </remarks>
    [RequireComponent(typeof(GameSession))]
    public sealed class StoryBeatSkipper : MonoBehaviour
    {
        [SerializeField] private StoryBeats beats;

        private GameSession _session;

        // Start, а не Awake: часы и мир заводятся в Awake сессии.
        private void Start()
        {
            _session = GetComponent<GameSession>();

            if (beats == null || _session.Time == null)
            {
                Debug.LogError($"{nameof(StoryBeatSkipper)}: не заданы сюжетные шаги или нет часов.", this);
                enabled = false;
                return;
            }

            _session.Time.DebugJumped += OnDebugJumped;
        }

        private void OnDestroy()
        {
            if (_session != null && _session.Time != null)
            {
                _session.Time.DebugJumped -= OnDebugJumped;
            }
        }

        private void OnDebugJumped(int minutes)
        {
            foreach (var beat in beats.Beats)
            {
                if (beat.night != _session.Time.Night
                    || !new GameTimeMark(beat.at).TryGetMinutes(out var mark)
                    || mark >= minutes)
                {
                    continue;
                }

                foreach (var flag in beat.doneFlags ?? System.Array.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(flag))
                    {
                        _session.World.Set(flag, true);
                    }
                }

                Debug.Log($"{nameof(StoryBeatSkipper)}: шаг «{beat.id}» ({beat.at}) отмечен пройденным.", this);
            }
        }
    }
}
