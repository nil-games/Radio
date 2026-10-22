using System;
using System.IO;
using UnityEngine;

namespace Radio.World
{
    /// <summary>
    /// Автосохранение на диск (GDD 02.12): одно сохранение, которое грузит «Продолжить»,
    /// плюс снимок начала каждой ночи — задел под выбор ночи в меню.
    /// </summary>
    /// <remarks>
    /// Файлы — JSON в Application.persistentDataPath. Пишутся через временный файл:
    /// если игру закроют посреди записи, старое сохранение останется целым.
    /// </remarks>
    public static class SaveSystem
    {
        private const string MainFile = "save.json";
        private const string NightFileFormat = "night_{0}.json";

        /// <summary>Снимок, который ждёт загрузки сцены («Продолжить» из меню).</summary>
        public static SaveData PendingLoad { get; set; }

        /// <summary>Сохранение записано. Передаёт снимок — для надписи «Сохранено».</summary>
        public static event Action<SaveData> Saved;

        public static bool HasSave => File.Exists(PathOf(MainFile));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            PendingLoad = null;
            Saved = null;
        }

        /// <summary>Записать сохранение. <paramref name="nightStart"/> — ещё и снимок начала ночи.</summary>
        public static void Save(SaveData data, bool nightStart = false)
        {
            try
            {
                var json = JsonUtility.ToJson(data, true);
                Write(MainFile, json);

                if (nightStart)
                {
                    Write(string.Format(NightFileFormat, data.night), json);
                }

                Debug.Log($"{nameof(SaveSystem)}: сохранено, ночь {data.night}, {data.minutes / 60:00}:{data.minutes % 60:00}" +
                          (nightStart ? " (начало ночи)" : string.Empty) + $" → {PathOf(MainFile)}");
                Saved?.Invoke(data);
            }
            catch (Exception exception)
            {
                Debug.LogError($"{nameof(SaveSystem)}: не удалось сохранить: {exception.Message}");
            }
        }

        /// <summary>Прочитать сохранение. Нет файла, файл битый или старой версии — null.</summary>
        public static SaveData Load()
        {
            var path = PathOf(MainFile);

            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));

                if (data == null || data.version != SaveData.CurrentVersion || string.IsNullOrEmpty(data.scene))
                {
                    Debug.LogWarning($"{nameof(SaveSystem)}: сохранение не подходит (версия " +
                                     $"{data?.version}, нужна {SaveData.CurrentVersion}) — считаем, что его нет.");
                    return null;
                }

                return data;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"{nameof(SaveSystem)}: сохранение повреждено ({exception.Message}) — считаем, что его нет.");
                return null;
            }
        }

        private static void Write(string file, string json)
        {
            var path = PathOf(file);
            var temp = path + ".tmp";
            File.WriteAllText(temp, json);

            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
        }

        private static string PathOf(string file) => Path.Combine(Application.persistentDataPath, file);
    }
}
