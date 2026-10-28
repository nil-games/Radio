using TMPro;
using UnityEngine;

namespace Radio.Broadcast
{
    /// <summary>
    /// Числа эфира за столом (GDD 02.22). Все они стартовые и подбираются плейтестом,
    /// поэтому живут в ассете: менять можно прямо во время игры, без перекомпиляции.
    /// </summary>
    /// <remarks>
    /// Таймеры здесь общие для всех эфиров: игрок привыкает к ритму (сколько ждёт
    /// слушатель, сколько перематывается кассета), а сложность задаёт поток элементов —
    /// лист «Сложность» таблицы Broadcast.xlsx, строка на каждый эфир каждой ночи.
    /// </remarks>
    [CreateAssetMenu(menuName = "Radio/Broadcast Settings", fileName = "BroadcastSettings")]
    public sealed class BroadcastSettings : ScriptableObject
    {
        [Header("Данные (CSV из Docs/Broadcast.xlsx, GDD 08.5)")]
        public TextAsset requestsCsv;
        public TextAsset cassettesCsv;
        public TextAsset adsCsv;

        [Tooltip("Лист «Сложность»: строка на эфир каждой ночи.")]
        public TextAsset difficultyCsv;

        [Tooltip("Лист «Сюжетные элементы»: звонки в эфир и объявления по ночам и эфирам.")]
        public TextAsset storyElementsCsv;

        [Header("Кассеты игрока")]
        [Tooltip("Кассеты, которые есть у игрока, по ID из таблицы. В эфир идут первые N — " +
                 "столько, сколько разрешает «Кассет в пуле» на листе «Сложность».")]
        public string[] startCassettes = { "К-01", "К-02", "К-03", "К-04" };

        [System.Serializable]
        public struct FoundCassette
        {
            [Tooltip("ID кассеты из таблицы: К-06.")]
            public string id;

            [Tooltip("Флаг мира: когда он true, кассета у игрока и входит в пул эфира.")]
            public string flag;
        }

        [Tooltip("Кассеты, найденные по сюжету: входят в пул, когда поставлен их флаг " +
                 "(К-06 «Гараж. Демо» — после звонка Яны). Пул по-прежнему ограничен листом «Сложность».")]
        public FoundCassette[] foundCassettes = System.Array.Empty<FoundCassette>();

        [System.Serializable]
        public struct PulledAd
        {
            [Tooltip("ID ролика из листа «Реклама»: R-01.")]
            public string id;

            [Tooltip("Флаг мира: когда он true, ролик больше не приходит.")]
            public string flag;
        }

        [Tooltip("Ролики, которые снимают по сюжету (R-01 «Уют» — после концерта «Гаража», ночь 3).")]
        public PulledAd[] pulledAds = System.Array.Empty<PulledAd>();

        [Tooltip("Добавить странную кассету К-05 сверх пула (для теста).")]
        public bool includeStrangeCassette = true;

        [Tooltip("ID странной кассеты, которую добавляет галочка выше.")]
        public string strangeCassetteId = "К-05";

        [Header("Таймеры — общие для всех эфиров")]
        [Tooltip("Сколько реальных секунд длится один эфир.")]
        public float durationSeconds = 90f;

        [Tooltip("Первый элемент приходит через столько секунд после начала.")]
        public float firstSpawnDelay = 2f;

        [Tooltip("Сколько ждёт заявка, пока слушатель не уйдёт, с.")]
        public float requestTimeout = 30f;

        [Tooltip("Сколько ждёт рекламный блок во входящих, с.")]
        public float adTimeout = 25f;

        [Tooltip("Сколько едет по ленте кассета (или кассеты одной заявки), с.")]
        public float cassetteTravel = 7f;

        [Tooltip("Сколько едет реклама, если в таблице время не задано, с.")]
        public float adTravelFallback = 15f;

        [Tooltip("Сколько ждёт сюжетный элемент (звонок, объявление), если в таблице не задано, с.")]
        public float storyTimeoutFallback = 40f;

        [Tooltip("Сколько едет сюжетный элемент после разговора, если в таблице не задано, с.")]
        public float storyTravelFallback = 8f;

        [Tooltip("Сколько кассета перематывается после результата, с.")]
        public float rewindSeconds = 20f;

        [Header("Реклама")]
        public bool adsEnabled = true;

        [Header("Сложность, если на листе «Сложность» нет строки")]
        [Tooltip("Раз в сколько секунд приходит новый элемент: случайно между x и y.")]
        public Vector2 spawnInterval = new Vector2(10f, 15f);

        [Tooltip("Больше этого во входящих не бывает: лишнее ждёт.")]
        public int maxInbox = 5;

        [Tooltip("Доля рекламы среди новых элементов.")]
        [Range(0f, 1f)] public float adShare = 0.25f;

        [Header("Проверка (GDD 02.22)")]
        public ShapeMode shapeMode = ShapeMode.Underlay;

        [Tooltip("Подложка b (для Underlay) или база k (для Base).")]
        public float shapePad = 0.2f;

        [Tooltip("Сколько вайба помещается от центра до края пятиугольника.")]
        public float radarScale = 10f;

        [Header("Вид")]
        public TMP_FontAsset font;
    }
}
