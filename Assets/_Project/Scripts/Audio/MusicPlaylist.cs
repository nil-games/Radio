using System;
using System.Collections.Generic;
using UnityEngine;

namespace Radio.Audio
{
    /// <summary>
    /// Список треков, собранный из папки. Кладёшь mp3 в папку — он сам попадает в список.
    /// </summary>
    /// <remarks>
    /// Папку во время игры прочитать нельзя: в сборку попадают только ассеты, на которые
    /// кто-то ссылается. Поэтому список хранит ссылки на клипы, а в редакторе его
    /// пересобирает <c>MusicPlaylistSync</c> при каждом изменении папки.
    /// </remarks>
    [CreateAssetMenu(menuName = "Радио/Плейлист музыки", fileName = "Playlist")]
    public sealed class MusicPlaylist : ScriptableObject
    {
        [Tooltip("Папка с треками, путь от корня проекта: Assets/_Project/Audio/Efir.")]
        [SerializeField] private string folder = "Assets/_Project/Audio/Efir";

        [Tooltip("Собирается из папки автоматически. Руками не править.")]
        [SerializeField] private AudioClip[] tracks = Array.Empty<AudioClip>();

        public string Folder => folder;

        public IReadOnlyList<AudioClip> Tracks => tracks;

        /// <summary>
        /// Случайный трек, по возможности не тот, что играл только что:
        /// два раза подряд одно и то же звучит как сбой, а не как случайность.
        /// </summary>
        public AudioClip PickRandom(AudioClip previous)
        {
            if (tracks.Length == 0)
            {
                return null;
            }

            if (tracks.Length == 1)
            {
                return tracks[0];
            }

            AudioClip pick;

            do
            {
                pick = tracks[UnityEngine.Random.Range(0, tracks.Length)];
            }
            while (pick == previous || pick == null);

            return pick;
        }

#if UNITY_EDITOR
        /// <summary>Сверить список с папкой. Возвращает true, если список поменялся.</summary>
        public bool SyncFromFolder()
        {
            if (string.IsNullOrWhiteSpace(folder) || !UnityEditor.AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogError($"{nameof(MusicPlaylist)} {name}: папки {folder} нет.", this);
                return false;
            }

            var found = new List<AudioClip>();

            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
            {
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guid));

                if (clip != null)
                {
                    found.Add(clip);
                }
            }

            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            if (found.Count == tracks.Length && found.TrueForAll(clip => Array.IndexOf(tracks, clip) >= 0))
            {
                return false;
            }

            tracks = found.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
            return true;
        }

        [ContextMenu("Собрать из папки")]
        private void SyncFromMenu()
        {
            SyncFromFolder();
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
        }
#endif
    }
}
