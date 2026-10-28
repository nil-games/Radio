using UnityEngine;

namespace Radio.World
{
    /// <summary>
    /// Фоновый звук в квартире на отрезке смены: гул труб в стенах с начала ночи 2,
    /// пока провод Серёги на стояке не сняли (N2-05).
    /// </summary>
    /// <remarks>
    /// Состояние выводится из часов и флага каждый кадр, а не хранится: после загрузки
    /// сохранения гул сам звучит или молчит как надо (GDD 02.12).
    /// Нет клипа — заглушка: всё работает, только без звука (список нехватающих ассетов).
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    public sealed class AmbientLoop : MonoBehaviour
    {
        [SerializeField] private AudioClip clip;

        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.5f;

        [Tooltip("С какой отметки звучит: «ЧЧ:ММ:НН».")]
        [SerializeField] private string from = "00:00:02";

        [Tooltip("До какой отметки звучит. Пусто — до конца игры.")]
        [SerializeField] private string until = "06:00:02";

        [Tooltip("Флаг мира: когда он true, звук стихает насовсем.")]
        [SerializeField] private string stopFlag = "ACTION_N2_GROUND_REMOVED";

        [Tooltip("Сколько секунд стихает, когда поставили флаг.")]
        [SerializeField] private float fadeSeconds = 2f;

        private AudioSource _source;
        private int _from;
        private int _until = int.MaxValue;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.clip = clip;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.volume = 0f;

            new GameTimeMark(from).TryGetTotalMinutes(out _from);

            if (!string.IsNullOrWhiteSpace(until) && new GameTimeMark(until).TryGetTotalMinutes(out var end))
            {
                _until = end;
            }
        }

        private void Update()
        {
            var session = GameSession.Current;

            if (session == null || clip == null)
            {
                return;
            }

            var now = session.Time.TotalMinutes;
            var stopped = !string.IsNullOrWhiteSpace(stopFlag)
                          && session.World.TryGet<bool>(stopFlag, out var flag) && flag;
            var audible = now >= _from && now < _until && !stopped;

            var step = volume / Mathf.Max(0.01f, fadeSeconds) * Time.deltaTime;
            _source.volume = Mathf.MoveTowards(_source.volume, audible ? volume : 0f, step);

            if (_source.volume > 0f && !_source.isPlaying)
            {
                _source.Play();
            }
            else if (_source.volume <= 0f && _source.isPlaying)
            {
                _source.Stop();
            }
        }
    }
}
