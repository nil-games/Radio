using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Radio.Broadcast
{
    /// <summary>Заявка слушателя: строка листа «Заявки» таблицы Broadcast.xlsx.</summary>
    public sealed class ListenerRequest
    {
        public string Id;
        public string Listener;
        public string Faction;

        /// <summary>Текст с разметкой TextMeshPro: намёки уже в &lt;b&gt;.</summary>
        public string Text;

        /// <summary>Сложность по пяти вайбам, порядок — <see cref="VibeRadar.Names"/>.</summary>
        public int[] Vibes = new int[VibeRadar.Axes];

        public int Level;
        public int Slots;
        public string SuccessText;
        public string SuccessResult;
        public string FailText;
        public string FailResult;
        public int NightFrom = 1;
        public int NightTo = 7;

        public bool IsForNight(int night) => night >= NightFrom && night <= NightTo;
    }

    /// <summary>Кассета: строка листа «Кассеты».</summary>
    public sealed class Cassette
    {
        public string Id;
        public string Title;
        public string Description;
        public int[] Vibes = new int[VibeRadar.Axes];

        /// <summary>Странная — свойство кассеты, а не вайб: её вклад в фигуру красный.</summary>
        public bool Strange;
    }

    /// <summary>Рекламный блок: строка листа «Реклама».</summary>
    public sealed class AdSpot
    {
        public string Id;
        public string Advertiser;
        public string Text;
        public int Money;
        public float TravelSeconds;
        public string Faction;
        public string Consequence;
    }

    /// <summary>
    /// Сложность одного эфира: строка листа «Сложность». Таймеры (ожидание заявки,
    /// перемотка, время в пути) здесь не меняются — они общие для всех эфиров
    /// (<see cref="BroadcastSettings"/>), сложность задаёт поток элементов.
    /// </summary>
    public sealed class DifficultyRow
    {
        public int Night;
        public int Broadcast;
        public int CassettePool;
        public float SpawnMin;
        public float SpawnMax;
        public float AdShare;

        /// <summary>Звонки в эфир, объявления — задел: таких элементов пока нет.</summary>
        public float OtherShare;

        public int LevelMin;
        public int LevelMax;

        /// <summary>Слотов в заявке; 0 — по уровню заявки (лист «Справочник»).</summary>
        public int Slots;

        public int MaxInbox;
    }

    /// <summary>Тип сюжетного элемента эфира.</summary>
    public enum StoryElementKind
    {
        /// <summary>Звонок в прямой эфир: разговор со слушателем.</summary>
        Call,

        /// <summary>Объявление: ведущий читает его в эфир.</summary>
        Announcement,
    }

    /// <summary>
    /// Сюжетный элемент эфира: строка листа «Сюжетные элементы». Приходит во входящие
    /// в заданную секунду конкретного эфира конкретной ночи; клик запускает узел Yarn.
    /// </summary>
    public sealed class StoryElement
    {
        public string Id;
        public int Night;
        public int Broadcast;
        public float AppearAt;
        public StoryElementKind Kind;
        public string Title;
        public string Node;

        /// <summary>
        /// Флаги мира через «;»: все должны быть истинны, «!» перед именем — ложен.
        /// Пусто — элемент приходит всегда.
        /// </summary>
        public string Condition;

        public float Timeout;
        public float Travel;
    }

    /// <summary>
    /// Чтение данных эфира из CSV, выгруженных из <c>Docs/Broadcast.xlsx</c> (GDD 08.5).
    /// </summary>
    /// <remarks>
    /// Таблицу правят часто и сохраняют из русского Excel, поэтому чтение терпимо ко всему,
    /// что Excel может сделать с файлом: разделитель <c>;</c> или <c>,</c> (определяется по
    /// строке заголовков), UTF-8 с BOM и без, кавычки с запятыми и переносами внутри.
    /// Колонки ищутся по названию заголовка, а не по номеру: вставка или перестановка
    /// колонок в таблице ничего не ломает. Строки «EX-…» — примеры формата, пропускаются.
    /// </remarks>
    public static class BroadcastData
    {
        private static readonly Regex Bold = new Regex(@"\*\*(.+?)\*\*");

        public static List<ListenerRequest> ReadRequests(TextAsset csv)
        {
            var result = new List<ListenerRequest>();
            var table = Table.Parse(csv, "Requests");

            if (table == null)
            {
                return result;
            }

            var ids = new HashSet<string>();

            foreach (var row in table.Rows)
            {
                var id = table.Get(row, "ID");

                if (Skip(id))
                {
                    continue;
                }

                var request = new ListenerRequest
                {
                    Id = id,
                    Listener = table.Get(row, "Слушатель"),
                    Faction = table.Get(row, "Фракция"),
                    Text = Markup(table.Get(row, "Заявка")),
                    Level = table.GetInt(row, "Уровень"),
                    Slots = Mathf.Max(1, table.GetInt(row, "Слотов кассет")),
                    SuccessText = table.Get(row, "При успехе: текст"),
                    SuccessResult = table.Get(row, "При успехе: результат"),
                    FailText = table.Get(row, "При провале: текст"),
                    FailResult = table.Get(row, "При провале: результат"),
                };

                ReadVibes(table, row, request.Vibes);
                ReadNights(table.Get(row, "Ночи"), request);

                var used = 0;
                foreach (var vibe in request.Vibes)
                {
                    if (vibe > 0)
                    {
                        used++;
                    }
                }

                if (!ids.Add(id))
                {
                    Warn("Requests", $"повторяется ID {id}");
                }

                if (string.IsNullOrWhiteSpace(request.Text))
                {
                    Warn("Requests", $"{id}: пустой текст заявки, строка пропущена");
                    continue;
                }

                if (used < 2)
                {
                    // Правило тестовой сборки (GDD 02.22): в заявке минимум два вайба.
                    Warn("Requests", $"{id}: вайбов меньше двух");
                }

                result.Add(request);
            }

            return result;
        }

        public static List<Cassette> ReadCassettes(TextAsset csv)
        {
            var result = new List<Cassette>();
            var table = Table.Parse(csv, "Cassettes");

            if (table == null)
            {
                return result;
            }

            foreach (var row in table.Rows)
            {
                var id = table.Get(row, "ID");

                if (Skip(id))
                {
                    continue;
                }

                var cassette = new Cassette
                {
                    Id = id,
                    Title = table.Get(row, "Название"),
                    Description = table.Get(row, "Описание"),
                    Strange = table.Get(row, "Странная").Trim().StartsWith("да", StringComparison.OrdinalIgnoreCase),
                };

                ReadVibes(table, row, cassette.Vibes);

                var norm = table.Get(row, "Проверка нормы");
                if (!string.IsNullOrWhiteSpace(norm) && !norm.Trim().Equals("ок", StringComparison.OrdinalIgnoreCase))
                {
                    Warn("Cassettes", $"{id}: «{norm}» в проверке нормы");
                }

                result.Add(cassette);
            }

            return result;
        }

        public static List<AdSpot> ReadAds(TextAsset csv)
        {
            var result = new List<AdSpot>();
            var table = Table.Parse(csv, "Ads");

            if (table == null)
            {
                return result;
            }

            foreach (var row in table.Rows)
            {
                var id = table.Get(row, "ID");

                if (Skip(id))
                {
                    continue;
                }

                result.Add(new AdSpot
                {
                    Id = id,
                    Advertiser = table.Get(row, "Рекламодатель"),
                    Text = Markup(table.Get(row, "Текст ролика")),
                    Money = table.GetInt(row, "Деньги"),
                    TravelSeconds = table.GetFloat(row, "Время в пути"),
                    Faction = table.Get(row, "Фракция"),
                    Consequence = table.Get(row, "Последствия"),
                });
            }

            return result;
        }

        public static List<DifficultyRow> ReadDifficulty(TextAsset csv)
        {
            var result = new List<DifficultyRow>();
            var table = Table.Parse(csv, "Difficulty");

            if (table == null)
            {
                return result;
            }

            foreach (var row in table.Rows)
            {
                var night = table.GetInt(row, "Ночь");

                if (night <= 0)
                {
                    continue;
                }

                result.Add(new DifficultyRow
                {
                    Night = night,
                    Broadcast = Mathf.Max(1, table.GetInt(row, "Эфир")),
                    CassettePool = table.GetInt(row, "Кассет в пуле"),
                    SpawnMin = table.GetFloat(row, "Интервал заявок от"),
                    SpawnMax = table.GetFloat(row, "Интервал заявок до"),
                    AdShare = Share(table.Get(row, "Доля рекламы")),
                    OtherShare = Share(table.Get(row, "Доля других элементов")),
                    LevelMin = Mathf.Max(1, table.GetInt(row, "Уровень заявок от")),
                    LevelMax = Mathf.Max(1, table.GetInt(row, "Уровень заявок до")),
                    Slots = table.GetInt(row, "Слотов в заявке"),
                    MaxInbox = table.GetInt(row, "Макс. во входящих"),
                });
            }

            return result;
        }

        public static List<StoryElement> ReadStoryElements(TextAsset csv)
        {
            var result = new List<StoryElement>();
            var table = Table.Parse(csv, "StoryElements");

            if (table == null)
            {
                return result;
            }

            foreach (var row in table.Rows)
            {
                var id = table.Get(row, "ID");

                if (Skip(id))
                {
                    continue;
                }

                var kind = table.Get(row, "Тип").Trim().ToLowerInvariant();
                var element = new StoryElement
                {
                    Id = id,
                    Night = table.GetInt(row, "Ночь"),
                    Broadcast = Mathf.Max(1, table.GetInt(row, "Эфир")),
                    AppearAt = table.GetFloat(row, "Появление"),
                    Kind = kind.StartsWith("объяв") ? StoryElementKind.Announcement : StoryElementKind.Call,
                    Title = table.Get(row, "Заголовок"),
                    Node = table.Get(row, "Узел"),
                    Condition = table.Get(row, "Условие"),
                    Timeout = table.GetFloat(row, "Ожидание"),
                    Travel = table.GetFloat(row, "Время в пути"),
                };

                if (string.IsNullOrWhiteSpace(element.Node))
                {
                    Warn("StoryElements", $"{id}: не указан узел Yarn, строка пропущена");
                    continue;
                }

                result.Add(element);
            }

            return result;
        }

        /// <summary>«25 %», «0,25» или «25» → 0,25.</summary>
        private static float Share(string value)
        {
            value = (value ?? string.Empty).Replace("%", string.Empty).Replace(',', '.').Trim();

            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                return 0f;
            }

            return Mathf.Clamp01(number > 1f ? number / 100f : number);
        }

        /// <summary>«**слово**» из таблицы → жирный TextMeshPro.</summary>
        public static string Markup(string text) =>
            string.IsNullOrEmpty(text) ? string.Empty : Bold.Replace(text, "<b>$1</b>");

        private static bool Skip(string id) =>
            string.IsNullOrWhiteSpace(id) || id.Trim().StartsWith("EX-", StringComparison.OrdinalIgnoreCase);

        private static void ReadVibes(Table table, string[] row, int[] into)
        {
            for (var i = 0; i < VibeRadar.Axes; i++)
            {
                into[i] = table.GetInt(row, VibeRadar.Names[i]);
            }
        }

        /// <summary>«1–7», «2-7» или «3» → диапазон ночей.</summary>
        private static void ReadNights(string value, ListenerRequest request)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var parts = value.Split(new[] { '–', '—', '-' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length > 0 && int.TryParse(parts[0].Trim(), out var from))
            {
                request.NightFrom = from;
                request.NightTo = from;
            }

            if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out var to))
            {
                request.NightTo = to;
            }
        }

        private static void Warn(string sheet, string message) =>
            Debug.LogWarning($"{nameof(BroadcastData)} {sheet}.csv: {message}");

        /// <summary>CSV с колонками по заголовку.</summary>
        private sealed class Table
        {
            private readonly Dictionary<string, int> _columns = new Dictionary<string, int>();
            private readonly string _sheet;
            private readonly HashSet<string> _reported = new HashSet<string>();

            public readonly List<string[]> Rows = new List<string[]>();

            private Table(string sheet) => _sheet = sheet;

            public static Table Parse(TextAsset asset, string sheet)
            {
                if (asset == null)
                {
                    Warn(sheet, "файл не задан");
                    return null;
                }

                var text = asset.text;

                if (text.Length > 0 && text[0] == '﻿')
                {
                    text = text.Substring(1);
                }

                var lines = Split(text, DetectSeparator(text));

                if (lines.Count == 0)
                {
                    Warn(sheet, "пустой файл");
                    return null;
                }

                var table = new Table(sheet);
                var header = lines[0];

                for (var i = 0; i < header.Length; i++)
                {
                    var name = header[i].Trim();

                    if (name.Length > 0 && !table._columns.ContainsKey(name))
                    {
                        table._columns[name] = i;
                    }
                }

                for (var i = 1; i < lines.Count; i++)
                {
                    table.Rows.Add(lines[i]);
                }

                return table;
            }

            /// <summary>
            /// Значение колонки. Имя можно дать началом заголовка: «Заявка» найдёт
            /// «Заявка (около абзаца, намёки **жирным**)» — заголовки в таблице длинные
            /// и пояснения в скобках в них ещё будут меняться.
            /// </summary>
            public string Get(string[] row, string column)
            {
                var index = Find(column);
                return index >= 0 && index < row.Length ? row[index].Trim() : string.Empty;
            }

            public int GetInt(string[] row, string column) =>
                Mathf.RoundToInt(GetFloat(row, column));

            public float GetFloat(string[] row, string column)
            {
                var value = Get(row, column).Replace(',', '.');
                return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                    ? number
                    : 0f;
            }

            private int Find(string column)
            {
                if (_columns.TryGetValue(column, out var exact))
                {
                    return exact;
                }

                foreach (var pair in _columns)
                {
                    if (pair.Key.StartsWith(column, StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Value;
                    }
                }

                if (_reported.Add(column))
                {
                    Warn(_sheet, $"нет колонки «{column}»");
                }

                return -1;
            }

            /// <summary>Разделитель по первой строке: чего больше вне кавычек — того и берём.</summary>
            private static char DetectSeparator(string text)
            {
                int semicolons = 0, commas = 0;
                var quoted = false;

                foreach (var c in text)
                {
                    if (c == '"')
                    {
                        quoted = !quoted;
                    }
                    else if (!quoted && (c == '\n' || c == '\r'))
                    {
                        break;
                    }
                    else if (!quoted && c == ';')
                    {
                        semicolons++;
                    }
                    else if (!quoted && c == ',')
                    {
                        commas++;
                    }
                }

                return semicolons >= commas ? ';' : ',';
            }

            private static List<string[]> Split(string text, char separator)
            {
                var rows = new List<string[]>();
                var row = new List<string>();
                var cell = new StringBuilder();
                var quoted = false;

                for (var i = 0; i < text.Length; i++)
                {
                    var c = text[i];

                    if (quoted)
                    {
                        if (c == '"')
                        {
                            if (i + 1 < text.Length && text[i + 1] == '"')
                            {
                                cell.Append('"');
                                i++;
                            }
                            else
                            {
                                quoted = false;
                            }
                        }
                        else
                        {
                            cell.Append(c);
                        }

                        continue;
                    }

                    if (c == '"')
                    {
                        quoted = true;
                    }
                    else if (c == separator)
                    {
                        row.Add(cell.ToString());
                        cell.Clear();
                    }
                    else if (c == '\n' || c == '\r')
                    {
                        if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        {
                            i++;
                        }

                        row.Add(cell.ToString());
                        cell.Clear();
                        AddRow(rows, row);
                        row = new List<string>();
                    }
                    else
                    {
                        cell.Append(c);
                    }
                }

                if (cell.Length > 0 || row.Count > 0)
                {
                    row.Add(cell.ToString());
                    AddRow(rows, row);
                }

                return rows;
            }

            private static void AddRow(List<string[]> rows, List<string> row)
            {
                foreach (var cell in row)
                {
                    if (!string.IsNullOrWhiteSpace(cell))
                    {
                        rows.Add(row.ToArray());
                        return;
                    }
                }
            }
        }
    }
}
