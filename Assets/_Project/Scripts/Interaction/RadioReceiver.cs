using System;
using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// Звук радиоприёмника. Включённое радио шипит между станциями; ближе к частоте станции
    /// сквозь помехи пробивается её эфир, а точно на частоте помех не слышно совсем.
    /// </summary>
    /// <remarks>
    /// Играет независимо от того, стоит ли игрок у радио: отошёл, не выключив, — оно и дальше
    /// играет из своего угла комнаты. Частоту берёт у <see cref="RadioTuner"/> каждый кадр,
    /// поэтому плавная прокрутка ручки слышна как плавное проявление станции.
    /// </remarks>
    public sealed class RadioReceiver : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private RadioStationList stations;

        [Tooltip("Если пусто, берётся с этого же объекта.")]
        [SerializeField] private RadioTuner tuner;

        [Header("Громкость")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.8f;

        [Tooltip("Громкость помех относительно эфира: шипение на полной громкости режет уши.")]
        [Range(0f, 1f)]
        [SerializeField] private float noiseVolume = 0.5f;

        [Tooltip("Как быстро громкости догоняют настройку, в долях за секунду. " +
                 "Без сглаживания шаг в 0.1 МГц слышен как щелчок.")]
        [SerializeField] private float fadeSpeed = 6f;

        [Header("Звук в комнате")]
        [SerializeField] private float minDistance = 1f;

        [SerializeField] private float maxDistance = 15f;

        [Header("Начало смены")]
        [SerializeField] private bool onAtStart;

        private AudioSource _noise;
        private AudioSource _music;
        private int _station = -1;
        private AudioClip _lastClip;
        private float _signal;

        /// <summary>Радио включено.</summary>
        public bool IsOn { get; private set; }

        /// <summary>Радио включили или выключили.</summary>
        public event Action<bool> PowerChanged;

        private void Awake()
        {
            if (tuner == null)
            {
                tuner = GetComponent<RadioTuner>();
            }

            if (stations == null || tuner == null)
            {
                Debug.LogError($"{nameof(RadioReceiver)}: не заданы станции или ручка настройки. Радио молчит.", this);
                enabled = false;
                return;
            }

            // Два источника, а не один: помехи и эфир звучат одновременно и смешиваются.
            _noise = CreateSource("Noise");
            _noise.clip = stations.Noise;
            _noise.loop = true;

            _music = CreateSource("Station");
            _music.loop = false;
        }

        private void Start()
        {
            if (onAtStart)
            {
                SetPower(true);
            }
        }

        public void Toggle() => SetPower(!IsOn);

        public void SetPower(bool on)
        {
            if (!enabled || IsOn == on)
            {
                return;
            }

            IsOn = on;

            if (on)
            {
                // Включённое радио сразу слышно — без плавного нарастания с нуля.
                _signal = Tune(out _);
                ApplyVolumes();

                if (_noise.clip != null)
                {
                    _noise.Play();
                }
            }
            else
            {
                _noise.Stop();
                _music.Stop();
                _station = -1;
            }

            PowerChanged?.Invoke(on);
        }

        private void Update()
        {
            if (!IsOn)
            {
                return;
            }

            var target = Tune(out var station);

            if (station != _station)
            {
                // Сменилась станция — со старой уходим сразу, новая начинается со случайной песни.
                _station = station;
                _music.Stop();
                _lastClip = null;
            }

            if (_station >= 0 && !_music.isPlaying)
            {
                PlayRandomClip();
            }

            _signal = Mathf.MoveTowards(_signal, target, fadeSpeed * Time.deltaTime);
            ApplyVolumes();
        }

        /// <summary>Сила сигнала на текущей частоте: 0 — одни помехи, 1 — чистая станция.</summary>
        private float Tune(out int station)
        {
            return stations.TryTune(tuner.Frequency, out station, out var signal) ? signal : 0f;
        }

        private void ApplyVolumes()
        {
            _music.volume = volume * _signal;
            _noise.volume = volume * noiseVolume * (1f - _signal);
        }

        private void PlayRandomClip()
        {
            var clips = stations.Stations[_station].clips;

            if (clips == null || clips.Length == 0)
            {
                return;
            }

            // Та же песня два раза подряд звучит как заевшая пластинка — если есть из чего
            // выбрать, берём другую.
            var clip = clips[UnityEngine.Random.Range(0, clips.Length)];

            if (clips.Length > 1 && clip == _lastClip)
            {
                clip = clips[(Array.IndexOf(clips, clip) + 1) % clips.Length];
            }

            _lastClip = clip;
            _music.clip = clip;
            _music.Play();
        }

        private AudioSource CreateSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;

            // Звук идёт от самого радио: в соседней комнате тише, у приёмника громче.
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            return source;
        }
    }
}
