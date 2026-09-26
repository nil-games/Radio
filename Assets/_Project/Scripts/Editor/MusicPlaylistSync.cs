using System;
using Radio.Audio;
using UnityEditor;

namespace Radio.EditorTools
{
    /// <summary>
    /// Держит плейлисты в согласии с их папками: трек добавили, удалили или перенесли —
    /// список пересобирается сам.
    /// </summary>
    public sealed class MusicPlaylistSync : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(MusicPlaylist)))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var playlist = AssetDatabase.LoadAssetAtPath<MusicPlaylist>(path);

                if (playlist == null)
                {
                    continue;
                }

                // Сам плейлист тоже в счёт: сменили в нём папку — пересобрать сразу.
                var touched = Array.IndexOf(imported, path) >= 0
                              || Touches(imported, playlist.Folder)
                              || Touches(deleted, playlist.Folder)
                              || Touches(moved, playlist.Folder)
                              || Touches(movedFrom, playlist.Folder);

                // Сохраняем только при настоящей перемене: сохранение само вызовет
                // этот обработчик снова, и без проверки он крутился бы бесконечно.
                if (touched && playlist.SyncFromFolder())
                {
                    AssetDatabase.SaveAssetIfDirty(playlist);
                }
            }
        }

        private static bool Touches(string[] paths, string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return false;
            }

            var prefix = folder.TrimEnd('/') + "/";

            foreach (var path in paths)
            {
                if (path.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
