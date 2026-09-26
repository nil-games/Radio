using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// Эфир радиоприёмника: какие станции на каких частотах и что на них играет.
    /// </summary>
    /// <remarks>
    /// Песни лежат по папкам, по одной на станцию, а папка названа «Имя — частота»:
    /// «Европа Плюс — 106,2». Во время игры Unity содержимое папок не видит, поэтому
    /// список собирается в редакторе кнопкой «Собрать станции из папок» в меню ассета.
    /// Новая песня — положить файл в папку станции и нажать кнопку ещё раз.
    /// </remarks>
    [CreateAssetMenu(fileName = "RadioStations", menuName = "Радио/Станции приёмника")]
    public sealed class RadioStationList : ScriptableObject
    {
        [Serializable]
        public struct Station
        {
            public string name;

            [Tooltip("Частота, МГц.")]
            public float frequency;

            [Tooltip("Песни станции. Каждый раз играет случайная. Пусто — на частоте только помехи.")]
            public AudioClip[] clips;
        }

        [Header("Помехи")]
        [Tooltip("Шипение между станциями. Зацикливается.")]
        [SerializeField] private AudioClip noise;

        [Header("Приём")]
        [Tooltip("На каком расстоянии от частоты станция начинает пробиваться сквозь помехи, МГц.")]
        [SerializeField] private float captureWidth = 0.6f;

        [Tooltip("Внутри этого расстояния помех нет совсем, МГц. Меньше шага настройки — " +
                 "значит, чисто только точно на частоте.")]
        [SerializeField] private float clearWidth = 0.05f;

        [Header("Станции")]
        [Tooltip("Папка, в которой лежат папки станций.")]
        [SerializeField] private string folder = "Assets/_Project/Audio/Music/RadioMusic";

        [SerializeField] private Station[] stations;

        public AudioClip Noise => noise;

        public IReadOnlyList<Station> Stations => stations ?? Array.Empty<Station>();

        /// <summary>
        /// Ближайшая слышимая станция и сила её сигнала: 0 — одни помехи, 1 — чистый приём.
        /// Станция без песен не слышна — на её частоте остаются помехи.
        /// </summary>
        public bool TryTune(float frequency, out int station, out float signal)
        {
            station = -1;
            signal = 0f;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < Stations.Count; i++)
            {
                var candidate = stations[i];

                if (candidate.clips == null || candidate.clips.Length == 0)
                {
                    continue;
                }

                var distance = Mathf.Abs(frequency - candidate.frequency);

                if (distance >= captureWidth || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                station = i;
            }

            if (station < 0)
            {
                return false;
            }

            var range = Mathf.Max(0.001f, captureWidth - clearWidth);
            signal = 1f - Mathf.Clamp01((bestDistance - clearWidth) / range);
            return true;
        }

#if UNITY_EDITOR
        [ContextMenu("Собрать станции из папок")]
        private void CollectFromFolders()
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogError($"{name}: папки {folder} нет.", this);
                return;
            }

            var result = new List<Station>();

            foreach (var sub in UnityEditor.AssetDatabase.GetSubFolders(folder))
            {
                var title = System.IO.Path.GetFileName(sub);

                if (!TryParseTitle(title, out var stationName, out var frequency))
                {
                    Debug.LogWarning($"{name}: папка «{title}» не похожа на «Имя — частота», пропущена.", this);
                    continue;
                }

                var clips = new List<AudioClip>();

                foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { sub }))
                {
                    var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guid));

                    if (clip != null)
                    {
                        clips.Add(clip);
                    }
                }

                result.Add(new Station { name = stationName, frequency = frequency, clips = clips.ToArray() });
            }

            result.Sort((a, b) => a.frequency.CompareTo(b.frequency));
            stations = result.ToArray();

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);

            foreach (var station in stations)
            {
                Debug.Log($"{name}: {station.name} — {station.frequency:0.0} МГц, песен: {station.clips.Length}.", this);
            }
        }

        /// <summary>«Европа Плюс — 106,2» → имя и частота. Дробь — хоть запятой, хоть точкой.</summary>
        private static bool TryParseTitle(string title, out string stationName, out float frequency)
        {
            stationName = title;
            frequency = 0f;

            var dash = title.LastIndexOfAny(new[] { '—', '–', '-' });

            if (dash <= 0)
            {
                return false;
            }

            stationName = title.Substring(0, dash).Trim();
            var number = title.Substring(dash + 1).Trim().Replace(',', '.');

            return float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out frequency);
        }
#endif

        private void OnValidate()
        {
            clearWidth = Mathf.Max(0f, clearWidth);
            captureWidth = Mathf.Max(clearWidth + 0.01f, captureWidth);
        }
    }
}
