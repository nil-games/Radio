using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Radio.Minigames
{
    /// <summary>
    /// Песня для мини-игры «Гитара»: звук, тайм-коды слов и окно попадания.
    /// </summary>
    /// <remarks>
    /// Тайм-коды лежат отдельным JSON, а не вписаны в ассет: их делает внешний
    /// скрипт выравнивания, и переделанную разметку достаточно положить поверх файла.
    /// Текст песни живёт только там же — в коде и ассете его нет.
    /// </remarks>
    [CreateAssetMenu(fileName = "Karaoke", menuName = "Радио/Мини-игра «Гитара»")]
    public sealed class KaraokeSong : ScriptableObject
    {
        /// <summary>Одно слово, которое нужно отбить.</summary>
        public readonly struct Note
        {
            public readonly float Time;
            public readonly Key Key;
            public readonly string Letter;
            public readonly string Latin;
            public readonly int Line;
            public readonly int WordInLine;

            public Note(float time, Key key, string letter, string latin, int line, int wordInLine)
            {
                Time = time;
                Key = key;
                Letter = letter;
                Latin = latin;
                Line = line;
                WordInLine = wordInLine;
            }
        }

        [Header("Песня")]
        [SerializeField] private AudioClip clip;

        [Tooltip("JSON с тайм-кодами: lines[].text, lines[].words[].word/letter/start/end.")]
        [SerializeField] private TextAsset timings;

        [Header("Сложность")]
        [Tooltip("Насколько рано и насколько поздно засчитывается нажатие, с. " +
                 "Шире 0.09 с делать не стоит: столько между самыми тесными словами, " +
                 "и окна соседей начнут перекрываться.")]
        [SerializeField] private float hitWindow = 0.1f;

        [Tooltip("Внутри этого окна попадание считается точным, с.")]
        [SerializeField] private float perfectWindow = 0.05f;

        [Tooltip("Сколько секунд буква едет от края дорожки до линии попадания.")]
        [SerializeField] private float leadTime = 2f;

        [Tooltip("Поправка на задержку звука, с. Положительная сдвигает ноты позже: " +
                 "если попадать приходится раньше, чем слышно слово, её увеличивают.")]
        [SerializeField] private float offset;

        [Header("Прочее")]
        [Tooltip("Сколько ждать после последнего слова, прежде чем показать итог, с.")]
        [SerializeField] private float outroDelay = 1.5f;

        public AudioClip Clip => clip;

        public float HitWindow => Mathf.Max(0.02f, hitWindow);

        public float PerfectWindow => Mathf.Clamp(perfectWindow, 0.01f, HitWindow);

        public float LeadTime => Mathf.Max(0.3f, leadTime);

        public float Offset => offset;

        public float OutroDelay => Mathf.Max(0f, outroDelay);

        /// <summary>
        /// Разбирает тайм-коды в ноты и строки песни. Зовётся на каждый запуск:
        /// разбор дешёвый, а держать его результат в ассете значило бы тащить
        /// устаревшие данные после замены JSON.
        /// </summary>
        public bool TryBuild(out Note[] notes, out string[][] lines)
        {
            notes = null;
            lines = null;

            if (clip == null || timings == null)
            {
                Debug.LogError($"{name}: не задан звук или тайм-коды.", this);
                return false;
            }

            SongData data;

            try
            {
                data = JsonUtility.FromJson<SongData>(timings.text);
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{name}: тайм-коды не читаются: {e.Message}", this);
                return false;
            }

            if (data?.lines == null || data.lines.Length == 0)
            {
                Debug.LogError($"{name}: в тайм-кодах нет строк.", this);
                return false;
            }

            var result = new List<Note>(160);
            lines = new string[data.lines.Length][];

            for (var l = 0; l < data.lines.Length; l++)
            {
                var words = data.lines[l].words ?? Array.Empty<WordData>();
                lines[l] = new string[words.Length];

                for (var w = 0; w < words.Length; w++)
                {
                    var word = words[w];
                    lines[l][w] = word.word;

                    var letter = FirstLetter(word);

                    if (!KeyboardGrid.TryGetByCyrillic(letter, out var cell))
                    {
                        Debug.LogWarning($"{name}: буквы «{letter}» нет на клавиатуре, слово пропущено.", this);
                        continue;
                    }

                    result.Add(new Note(word.start + offset, cell.Key, cell.Cyrillic, cell.Latin, l, w));
                }
            }

            // Разметка и так идёт по порядку, но правила опираются на это жёстко:
            // ищут первую неотыгранную ноту, и одна перепутанная сломала бы всё после неё.
            result.Sort((a, b) => a.Time.CompareTo(b.Time));
            notes = result.ToArray();
            return notes.Length > 0;
        }

        private static string FirstLetter(WordData word)
        {
            var source = string.IsNullOrEmpty(word.letter) ? word.word : word.letter;

            foreach (var c in source ?? string.Empty)
            {
                if (char.IsLetter(c))
                {
                    return char.ToUpperInvariant(c).ToString();
                }
            }

            return string.Empty;
        }

        private void OnValidate()
        {
            if (perfectWindow > hitWindow)
            {
                perfectWindow = hitWindow;
            }
        }

        // Поля названы как в JSON: JsonUtility сопоставляет их по имени.
#pragma warning disable 0649
        [Serializable]
        private sealed class SongData
        {
            public LineData[] lines;
        }

        [Serializable]
        private sealed class LineData
        {
            public string text;
            public WordData[] words;
        }

        [Serializable]
        private sealed class WordData
        {
            public string word;
            public string letter;
            public float start;
            public float end;
        }
#pragma warning restore 0649
    }
}
