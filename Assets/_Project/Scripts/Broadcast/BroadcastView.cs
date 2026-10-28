using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Radio.Broadcast
{
    /// <summary>
    /// Экран эфира за столом (GDD 02.22, набросок Docs/BroadcastUI_Sketch.png): слева
    /// кассеты, в центре лента, справа входящие; поверх — окна заявки, рекламы,
    /// результата и итогов. Правила — в <see cref="BroadcastGame"/>, здесь только картинка
    /// и клики.
    /// </summary>
    /// <remarks>
    /// Пока открыто любое окно, эфир на паузе (<see cref="BroadcastGame.Paused"/>):
    /// за чтение не наказываем. Окно заявки не закрывает колонку кассет — по ней и
    /// кликают, чтобы положить кассету в слот.
    /// </remarks>
    [RequireComponent(typeof(BroadcastGame))]
    public sealed class BroadcastView : MonoBehaviour
    {
        // Раскладка в пикселях холста 1920×1080.
        private const float ColumnTop = 150f;
        private const float CassetteX = 40f;
        private const float CassetteWidth = 380f;
        private const float CassetteHeight = 96f;
        private const float TapeX = 460f;
        private const float TapeWidth = 980f;
        private const float TapeY = 250f;
        private const float TapeHeight = 190f;
        private const float InboxX = 1480f;

        // Правая колонка ниже остальных: над ней список задач, который растёт вниз.
        private const float InboxTop = 270f;
        private const float InboxWidth = 400f;
        private const float InboxHeight = 96f;
        private const float WindowX = 460f;
        private const float WindowY = 110f;
        private const float WindowWidth = 1420f;
        private const float WindowHeight = 930f;

        [Tooltip("Корень, куда строится экран. Пусто — сам объект.")]
        [SerializeField] private RectTransform root;

        private BroadcastGame _game;
        private RectTransform _screen;
        private TextMeshProUGUI _clock;
        private TextMeshProUGUI _remaining;
        private Image _progress;
        private TextMeshProUGUI _tapeStatus;
        private RectTransform _tapeArea;
        private RectTransform _cassetteArea;
        private RectTransform _inboxArea;
        private TextMeshProUGUI _inboxEmpty;

        private readonly Dictionary<CassetteSlot, CassetteCard> _cassetteCards = new Dictionary<CassetteSlot, CassetteCard>();
        private readonly Dictionary<InboxItem, InboxCard> _inboxCards = new Dictionary<InboxItem, InboxCard>();
        private readonly Dictionary<TapeItem, TapeCard> _tapeCards = new Dictionary<TapeItem, TapeCard>();

        // Окно заявки / рекламы.
        private GameObject _itemWindow;
        private TextMeshProUGUI _itemTitle;
        private TextMeshProUGUI _itemBody;
        private TextMeshProUGUI _itemHint;
        private RectTransform _slotRow;
        private Button _placeButton;
        private readonly List<SlotView> _slots = new List<SlotView>();
        private InboxItem _openItem;
        private readonly List<CassetteSlot> _selection = new List<CassetteSlot>();

        private ResultWindow _result;

        // Итоги.
        private GameObject _summary;
        private TextMeshProUGUI _summaryText;

        private bool _built;
        private bool _wasActive;

        // Сюжетный элемент (звонок, объявление) идёт обычным диалогом: экран эфира на это
        // время прячется, эфир стоит. После разговора элемент уезжает на ленту — если
        // узел не сказал <<broadcast_skip>> («не ставить»).
        private Radio.Dialogue.DialogueController _dialogue;
        private InboxItem _storyItem;
        private bool _storySkip;
        private bool _eventRunning;
        private bool _storyQuick;
        private float _statusFlashUntil;

        private sealed class CassetteCard
        {
            public RectTransform Rect;
            public Image Background;
            public Button Button;
            public TextMeshProUGUI Title;
            public TextMeshProUGUI Status;
            public Image Timer;
            public CanvasGroup Group;
        }

        private sealed class InboxCard
        {
            public RectTransform Rect;
            public Image Timer;
        }

        private sealed class TapeCard
        {
            public RectTransform Rect;
            public Image Background;
            public CanvasGroup Group;
        }

        private sealed class SlotView
        {
            public Image Background;
            public TextMeshProUGUI Label;
        }

        private bool AnyWindowOpen =>
            (_itemWindow != null && _itemWindow.activeSelf) || (_result != null && _result.IsOpen)
            || (_summary != null && _summary.activeSelf) || _storyItem != null || _eventRunning;

        private void Awake()
        {
            _game = GetComponent<BroadcastGame>();

            if (root == null)
            {
                root = (RectTransform)transform;
            }

            _game.Changed += Refresh;
            _game.StoryEventFired += StartStoryEvent;
        }

        private void Start()
        {
            _dialogue = FindAnyObjectByType<Radio.Dialogue.DialogueController>();
            var runner = FindAnyObjectByType<Yarn.Unity.DialogueRunner>();

            if (runner != null)
            {
                // «Не ставить»: элемент остаётся во входящих и сгорит по своему таймеру.
                runner.AddCommandHandler("broadcast_skip", (System.Action)(() => _storySkip = true));

                // «Коротко»: элемент уезжает по ленте быстро — отменённый концерт не держит заявки.
                runner.AddCommandHandler("broadcast_quick", (System.Action)(() => _storyQuick = true));
            }
        }

        private void OnDestroy()
        {
            if (_game != null)
            {
                _game.Changed -= Refresh;
                _game.StoryEventFired -= StartStoryEvent;
            }
        }

        private void Build()
        {
            if (_built)
            {
                return;
            }

            _built = true;
            BroadcastUi.Font = _game.Settings.font;

            var background = BroadcastUi.Box(root, "BroadcastScreen", BroadcastUi.Background);
            _screen = BroadcastUi.Stretch(background.rectTransform);

            var title = BroadcastUi.Label(_screen, "Title", "ЭФИР · Радио 103 FM", 40f, BroadcastUi.Amber);
            BroadcastUi.Place(title.rectTransform, 40f, 30f, 700f, 60f);

            // Правый верхний угол занят списком задач (он виден и в мини-играх),
            // поэтому часы и остаток эфира — в середине верхней строки.
            _clock = BroadcastUi.Label(_screen, "Clock", "00:00", 44f, BroadcastUi.Text, TextAlignmentOptions.TopRight);
            BroadcastUi.Place(_clock.rectTransform, 640f, 26f, 400f, 60f);

            _remaining = BroadcastUi.Label(_screen, "Remaining", string.Empty, 30f, BroadcastUi.Muted);
            BroadcastUi.Place(_remaining.rectTransform, 1080f, 36f, 340f, 50f);

            var bar = BroadcastUi.Box(_screen, "ProgressBack", new Color(1f, 1f, 1f, 0.08f));
            BroadcastUi.Place(bar.rectTransform, 40f, 96f, 1840f, 8f);
            _progress = BroadcastUi.Box(bar.transform, "Progress", BroadcastUi.Amber);
            _progress.rectTransform.anchorMin = Vector2.zero;
            _progress.rectTransform.anchorMax = new Vector2(0f, 1f);
            _progress.rectTransform.offsetMin = _progress.rectTransform.offsetMax = Vector2.zero;

            Header("Кассеты", CassetteX);
            Header("Лента эфира", TapeX);
            Header("Входящие", InboxX, InboxTop - 38f);

            _cassetteArea = BroadcastUi.Place(BroadcastUi.Rect(_screen, "Cassettes"), CassetteX, ColumnTop, CassetteWidth, 880f);
            _inboxArea = BroadcastUi.Place(BroadcastUi.Rect(_screen, "Inbox"), InboxX, InboxTop, InboxWidth, 760f);
            _inboxEmpty = BroadcastUi.Label(_inboxArea, "Empty", "Пока тихо…", 26f, BroadcastUi.Muted);
            BroadcastUi.Place(_inboxEmpty.rectTransform, 0f, 10f, InboxWidth, 40f);

            var tape = BroadcastUi.Box(_screen, "Tape", BroadcastUi.Panel);
            BroadcastUi.Border(tape, new Color(1f, 1f, 1f, 0.15f));
            _tapeArea = BroadcastUi.Place(tape.rectTransform, TapeX, TapeY, TapeWidth, TapeHeight);

            // Стрелка направления: лента едет слева направо, доехавшее ждёт у правого края.
            var arrow = BroadcastUi.Label(_tapeArea, "Direction", "→   →   →   →   →   →   →   →   →   →", 28f,
                new Color(1f, 1f, 1f, 0.07f), TextAlignmentOptions.Center);
            BroadcastUi.Stretch(arrow.rectTransform);

            _tapeStatus = BroadcastUi.Label(_screen, "TapeStatus", string.Empty, 26f, BroadcastUi.AdBlue, TextAlignmentOptions.Center);
            BroadcastUi.Place(_tapeStatus.rectTransform, TapeX, TapeY + TapeHeight + 16f, TapeWidth, 40f);

            var perks = BroadcastUi.Box(_screen, "Perks", new Color(1f, 1f, 1f, 0.03f));
            BroadcastUi.Border(perks, new Color(1f, 1f, 1f, 0.12f), 1f);
            BroadcastUi.Place(perks.rectTransform, TapeX + 120f, TapeY + TapeHeight + 80f, TapeWidth - 240f, 90f);
            var perksLabel = BroadcastUi.Label(perks.transform, "Label", "Набор перков — позже", 24f, BroadcastUi.Muted, TextAlignmentOptions.Center);
            BroadcastUi.Stretch(perksLabel.rectTransform);

            var help = BroadcastUi.Label(_screen, "Help",
                "Кликните заявку справа, положите в неё кассеты слева и нажмите «Поставить».\n" +
                "Доехавшую кассету кликните, чтобы узнать результат. Пока открыто окно, эфир стоит.",
                22f, BroadcastUi.Muted, TextAlignmentOptions.Center);
            BroadcastUi.Place(help.rectTransform, TapeX, 720f, TapeWidth, 90f);

            BuildItemWindow();
            _result = new ResultWindow(BroadcastUi.Place(BroadcastUi.Rect(_screen, "ResultArea"), WindowX, WindowY, WindowWidth, WindowHeight));
            BuildSummary();

            _screen.gameObject.SetActive(false);
        }

        private void Header(string text, float x, float y = 112f)
        {
            var label = BroadcastUi.Label(_screen, "Header " + text, text.ToUpperInvariant(), 26f, BroadcastUi.Muted);
            BroadcastUi.Place(label.rectTransform, x, y, 400f, 36f);
        }

        private void BuildItemWindow()
        {
            var panel = BroadcastUi.Box(_screen, "ItemWindow", BroadcastUi.Panel);
            BroadcastUi.Border(panel, BroadcastUi.Amber);
            var rect = BroadcastUi.Place(panel.rectTransform, WindowX, WindowY, WindowWidth, WindowHeight);
            _itemWindow = panel.gameObject;

            _itemTitle = BroadcastUi.Label(rect, "Title", string.Empty, 40f, BroadcastUi.Text);
            BroadcastUi.Place(_itemTitle.rectTransform, 50f, 40f, WindowWidth - 100f, 60f);

            _itemBody = BroadcastUi.Label(rect, "Body", string.Empty, 32f, BroadcastUi.Text);
            _itemBody.overflowMode = TextOverflowModes.Overflow;
            BroadcastUi.Place(_itemBody.rectTransform, 50f, 120f, WindowWidth - 100f, 420f);

            _itemHint = BroadcastUi.Label(rect, "Hint", string.Empty, 26f, BroadcastUi.Muted);
            BroadcastUi.Place(_itemHint.rectTransform, 50f, 560f, WindowWidth - 100f, 40f);

            _slotRow = BroadcastUi.Place(BroadcastUi.Rect(rect, "Slots"), 50f, 610f, WindowWidth - 100f, 110f);

            _placeButton = BroadcastUi.Button(rect, "Place", "Поставить", PlaceOpenItem, 34f);
            BroadcastUi.Place((RectTransform)_placeButton.transform, 50f, 790f, 320f, 80f);

            var close = BroadcastUi.Button(rect, "Close", "Закрыть", CloseItemWindow, 34f);
            BroadcastUi.Place((RectTransform)close.transform, 400f, 790f, 260f, 80f);

            _itemWindow.SetActive(false);
        }

        private void BuildSummary()
        {
            var panel = BroadcastUi.Box(_screen, "Summary", BroadcastUi.Panel);
            BroadcastUi.Border(panel, BroadcastUi.Amber);
            var rect = BroadcastUi.Place(panel.rectTransform, 360f, 90f, 1200f, 900f);
            _summary = panel.gameObject;

            var title = BroadcastUi.Label(rect, "Title", "Итоги эфира", 46f, BroadcastUi.Amber);
            BroadcastUi.Place(title.rectTransform, 50f, 40f, 1100f, 70f);

            _summaryText = BroadcastUi.Label(rect, "Text", string.Empty, 30f, BroadcastUi.Text);
            _summaryText.overflowMode = TextOverflowModes.Truncate;
            BroadcastUi.Place(_summaryText.rectTransform, 50f, 130f, 1100f, 620f);

            var finish = BroadcastUi.Button(rect, "Finish", "Закончить эфир", () => _game.Finish(), 34f);
            BroadcastUi.Place((RectTransform)finish.transform, 50f, 780f, 400f, 80f);

            _summary.SetActive(false);
        }

        private void Update()
        {
            var active = _game.Phase != BroadcastPhase.Idle;

            if (active && !_built)
            {
                Build();
            }

            if (!_built)
            {
                return;
            }

            if (active != _wasActive)
            {
                _wasActive = active;

                if (active)
                {
                    ResetWindows();
                    Refresh();
                }
            }

            var visible = active && _storyItem == null && !_eventRunning;

            if (_screen.gameObject.activeSelf != visible)
            {
                _screen.gameObject.SetActive(visible);
            }

            if (!active)
            {
                return;
            }

            DebugKeys();

            _result.Tick(Time.unscaledDeltaTime);
            UpdateWrapUp();
            _game.Paused = AnyWindowOpen;
            UpdateTimers();
        }

        private void ResetWindows()
        {
            _itemWindow.SetActive(false);
            _summary.SetActive(false);
            _openItem = null;
            _selection.Clear();
        }

        /// <summary>
        /// Конец эфира: непросмотренные результаты открываются по очереди, затем итоги.
        /// </summary>
        private void UpdateWrapUp()
        {
            if (AnyWindowOpen)
            {
                return;
            }

            if (_game.Phase == BroadcastPhase.Summary)
            {
                ShowSummary();
                return;
            }

            if (_game.Phase != BroadcastPhase.Wrapping)
            {
                return;
            }

            foreach (var item in _game.Tape)
            {
                if (!item.AutoComplete && item.Arrived)
                {
                    OpenResult(item);
                    return;
                }
            }
        }

        private void DebugKeys()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Отладка теста: F9 — сразу к концу эфира, чтобы проверить итоги.
            if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
            {
                _game.DebugSkipToEnd();
            }
#endif
        }

        private void UpdateTimers()
        {
            _clock.text = $"<size=60%><color=#9E9E94>Часы эфира</color></size>  {BroadcastUi.Clock(_game.ClockMinutes)}";
            var left = Mathf.Max(0f, _game.Duration - _game.Elapsed);
            _remaining.text = _game.Phase == BroadcastPhase.Running
                ? $"Осталось {Mathf.FloorToInt(left / 60f)}:{Mathf.FloorToInt(left % 60f):00}" + (_game.Paused ? "  · пауза" : string.Empty)
                : "Эфир окончен";
            _progress.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(_game.Elapsed / Mathf.Max(1f, _game.Duration)), 1f);

            foreach (var pair in _inboxCards)
            {
                BroadcastUi.SetTimer(pair.Value.Timer, pair.Key.Progress);
            }

            foreach (var pair in _cassetteCards)
            {
                var slot = pair.Key;
                var card = pair.Value;
                card.Timer.transform.parent.gameObject.SetActive(slot.State == CassetteState.Rewinding);

                if (slot.State == CassetteState.Rewinding)
                {
                    BroadcastUi.SetTimer(card.Timer, Mathf.Clamp01(slot.RewindLeft / Mathf.Max(0.01f, _game.Settings.rewindSeconds)));
                }
            }

            var blink = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
            var arrivedIndex = 0;

            foreach (var item in _game.Tape)
            {
                if (!_tapeCards.TryGetValue(item, out var card))
                {
                    continue;
                }

                const float cardWidth = 210f;
                var travel = TapeWidth - cardWidth - 30f;
                var x = 15f + travel * item.Progress;
                var y = 20f;

                if (item.Arrived)
                {
                    // Доехавшие ждут у правого края стопкой.
                    y = 20f + (arrivedIndex % 2) * 80f;
                    x -= (arrivedIndex / 2) * 30f;
                    arrivedIndex++;
                    card.Group.alpha = blink;
                }
                else
                {
                    y = 20f + (_game.Tape.Count > 1 && IndexOf(item) % 2 == 1 ? 80f : 0f);
                    card.Group.alpha = 1f;
                }

                card.Rect.anchoredPosition = new Vector2(x, -y);
            }

            if (_itemWindow.activeSelf)
            {
                UpdateItemWindowState();
            }
        }

        private int IndexOf(TapeItem item)
        {
            for (var i = 0; i < _game.Tape.Count; i++)
            {
                if (_game.Tape[i] == item)
                {
                    return i;
                }
            }

            return 0;
        }

        // ------------------------------------------------------------------ Списки

        private void Refresh()
        {
            if (!_built)
            {
                return;
            }

            RefreshCassettes();
            RefreshInbox();
            RefreshTape();

            if (_openItem != null && !Contains(_game.Inbox, _openItem))
            {
                // Пока окно открыто, таймеры стоят, так что исчезнуть элемент мог только
                // в конце эфира — тогда окно просто закрываем.
                CloseItemWindow();
            }

            if (Time.unscaledTime < _statusFlashUntil)
            {
                return;
            }

            _tapeStatus.text = _game.AdOnAir ? "Идёт реклама — пока она в эфире, ставить ничего нельзя"
                : _game.Phase == BroadcastPhase.Wrapping ? "Время вышло: доезжает то, что уже в эфире"
                : string.Empty;
        }

        private static bool Contains(IReadOnlyList<InboxItem> list, InboxItem item)
        {
            foreach (var entry in list)
            {
                if (entry == item)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshCassettes()
        {
            foreach (var slot in _game.Cassettes)
            {
                if (!_cassetteCards.ContainsKey(slot))
                {
                    _cassetteCards[slot] = MakeCassetteCard(slot);
                }
            }

            var stale = new List<CassetteSlot>();
            foreach (var pair in _cassetteCards)
            {
                if (!ContainsSlot(pair.Key))
                {
                    Destroy(pair.Value.Rect.gameObject);
                    stale.Add(pair.Key);
                }
            }

            foreach (var slot in stale)
            {
                _cassetteCards.Remove(slot);
            }

            // Перематывающиеся — вниз колонки, остальные в исходном порядке.
            var ordered = new List<CassetteSlot>(_game.Cassettes);
            ordered.Sort((a, b) =>
            {
                var ra = a.State == CassetteState.Rewinding ? 1 : 0;
                var rb = b.State == CassetteState.Rewinding ? 1 : 0;
                return ra != rb ? ra.CompareTo(rb) : a.Order.CompareTo(b.Order);
            });

            for (var i = 0; i < ordered.Count; i++)
            {
                var slot = ordered[i];
                var card = _cassetteCards[slot];
                card.Rect.anchoredPosition = new Vector2(0f, -i * (CassetteHeight + 12f));

                var selected = _selection.Contains(slot);
                var usable = slot.State == CassetteState.Ready;
                card.Group.alpha = usable ? 1f : 0.45f;
                card.Button.interactable = usable;
                card.Background.color = selected ? new Color(0.33f, 0.26f, 0.1f, 1f) : BroadcastUi.Card;
                card.Status.text = slot.State switch
                {
                    CassetteState.OnAir => "в эфире",
                    CassetteState.Rewinding => "перемотка",
                    _ => selected ? "в слоте" : BroadcastUi.VibeLine(slot.Data.Vibes),
                };
            }
        }

        private bool ContainsSlot(CassetteSlot slot)
        {
            foreach (var entry in _game.Cassettes)
            {
                if (entry == slot)
                {
                    return true;
                }
            }

            return false;
        }

        private CassetteCard MakeCassetteCard(CassetteSlot slot)
        {
            var background = BroadcastUi.Box(_cassetteArea, "Cassette " + slot.Data.Id, BroadcastUi.Card);
            BroadcastUi.Border(background, slot.Data.Strange ? BroadcastUi.Red : BroadcastUi.Amber);
            var rect = BroadcastUi.Place(background.rectTransform, 0f, 0f, CassetteWidth, CassetteHeight);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => ToggleCassette(slot));

            var title = BroadcastUi.Label(rect, "Title", $"<b>{slot.Data.Id}</b>  {slot.Data.Title}", 26f, BroadcastUi.Text);
            BroadcastUi.Place(title.rectTransform, 16f, 10f, CassetteWidth - 100f, 40f);

            var status = BroadcastUi.Label(rect, "Status", string.Empty, 22f, BroadcastUi.Muted);
            BroadcastUi.Place(status.rectTransform, 16f, 52f, CassetteWidth - 100f, 34f);

            var timer = BroadcastUi.RadialTimer(rect, "Rewind");
            BroadcastUi.Place((RectTransform)timer.transform.parent, CassetteWidth - 76f, 18f, 60f, 60f);
            timer.transform.parent.gameObject.SetActive(false);

            return new CassetteCard
            {
                Rect = rect,
                Background = background,
                Button = button,
                Title = title,
                Status = status,
                Timer = timer,
                Group = background.gameObject.AddComponent<CanvasGroup>(),
            };
        }

        private void RefreshInbox()
        {
            var stale = new List<InboxItem>();
            foreach (var pair in _inboxCards)
            {
                if (!Contains(_game.Inbox, pair.Key))
                {
                    Destroy(pair.Value.Rect.gameObject);
                    stale.Add(pair.Key);
                }
            }

            foreach (var item in stale)
            {
                _inboxCards.Remove(item);
            }

            for (var i = 0; i < _game.Inbox.Count; i++)
            {
                var item = _game.Inbox[i];

                if (!_inboxCards.TryGetValue(item, out var card))
                {
                    card = MakeInboxCard(item);
                    _inboxCards[item] = card;
                }

                card.Rect.anchoredPosition = new Vector2(0f, -i * (InboxHeight + 12f));
            }

            _inboxEmpty.gameObject.SetActive(_game.Inbox.Count == 0);
        }

        private InboxCard MakeInboxCard(InboxItem item)
        {
            var isAd = item.Kind == InboxKind.Ad;
            var isStory = item.Kind == InboxKind.Story;
            var background = BroadcastUi.Box(_inboxArea, "Inbox", BroadcastUi.Card);
            BroadcastUi.Border(background, isAd ? BroadcastUi.AdBlue : isStory ? BroadcastUi.Amber : new Color(1f, 1f, 1f, 0.2f));
            var rect = BroadcastUi.Place(background.rectTransform, 0f, 0f, InboxWidth, InboxHeight);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => OpenItem(item));

            var timer = BroadcastUi.RadialTimer(rect, "Timer");
            BroadcastUi.Place((RectTransform)timer.transform.parent, 16f, 18f, 60f, 60f);

            var kindText = isAd ? "Реклама"
                : !isStory ? "Заявка"
                : KindLabel(item.Story.Kind, true);
            var kind = BroadcastUi.Label(rect, "Kind", kindText, 20f,
                isAd ? BroadcastUi.AdBlue : isStory ? BroadcastUi.Amber : BroadcastUi.Muted);
            BroadcastUi.Place(kind.rectTransform, 92f, 12f, InboxWidth - 100f, 30f);

            var name = isAd ? item.Ad.Advertiser : isStory ? item.Story.Title : item.Request.Listener;
            var title = BroadcastUi.Label(rect, "Title", name, 26f, BroadcastUi.Text);
            BroadcastUi.Place(title.rectTransform, 92f, 42f, InboxWidth - 100f, 40f);

            return new InboxCard { Rect = rect, Timer = timer };
        }

        private void RefreshTape()
        {
            var stale = new List<TapeItem>();
            foreach (var pair in _tapeCards)
            {
                if (!ContainsTape(pair.Key))
                {
                    Destroy(pair.Value.Rect.gameObject);
                    stale.Add(pair.Key);
                }
            }

            foreach (var item in stale)
            {
                _tapeCards.Remove(item);
            }

            foreach (var item in _game.Tape)
            {
                if (!_tapeCards.ContainsKey(item))
                {
                    _tapeCards[item] = MakeTapeCard(item);
                }
            }
        }

        private bool ContainsTape(TapeItem item)
        {
            foreach (var entry in _game.Tape)
            {
                if (entry == item)
                {
                    return true;
                }
            }

            return false;
        }

        private TapeCard MakeTapeCard(TapeItem item)
        {
            var background = BroadcastUi.Box(_tapeArea, "TapeItem", BroadcastUi.Card);
            var strange = item.Cassettes.Exists(c => c.Data.Strange);
            BroadcastUi.Border(background, item.IsAd ? BroadcastUi.AdBlue : strange ? BroadcastUi.Red : BroadcastUi.Amber);
            var rect = BroadcastUi.Place(background.rectTransform, 0f, 0f, 210f, 70f);

            string text;

            if (item.IsAd)
            {
                text = $"<color=#5999E5>Реклама</color>\n{item.Source.Ad.Advertiser}";
            }
            else if (item.Source.Kind == InboxKind.Story)
            {
                var story = item.Source.Story;
                var kind = KindLabel(story.Kind, false);
                text = $"<color=#F2A82E>{kind}</color>\n{story.Title}";
            }
            else
            {
                var ids = new List<string>();
                foreach (var slot in item.Cassettes)
                {
                    ids.Add(slot.Data.Id);
                }

                text = $"<b>{string.Join(" + ", ids)}</b>\n{item.Source.Request.Listener}";
                var button = background.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.onClick.AddListener(() =>
                {
                    if (item.Arrived && !AnyWindowOpen)
                    {
                        OpenResult(item);
                    }
                });
            }

            var label = BroadcastUi.Label(rect, "Label", text, 20f, BroadcastUi.Text);
            BroadcastUi.Stretch(label.rectTransform, 10f, 6f, 10f, 6f);

            return new TapeCard
            {
                Rect = rect,
                Background = background,
                Group = background.gameObject.AddComponent<CanvasGroup>(),
            };
        }

        // ------------------------------------------------------------------ Окно заявки

        private void OpenItem(InboxItem item)
        {
            if (AnyWindowOpen || !Contains(_game.Inbox, item))
            {
                return;
            }

            if (item.Kind == InboxKind.Story)
            {
                StartStory(item);
                return;
            }

            _openItem = item;
            _selection.Clear();

            foreach (Transform child in _slotRow)
            {
                Destroy(child.gameObject);
            }

            _slots.Clear();

            if (item.Kind == InboxKind.Request)
            {
                var request = item.Request;
                var faction = string.IsNullOrWhiteSpace(request.Faction) || request.Faction == "Нет"
                    ? string.Empty
                    : $"  <size=70%><color=#9E9E94>· {request.Faction}</color></size>";
                _itemTitle.text = $"Заявка · {request.Listener}{faction}";
                _itemBody.text = request.Text;

                for (var i = 0; i < item.Slots; i++)
                {
                    var index = i;
                    var image = BroadcastUi.Box(_slotRow, "Slot" + i, new Color(1f, 1f, 1f, 0.05f));
                    BroadcastUi.Border(image, new Color(1f, 1f, 1f, 0.25f));
                    BroadcastUi.Place(image.rectTransform, i * 330f, 0f, 310f, 100f);
                    var button = image.gameObject.AddComponent<Button>();
                    button.targetGraphic = image;
                    button.onClick.AddListener(() => RemoveFromSlot(index));
                    var label = BroadcastUi.Label(image.transform, "Label", string.Empty, 24f, BroadcastUi.Text, TextAlignmentOptions.Center);
                    BroadcastUi.Stretch(label.rectTransform, 10f, 8f, 10f, 8f);
                    _slots.Add(new SlotView { Background = image, Label = label });
                }
            }
            else
            {
                var ad = item.Ad;
                _itemTitle.text = $"Реклама · {ad.Advertiser}";
                var consequence = string.IsNullOrWhiteSpace(ad.Consequence) || ad.Consequence.Trim() == "—"
                    ? string.Empty
                    : $"\n<color=#9E9E94><size=80%>{ad.Consequence}</size></color>";
                _itemBody.text = $"«{ad.Text}»\n\n<color=#F2A82E>Оплата: {ad.Money}</color>{consequence}";
            }

            _itemWindow.SetActive(true);
            _itemWindow.transform.SetAsLastSibling();
            UpdateItemWindowState();
            RefreshCassettes();
        }

        private void ToggleCassette(CassetteSlot slot)
        {
            if (!_itemWindow.activeSelf || _openItem == null || _openItem.Kind != InboxKind.Request
                || slot.State != CassetteState.Ready)
            {
                return;
            }

            if (_selection.Contains(slot))
            {
                _selection.Remove(slot);
            }
            else if (_selection.Count < _openItem.Slots)
            {
                _selection.Add(slot);
            }

            UpdateItemWindowState();
            RefreshCassettes();
        }

        private void RemoveFromSlot(int index)
        {
            if (index < _selection.Count)
            {
                _selection.RemoveAt(index);
                UpdateItemWindowState();
                RefreshCassettes();
            }
        }

        private void UpdateItemWindowState()
        {
            if (_openItem == null)
            {
                return;
            }

            for (var i = 0; i < _slots.Count; i++)
            {
                var filled = i < _selection.Count;
                var cassette = filled ? _selection[i].Data : null;
                _slots[i].Label.text = filled
                    ? $"<b>{cassette.Id}</b>\n<size=80%>{cassette.Title}</size>"
                    : "<color=#9E9E94>пустой слот</color>";
                BroadcastUi.Border(_slots[i].Background,
                    filled ? (cassette.Strange ? BroadcastUi.Red : BroadcastUi.Amber) : new Color(1f, 1f, 1f, 0.25f));
            }

            var isRequest = _openItem.Kind == InboxKind.Request;
            var ready = isRequest ? _selection.Count > 0 : true;
            _placeButton.interactable = _game.CanPlace && ready;

            _itemHint.text = _game.AdOnAir ? "<color=#5999E5>Идёт реклама — поставить пока нельзя.</color>"
                : isRequest ? $"Кассет можно поставить: до {_openItem.Slots}. Кликните кассету слева, чтобы положить её в слот; клик по слоту — убрать."
                : "Пока реклама едет по ленте, ничего другого поставить нельзя.";
        }

        private void PlaceOpenItem()
        {
            if (_openItem == null)
            {
                return;
            }

            var placed = _openItem.Kind == InboxKind.Request
                ? _game.PlaceRequest(_openItem, _selection)
                : _game.PlaceAd(_openItem);

            if (placed)
            {
                CloseItemWindow();
            }
        }

        private void CloseItemWindow()
        {
            _itemWindow.SetActive(false);
            _openItem = null;
            _selection.Clear();
            RefreshCassettes();
        }

        // ------------------------------------------------------------------ Сюжетные элементы

        private void StartStory(InboxItem item)
        {
            if (!_game.CanPlace)
            {
                Flash("Идёт реклама — " + (item.Story.Kind == StoryElementKind.Call ? "звонок подождёт" : "объявление подождёт"));
                return;
            }

            if (_dialogue == null)
            {
                _dialogue = FindAnyObjectByType<Radio.Dialogue.DialogueController>();
            }

            if (_dialogue == null)
            {
                // Без диалоговой системы разговор не показать — просто ставим элемент.
                _game.PlaceStory(item);
                return;
            }

            _storyItem = item;
            _storySkip = false;
            _storyQuick = false;
            _game.Paused = true;
            _dialogue.DialogueFinished += OnStoryFinished;
            _dialogue.StartDialogue(item.Story.Node);
        }

        private static string KindLabel(StoryElementKind kind, bool full) => kind switch
        {
            StoryElementKind.Call => full ? "Звонок в эфир" : "Звонок",
            StoryElementKind.Concert => "Концерт",
            StoryElementKind.Event => "Событие",
            _ => "Объявление",
        };

        /// <summary>
        /// Событие посреди эфира (налёт): разговор начинается сам, экран эфира прячется,
        /// эфир на паузе — как при сюжетном элементе, только без входящих и ленты.
        /// </summary>
        private void StartStoryEvent(StoryElement element)
        {
            if (_dialogue == null)
            {
                _dialogue = FindAnyObjectByType<Radio.Dialogue.DialogueController>();
            }

            if (_dialogue == null || _dialogue.IsRunning)
            {
                return;
            }

            _eventRunning = true;
            _game.Paused = true;
            _dialogue.DialogueFinished += OnStoryEventFinished;
            _dialogue.StartDialogue(element.Node);
        }

        private void OnStoryEventFinished()
        {
            _dialogue.DialogueFinished -= OnStoryEventFinished;
            _eventRunning = false;
            Refresh();
        }

        private void OnStoryFinished()
        {
            _dialogue.DialogueFinished -= OnStoryFinished;
            var item = _storyItem;
            _storyItem = null;

            if (!_storySkip)
            {
                _game.PlaceStory(item, _storyQuick);
            }

            Refresh();
        }

        private void Flash(string text)
        {
            _tapeStatus.text = text;
            _statusFlashUntil = Time.unscaledTime + 2.5f;
        }

        // ------------------------------------------------------------------ Результат и итоги

        private void OpenResult(TapeItem item)
        {
            var result = _game.Evaluate(item);
            _result.Open(item, result, _game.Settings.radarScale, () => _game.CompleteResult(item));
        }

        private void ShowSummary()
        {
            var tally = _game.Tally;
            var text = new StringBuilder();
            text.AppendLine($"<color=#73CC66>Подошло:</color> {tally.Success}");
            text.AppendLine($"<color=#E5403A>Не подошло:</color> {tally.Failed}");
            text.AppendLine($"<color=#E5403A>Слушатель не дождался:</color> {tally.Expired}");
            text.AppendLine($"Реклама не поставлена: {tally.Missed}");
            text.AppendLine();
            text.AppendLine($"<color=#F2A82E>Баллы эфира: {tally.Points}</color>");
            text.AppendLine($"<color=#F2A82E>Деньги за рекламу: {tally.Money}</color>");

            if (tally.Chthon > 0)
            {
                text.AppendLine($"<color=#E5403A>Хтонь: +{tally.Chthon}</color>");
            }

            if (tally.Perfect)
            {
                text.AppendLine();
                text.AppendLine("<color=#73CC66><b>Идеальный эфир!</b></color>");
            }

            if (tally.Notes.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("<size=80%><color=#9E9E94>Последствия (в тесте только сводка, со шкалами пока не связаны):</color>");

                foreach (var note in tally.Notes)
                {
                    text.AppendLine($"· {note}");
                }

                text.Append("</size>");
            }

            _summaryText.text = text.ToString();
            _summary.SetActive(true);
            _summary.transform.SetAsLastSibling();
        }
    }
}
