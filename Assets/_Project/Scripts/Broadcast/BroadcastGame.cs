using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Radio.Broadcast
{
    public enum InboxKind
    {
        Request,
        Ad,

        /// <summary>Сюжетный элемент: звонок в эфир или объявление (лист «Сюжетные элементы»).</summary>
        Story,
    }

    /// <summary>Элемент во входящих: заявка или реклама, с таймером ожидания.</summary>
    public sealed class InboxItem
    {
        public InboxKind Kind;
        public ListenerRequest Request;
        public AdSpot Ad;
        public StoryElement Story;
        public float Timeout;
        public float TimeLeft;

        /// <summary>Сколько кассет можно поставить: по уровню заявки или из листа «Сложность».</summary>
        public int Slots = 1;

        public float Progress => Timeout <= 0f ? 0f : Mathf.Clamp01(TimeLeft / Timeout);
    }

    public enum CassetteState
    {
        Ready,

        /// <summary>Едет по ленте или ждёт в её конце, пока игрок не посмотрит результат.</summary>
        OnAir,

        Rewinding,
    }

    public sealed class CassetteSlot
    {
        public Cassette Data;
        public CassetteState State;
        public float RewindLeft;
        public int Order;
    }

    /// <summary>Результат проверки заявки: фигуры, шанс, исход и путь точки.</summary>
    public sealed class CheckResult
    {
        public float[] Request;

        /// <summary>Фигура всех поставленных кассет.</summary>
        public float[] All;

        /// <summary>Фигура обычных кассет; null, если на заявке только странные.</summary>
        public float[] Normal;

        public bool HasStrange;
        public float Chance;
        public CheckOutcome Outcome;
        public List<Vector2> Path;

        public bool Success => Outcome != CheckOutcome.Fail;
    }

    /// <summary>Что едет по ленте: кассеты одной заявки или рекламный блок.</summary>
    public sealed class TapeItem
    {
        public InboxItem Source;
        public List<CassetteSlot> Cassettes = new List<CassetteSlot>();
        public float Elapsed;
        public float Duration;
        public CheckResult Result;

        /// <summary>
        /// Сюжетный элемент уехал коротко (<c>&lt;&lt;broadcast_quick&gt;&gt;</c>): отменённый
        /// концерт не держит заявки.
        /// </summary>
        public bool Quick;

        public bool IsAd => Source.Kind == InboxKind.Ad;

        /// <summary>Едет без проверки и сам уходит с ленты: реклама и сюжетные элементы.</summary>
        public bool AutoComplete => Source.Kind != InboxKind.Request;
        public bool Arrived => Elapsed >= Duration;
        public float Progress => Duration <= 0f ? 1f : Mathf.Clamp01(Elapsed / Duration);
    }

    /// <summary>Итоги эфира — по образцу итогов смены в Dispatch (GDD 02.22).</summary>
    public sealed class BroadcastTally
    {
        public int Success;
        public int Failed;

        /// <summary>
        /// Заявки, которые так и не поставили: кончилось ожидание или эфир. Это провал (GDD 02.22).
        /// </summary>
        public int Expired;

        /// <summary>Реклама, которую не поставили.</summary>
        public int Missed;

        public int Points;
        public int Money;
        public int Chthon;

        /// <summary>Остальные последствия: репутация фракций, шкалы. В тесте — только для сводки.</summary>
        public readonly List<string> Notes = new List<string>();

        public bool Perfect => Failed == 0 && Expired == 0 && Missed == 0 && Success > 0;
    }

    public enum BroadcastPhase
    {
        Idle,
        Running,

        /// <summary>Время вышло: новые элементы не приходят, доезжает то, что в пути.</summary>
        Wrapping,

        Summary,
    }

    /// <summary>
    /// Правила эфира за столом (GDD 02.22): входящие и их таймеры, кассеты и перемотка,
    /// лента, реклама, проверка и итоги. Картинку рисует <see cref="BroadcastView"/>.
    /// </summary>
    /// <remarks>
    /// Время эфира и все таймеры идут только пока <see cref="Paused"/> снят: пока игрок
    /// читает заявку или смотрит результат, ничего не тикает (за чтение не наказываем).
    /// Всё считается в реальных секундах без масштаба времени.
    /// </remarks>
    public sealed class BroadcastGame : MonoBehaviour
    {
        private static readonly Regex PointsRegex = new Regex(@"([+\-−]?\s*\d+)\s*балл", RegexOptions.IgnoreCase);

        [SerializeField] private BroadcastSettings settings;

        private readonly List<InboxItem> _inbox = new List<InboxItem>();
        private readonly List<TapeItem> _tape = new List<TapeItem>();
        private readonly List<CassetteSlot> _cassettes = new List<CassetteSlot>();
        private readonly HashSet<string> _usedRequests = new HashSet<string>();

        private List<ListenerRequest> _requests;
        private List<Cassette> _allCassettes;
        private List<AdSpot> _ads;
        private System.Random _random;
        private float _nextSpawn;
        private AdSpot _lastAd;

        /// <summary>Эфир закончился и игрок закрыл сводку.</summary>
        public event Action<BroadcastTally> Finished;

        /// <summary>Что-то поменялось в составе входящих, ленты или кассет.</summary>
        public event Action Changed;

        public BroadcastSettings Settings => settings;
        public BroadcastPhase Phase { get; private set; }
        public BroadcastTally Tally { get; private set; } = new BroadcastTally();
        public float Elapsed { get; private set; }
        public float Duration => settings.durationSeconds;
        public int ClockStart { get; private set; }
        public int ClockEnd { get; private set; }
        public bool Paused { get; set; }

        public IReadOnlyList<InboxItem> Inbox => _inbox;
        public IReadOnlyList<TapeItem> Tape => _tape;
        public IReadOnlyList<CassetteSlot> Cassettes => _cassettes;

        /// <summary>Реклама в пути: пока она едет, поставить ничего нельзя.</summary>
        public bool AdOnAir
        {
            get
            {
                foreach (var item in _tape)
                {
                    if (item.IsAd && !item.Arrived)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Игровые минуты от начала смены, которые сейчас показывают часы эфира.</summary>
        public int ClockMinutes =>
            ClockStart + Mathf.FloorToInt((ClockEnd - ClockStart) * Mathf.Clamp01(Elapsed / Mathf.Max(1f, Duration)));

        /// <summary>Сложность текущего эфира (строка листа «Сложность»).</summary>
        public DifficultyRow Difficulty { get; private set; }

        /// <summary>
        /// Начать эфир номер <paramref name="broadcast"/> ночи <paramref name="night"/>.
        /// Часы на экране идут от <paramref name="clockStart"/> до <paramref name="clockEnd"/>, в минутах смены.
        /// </summary>
        public void Begin(int night, int broadcast, int clockStart, int clockEnd)
        {
            _requests ??= BroadcastData.ReadRequests(settings.requestsCsv);
            _allCassettes ??= BroadcastData.ReadCassettes(settings.cassettesCsv);
            _ads ??= BroadcastData.ReadAds(settings.adsCsv);
            _difficulty ??= BroadcastData.ReadDifficulty(settings.difficultyCsv);
            _storyElements ??= BroadcastData.ReadStoryElements(settings.storyElementsCsv);
            _random = new System.Random();
            Difficulty = FindDifficulty(Mathf.Max(1, night), Mathf.Max(1, broadcast));

            // Сюжетные элементы этого эфира ждут своей секунды; условие проверяется,
            // когда секунда наступила, — к ней флаги мира могут успеть поменяться.
            _pendingStory.Clear();
            _pendingStory.AddRange(_storyElements.FindAll(e => e.Night == Mathf.Max(1, night) && e.Broadcast == Mathf.Max(1, broadcast)));

            _inbox.Clear();
            _tape.Clear();
            _cassettes.Clear();
            _usedRequests.Clear();
            Tally = new BroadcastTally();
            Elapsed = 0f;
            Paused = false;
            ClockStart = clockStart;
            ClockEnd = Mathf.Max(clockStart, clockEnd);
            _nextSpawn = settings.firstSpawnDelay;
            _night = Mathf.Max(1, night);

            // В эфир идут первые N кассет игрока — сколько разрешает пул этого эфира.
            var pool = Difficulty.CassettePool > 0 ? Difficulty.CassettePool : settings.startCassettes.Length;

            foreach (var id in settings.startCassettes)
            {
                if (_cassettes.Count >= pool)
                {
                    break;
                }

                AddCassette(id);
            }

            // Найденные по сюжету — в оставшиеся места пула, если их флаг поставлен.
            var world = Radio.World.GameSession.Current != null ? Radio.World.GameSession.Current.World : null;

            foreach (var found in settings.foundCassettes ?? System.Array.Empty<BroadcastSettings.FoundCassette>())
            {
                if (_cassettes.Count < pool && world != null
                    && world.TryGet<bool>(found.flag, out var owned) && owned)
                {
                    AddCassette(found.id);
                }
            }

            if (settings.includeStrangeCassette)
            {
                AddCassette(settings.strangeCassetteId);
            }

            Phase = BroadcastPhase.Running;
            Changed?.Invoke();
        }

        private int _night = 1;
        private List<DifficultyRow> _difficulty;
        private List<StoryElement> _storyElements;
        private readonly List<StoryElement> _pendingStory = new List<StoryElement>();

        /// <summary>
        /// Строка сложности: точная (ночь и эфир), иначе ближайший более ранний эфир той же
        /// ночи, иначе последняя строка более ранней ночи. Так одной строкой на ночь можно
        /// задать все её эфиры, а уточнять — только нужные.
        /// </summary>
        private DifficultyRow FindDifficulty(int night, int broadcast)
        {
            DifficultyRow best = null;

            foreach (var row in _difficulty)
            {
                if (row.Night > night || (row.Night == night && row.Broadcast > broadcast))
                {
                    continue;
                }

                if (best == null || row.Night > best.Night || (row.Night == best.Night && row.Broadcast > best.Broadcast))
                {
                    best = row;
                }
            }

            if (best != null)
            {
                return best;
            }

            Debug.LogWarning($"{nameof(BroadcastGame)}: на листе «Сложность» нет строки для ночи {night}, " +
                             "беру запасные числа из настроек.", this);
            return new DifficultyRow
            {
                Night = night,
                Broadcast = broadcast,
                SpawnMin = settings.spawnInterval.x,
                SpawnMax = settings.spawnInterval.y,
                AdShare = settings.adShare,
                LevelMin = 1,
                LevelMax = 5,
                MaxInbox = settings.maxInbox,
            };
        }

        private void AddCassette(string id)
        {
            var data = _allCassettes.Find(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

            if (data == null)
            {
                Debug.LogWarning($"{nameof(BroadcastGame)}: кассеты {id} нет в таблице.", this);
                return;
            }

            if (_cassettes.Exists(c => c.Data == data))
            {
                return;
            }

            _cassettes.Add(new CassetteSlot { Data = data, Order = _cassettes.Count });
        }

        private void Update()
        {
            if (Phase == BroadcastPhase.Idle || Phase == BroadcastPhase.Summary || Paused)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            var changed = false;

            if (Phase == BroadcastPhase.Running)
            {
                Elapsed += dt;
                changed |= TickInbox(dt);
                changed |= TickStory();
                changed |= TickSpawn(dt);

                if (Elapsed >= Duration)
                {
                    Elapsed = Duration;
                    Wrap();
                    changed = true;
                }
            }

            changed |= TickTape(dt);
            changed |= TickRewind(dt);

            if (Phase == BroadcastPhase.Wrapping && _tape.Count == 0)
            {
                Phase = BroadcastPhase.Summary;
                changed = true;
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>По ленте едет концерт: заявки ждут, их таймеры стоят (ночь 3).</summary>
        public bool ConcertOnAir =>
            _tape.Exists(t => !t.Quick && t.Source.Kind == InboxKind.Story && t.Source.Story.Kind == StoryElementKind.Concert);

        /// <summary>
        /// Событие посреди эфира (налёт): разговор надо начать прямо сейчас. Слушает
        /// экран эфира — прячется и запускает узел, как у сюжетного элемента.
        /// </summary>
        public event Action<StoryElement> StoryEventFired;

        private bool TickInbox(float dt)
        {
            var changed = false;
            var concert = ConcertOnAir;

            for (var i = _inbox.Count - 1; i >= 0; i--)
            {
                var item = _inbox[i];

                if (concert && item.Kind == InboxKind.Request)
                {
                    continue;
                }

                item.TimeLeft -= dt;

                if (item.TimeLeft > 0f)
                {
                    continue;
                }

                // Не успел: заявка — провал, реклама и сюжетный элемент — пропуск.
                if (item.Kind == InboxKind.Request)
                {
                    Tally.Expired++;
                }
                else
                {
                    Tally.Missed++;
                }

                if (item.Kind == InboxKind.Story)
                {
                    Tally.Notes.Add($"Пропущено: {item.Story.Title}");
                }

                _inbox.RemoveAt(i);
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Сюжетные элементы приходят в свою секунду эфира, сверх лимита входящих:
        /// сюжет не должен пропасть из-за того, что справа скопились заявки.
        /// </summary>
        private bool TickStory()
        {
            var changed = false;

            for (var i = _pendingStory.Count - 1; i >= 0; i--)
            {
                var element = _pendingStory[i];

                if (Elapsed < element.AppearAt)
                {
                    continue;
                }

                // Событие ждёт, пока сложатся условия и пойдёт концерт: налёт в 04:15
                // случается, только если концерт в этот момент звучит (или начнётся позже).
                if (element.Kind == StoryElementKind.Event)
                {
                    if (!ConcertOnAir || !ConditionMet(element.Condition))
                    {
                        continue;
                    }

                    _pendingStory.RemoveAt(i);
                    StoryEventFired?.Invoke(element);
                    changed = true;
                    continue;
                }

                _pendingStory.RemoveAt(i);

                if (!ConditionMet(element.Condition))
                {
                    continue;
                }

                var timeout = element.Timeout > 0f ? element.Timeout : settings.storyTimeoutFallback;
                _inbox.Add(new InboxItem
                {
                    Kind = InboxKind.Story,
                    Story = element,
                    Timeout = timeout,
                    TimeLeft = timeout,
                });
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// «FLAG_A;!FLAG_B;MODE=read|debunk» — все условия выполняются: флаг истинен,
        /// с «!» — ложен, с «=» — значение флага (текст, число или true/false) одно из перечисленных.
        /// </summary>
        private static bool ConditionMet(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                return true;
            }

            var world = Radio.World.GameSession.Current != null ? Radio.World.GameSession.Current.World : null;

            foreach (var raw in condition.Split(';'))
            {
                var equals = raw.IndexOf('=');

                if (equals > 0)
                {
                    if (!ValueMatches(world, raw.Substring(0, equals).Trim().TrimStart('$'), raw.Substring(equals + 1)))
                    {
                        return false;
                    }

                    continue;
                }

                var name = raw.Trim().TrimStart('$');
                var negate = name.StartsWith("!");
                name = name.TrimStart('!').TrimStart('$').Trim();

                if (name.Length == 0)
                {
                    continue;
                }

                var value = world != null && world.TryGet<bool>(name, out var flag) && flag;

                if (value == negate)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValueMatches(Radio.World.WorldState world, string name, string allowed)
        {
            string value = null;

            if (world != null)
            {
                if (world.TryGet<string>(name, out var text))
                {
                    value = text;
                }
                else if (world.TryGet<bool>(name, out var flag))
                {
                    value = flag ? "true" : "false";
                }
                else if (world.TryGet<float>(name, out var number))
                {
                    value = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            foreach (var option in allowed.Split('|'))
            {
                if (string.Equals(option.Trim(), value ?? "false", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TickSpawn(float dt)
        {
            _nextSpawn -= dt;

            var maxInbox = Difficulty.MaxInbox > 0 ? Difficulty.MaxInbox : settings.maxInbox;

            if (_nextSpawn > 0f || _inbox.Count >= maxInbox)
            {
                return false;
            }

            _nextSpawn = Mathf.Lerp(Difficulty.SpawnMin, Difficulty.SpawnMax, (float)_random.NextDouble());
            var wantAd = settings.adsEnabled && _ads.Count > 0 && _random.NextDouble() < Difficulty.AdShare;

            if (wantAd || !SpawnRequest())
            {
                return SpawnAd() || SpawnRequest();
            }

            return true;
        }

        private bool SpawnRequest()
        {
            var pool = _requests.FindAll(r => Fits(r) && !_usedRequests.Contains(r.Id));

            if (pool.Count == 0)
            {
                // Пул кончился за один эфир — начинаем по кругу, лучше повтор, чем пустота.
                _usedRequests.Clear();
                pool = _requests.FindAll(Fits);
            }

            if (pool.Count == 0)
            {
                // Под уровни этого эфира в таблице нет ни одной заявки: берём любые этой ночи.
                pool = _requests.FindAll(r => r.IsForNight(_night));
            }

            if (pool.Count == 0)
            {
                return false;
            }

            var request = pool[_random.Next(pool.Count)];
            _usedRequests.Add(request.Id);
            _inbox.Add(new InboxItem
            {
                Kind = InboxKind.Request,
                Request = request,
                Slots = Difficulty.Slots > 0 ? Difficulty.Slots : request.Slots,
                Timeout = settings.requestTimeout,
                TimeLeft = settings.requestTimeout,
            });
            return true;
        }

        private bool Fits(ListenerRequest request) =>
            request.IsForNight(_night) && request.Level >= Difficulty.LevelMin && request.Level <= Difficulty.LevelMax;

        private bool SpawnAd()
        {
            // Одна реклама за раз: пока одна ждёт во входящих или едет по ленте, новой нет.
            if (!settings.adsEnabled || _ads.Count == 0 || _inbox.Exists(i => i.Kind == InboxKind.Ad)
                || _tape.Exists(t => t.IsAd))
            {
                return false;
            }

            // Снятые по сюжету ролики (R-01 после концерта) больше не приходят.
            var ads = _ads.FindAll(a => !IsPulled(a));

            if (ads.Count == 0)
            {
                return false;
            }

            var ad = ads[_random.Next(ads.Count)];

            if (ad == _lastAd && ads.Count > 1)
            {
                ad = ads[(ads.IndexOf(ad) + 1) % ads.Count];
            }

            _lastAd = ad;
            _inbox.Add(new InboxItem
            {
                Kind = InboxKind.Ad,
                Ad = ad,
                Timeout = settings.adTimeout,
                TimeLeft = settings.adTimeout,
            });
            return true;
        }

        private bool IsPulled(AdSpot ad)
        {
            var world = Radio.World.GameSession.Current != null ? Radio.World.GameSession.Current.World : null;

            foreach (var pulled in settings.pulledAds ?? Array.Empty<BroadcastSettings.PulledAd>())
            {
                if (string.Equals(pulled.id, ad.Id, StringComparison.OrdinalIgnoreCase)
                    && world != null && world.TryGet<bool>(pulled.flag, out var flag) && flag)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TickTape(float dt)
        {
            var changed = false;

            for (var i = _tape.Count - 1; i >= 0; i--)
            {
                var item = _tape[i];

                // Кассета, доехавшая до конца, ждёт клика за результатом. Реклама и сюжетные
                // элементы ждать нечего — снимаем их, даже если они оказались в конце ленты
                // не через этот кадр (например, отладочной перемоткой).
                if (item.Arrived && !item.AutoComplete)
                {
                    continue;
                }

                item.Elapsed += dt;

                if (!item.Arrived)
                {
                    continue;
                }

                changed = true;

                // Реклама результата не ждёт: доехала — деньги в кассе.
                if (item.IsAd)
                {
                    var ad = item.Source.Ad;
                    Tally.Money += ad.Money;

                    if (!string.IsNullOrWhiteSpace(ad.Consequence) && ad.Consequence.Trim() != "—")
                    {
                        Tally.Notes.Add($"{ad.Advertiser}: {ad.Consequence}");
                    }
                }

                // Реклама и сюжетные элементы проверки не ждут: доехали — ушли с ленты.
                if (item.AutoComplete)
                {
                    _tape.RemoveAt(i);
                }
            }

            return changed;
        }

        private bool TickRewind(float dt)
        {
            var changed = false;

            foreach (var slot in _cassettes)
            {
                if (slot.State != CassetteState.Rewinding)
                {
                    continue;
                }

                slot.RewindLeft -= dt;

                if (slot.RewindLeft <= 0f)
                {
                    slot.State = CassetteState.Ready;
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// Время вышло. Необработанная заявка — слушатель не дождался (провал), как и при
        /// истёкшем таймере; непоставленная реклама — пропуск.
        /// </summary>
        private void Wrap()
        {
            Phase = BroadcastPhase.Wrapping;

            foreach (var item in _inbox)
            {
                if (item.Kind == InboxKind.Request)
                {
                    Tally.Expired++;
                }
                else
                {
                    Tally.Missed++;
                }
            }

            _inbox.Clear();
        }

        public bool CanPlace => Phase == BroadcastPhase.Running && !AdOnAir;

        /// <summary>Поставить кассеты на заявку: они уезжают на ленту.</summary>
        public bool PlaceRequest(InboxItem item, IReadOnlyList<CassetteSlot> cassettes)
        {
            if (!CanPlace || item == null || item.Kind != InboxKind.Request || !_inbox.Contains(item)
                || cassettes == null || cassettes.Count == 0)
            {
                return false;
            }

            var tape = new TapeItem { Source = item, Duration = settings.cassetteTravel };

            foreach (var slot in cassettes)
            {
                if (slot.State != CassetteState.Ready || tape.Cassettes.Contains(slot))
                {
                    continue;
                }

                slot.State = CassetteState.OnAir;
                tape.Cassettes.Add(slot);
            }

            if (tape.Cassettes.Count == 0)
            {
                return false;
            }

            _inbox.Remove(item);
            _tape.Add(tape);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Сюжетный элемент отыгран (разговор или объявление): он уезжает на ленту.
        /// <paramref name="quick"/> — коротко, со временем в пути по умолчанию (отменённый концерт).
        /// </summary>
        public bool PlaceStory(InboxItem item, bool quick = false)
        {
            if (Phase != BroadcastPhase.Running || item == null || item.Kind != InboxKind.Story || !_inbox.Contains(item))
            {
                return false;
            }

            var travel = !quick && item.Story.Travel > 0f ? item.Story.Travel : settings.storyTravelFallback;
            _inbox.Remove(item);
            _tape.Add(new TapeItem { Source = item, Duration = travel, Quick = quick });
            Changed?.Invoke();
            return true;
        }

        public bool PlaceAd(InboxItem item)
        {
            if (!CanPlace || item == null || item.Kind != InboxKind.Ad || !_inbox.Contains(item))
            {
                return false;
            }

            var travel = item.Ad.TravelSeconds > 0f ? item.Ad.TravelSeconds : settings.adTravelFallback;
            _inbox.Remove(item);
            _tape.Add(new TapeItem { Source = item, Duration = travel });
            Changed?.Invoke();
            return true;
        }

        /// <summary>Проверка заявки. Считается один раз: повторное открытие показывает тот же исход.</summary>
        public CheckResult Evaluate(TapeItem item)
        {
            if (item.Result != null)
            {
                return item.Result;
            }

            var mode = settings.shapeMode;
            var pad = settings.shapePad;
            var normalCassettes = new List<Cassette>();
            var allCassettes = new List<Cassette>();

            foreach (var slot in item.Cassettes)
            {
                allCassettes.Add(slot.Data);

                if (!slot.Data.Strange)
                {
                    normalCassettes.Add(slot.Data);
                }
            }

            var result = new CheckResult
            {
                Request = VibeRadar.Radii(item.Source.Request.Vibes, mode, pad),
                All = VibeRadar.Radii(VibeRadar.Sum(allCassettes), mode, pad),
                Normal = normalCassettes.Count > 0 ? VibeRadar.Radii(VibeRadar.Sum(normalCassettes), mode, pad) : null,
                HasStrange = normalCassettes.Count < allCassettes.Count,
            };

            result.Chance = VibeRadar.Chance(result.Request, result.All);
            var normalChance = result.Normal != null ? VibeRadar.Chance(result.Request, result.Normal) : 0f;

            // Бросок решает исход заранее; красная часть — то, что странные кассеты
            // добавили к фигуре обычных, поэтому её доля — разница шансов.
            var roll = (float)_random.NextDouble();
            result.Outcome = roll < normalChance ? CheckOutcome.Success
                : roll < result.Chance ? CheckOutcome.StrangeSuccess
                : CheckOutcome.Fail;

            result.Path = VibeRadar.Trajectory(result.Request, result.All, result.Normal, result.Outcome, _random);
            item.Result = result;
            return result;
        }

        /// <summary>Игрок закрыл окно результата: засчитываем и отправляем кассеты на перемотку.</summary>
        public void CompleteResult(TapeItem item)
        {
            if (item == null || !_tape.Contains(item))
            {
                return;
            }

            var result = Evaluate(item);
            var request = item.Source.Request;

            if (result.Success)
            {
                Tally.Success++;
            }
            else
            {
                Tally.Failed++;
            }

            if (result.Outcome == CheckOutcome.StrangeSuccess)
            {
                Tally.Chthon++;
            }

            ApplyResultText(request.Listener, result.Success ? request.SuccessResult : request.FailResult);

            foreach (var slot in item.Cassettes)
            {
                slot.State = CassetteState.Rewinding;
                slot.RewindLeft = settings.rewindSeconds;
            }

            _tape.Remove(item);
            Changed?.Invoke();
        }

        /// <summary>
        /// «+1 «Гараж»; +1 балл эфира» → баллы в счёт, остальное в заметки сводки.
        /// Со шкалами сюжета пока не связано (GDD 02.22, «связать результаты со шкалами»).
        /// </summary>
        private void ApplyResultText(string who, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            foreach (var raw in text.Split(';'))
            {
                var part = raw.Trim();

                if (part.Length == 0)
                {
                    continue;
                }

                var match = PointsRegex.Match(part);

                if (match.Success)
                {
                    var number = match.Groups[1].Value.Replace("−", "-").Replace(" ", string.Empty);

                    if (int.TryParse(number, out var points))
                    {
                        Tally.Points += points;
                    }

                    continue;
                }

                Tally.Notes.Add($"{who}: {part}");
            }
        }

        /// <summary>Сводка закрыта: эфир окончен.</summary>
        public void Finish()
        {
            if (Phase == BroadcastPhase.Idle)
            {
                return;
            }

            Phase = BroadcastPhase.Idle;
            Paused = false;
            Finished?.Invoke(Tally);
        }

        /// <summary>Досрочный выход (отладка): без итогов и без флага эфира.</summary>
        public void Abort()
        {
            Phase = BroadcastPhase.Idle;
            _inbox.Clear();
            _tape.Clear();
        }

        /// <summary>
        /// Отладка: закончить эфир сразу, как пройденный, — с теми итогами, что набраны.
        /// Входящие и лента сбрасываются: несработавшие сюжетные элементы (звонки,
        /// объявления) пропадают, их флаги не ставятся.
        /// </summary>
        public void DebugFinishNow()
        {
            if (Phase == BroadcastPhase.Idle)
            {
                return;
            }

            _inbox.Clear();
            _tape.Clear();
            _pendingStory.Clear();
            Tally.Notes.Add("Отладка: эфир пропущен.");
            Finish();
        }

        /// <summary>Отладка: перемотать время эфира к концу.</summary>
        public void DebugSkipToEnd()
        {
            if (Phase == BroadcastPhase.Running)
            {
                Elapsed = Duration;
            }
        }
    }
}
