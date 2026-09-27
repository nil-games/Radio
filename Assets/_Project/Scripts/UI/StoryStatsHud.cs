using System.Collections.Generic;
using System.Text;
using Radio.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Radio.UI
{
    /// <summary>
    /// «Это будет иметь последствия» под списком задач, когда меняется параметр сюжета,
    /// и отладочная сводка всех параметров по правому Ctrl в левом верхнем углу.
    /// </summary>
    /// <remarks>
    /// Надпись срабатывает на настоящее изменение, а не на каждую запись: прибавка к уже
    /// максимальному значению прижимается к краю и ничего не меняет — сообщать игроку
    /// о последствиях, которых нет, было бы враньём.
    /// Сводка есть только в редакторе и отладочных сборках: игроку цифры видеть незачем.
    /// </remarks>
    public sealed class StoryStatsHud : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Если пусто, берётся из GameSession в сцене.")]
        [SerializeField] private WorldState world;

        [Tooltip("Часы смены для отладочной сводки. Если пусто, берутся из GameSession в сцене.")]
        [SerializeField] private TimeManager time;

        [Tooltip("Шрифт подписей. Обязан содержать кириллицу.")]
        [SerializeField] private TMP_FontAsset font;

        [Tooltip("Список задач: надпись о последствиях встаёт прямо под ним. " +
                 "Если пусто, ищется в сцене; не нашёлся — надпись в правом верхнем углу.")]
        [SerializeField] private TaskHud taskHud;

        [Header("Последствия")]
        [SerializeField] private string message = "Это будет иметь последствия";

        [Tooltip("Сколько секунд надпись держится после последнего изменения.")]
        [SerializeField] private float holdTime = 3f;

        [SerializeField] private float fadeTime = 0.35f;

        [SerializeField] private Color messageColor = Color.black;

        [Tooltip("Обводка надписи: чёрный текст без неё теряется на тёмной сцене.")]
        [SerializeField] private Color messageOutlineColor = Color.white;

        [Tooltip("Толщина обводки, доля от 0 до 1 (параметр TextMeshPro).")]
        [Range(0f, 1f)]
        [SerializeField] private float messageOutlineWidth = 0.2f;

        [Tooltip("Зазор между списком задач и надписью, в единицах холста.")]
        [SerializeField] private float gapBelowTasks = 12f;

        [Header("Вид")]
        [Tooltip("Порядок холста. Выше диалога и мини-игр, как у списка задач.")]
        [SerializeField] private int sortingOrder = 50;

        [SerializeField] private Vector2 margin = new Vector2(36f, 32f);

        [SerializeField] private Color debugColor = new Color(0.6f, 1f, 0.6f, 0.95f);

        [Tooltip("Через сколько секунд после четвёртой цифры время применяется без номера ночи.")]
        [SerializeField] private float timeOnlyDelay = 1.2f;

        [Tooltip("Через сколько секунд без нажатий недобранные цифры времени сбрасываются.")]
        [SerializeField] private float timeInputTimeout = 3f;

        private readonly Dictionary<string, float> _known = new Dictionary<string, float>();
        private readonly StringBuilder _debugText = new StringBuilder();

        private CanvasGroup _messageGroup;
        private RectTransform _messageRect;
        private TextMeshProUGUI _debugLabel;
        private float _hideAt = float.NegativeInfinity;
        private bool _toggleHeld;
        private readonly StringBuilder _timeInput = new StringBuilder(4);
        private float _timeInputAt;

        private void Awake()
        {
            if (font == null)
            {
                Debug.LogError($"{nameof(StoryStatsHud)}: не задан шрифт.", this);
                enabled = false;
                return;
            }

            Build();
        }

        /// <summary>
        /// Подписка в Start, а не в Awake: мир заводит стартовые значения в своём Awake,
        /// и надпись не должна срабатывать на то, что игра просто началась.
        /// </summary>
        private void Start()
        {
            if (world == null && GameSession.Current != null)
            {
                world = GameSession.Current.World;
            }

            if (time == null && GameSession.Current != null)
            {
                time = GameSession.Current.Time;
            }

            if (taskHud == null)
            {
                taskHud = FindAnyObjectByType<TaskHud>();
            }

            if (world == null)
            {
                Debug.LogError($"{nameof(StoryStatsHud)}: нет состояния мира, параметры не видны.", this);
                enabled = false;
                return;
            }

            foreach (var stat in StoryStats.All)
            {
                _known[stat.Name] = Read(stat.Name);
            }

            world.FlagChanged += OnFlagChanged;

            if (time != null)
            {
                time.TimeAdvanced += OnTimeAdvanced;
            }

            RefreshDebug();
        }

        private void OnDestroy()
        {
            if (world != null)
            {
                world.FlagChanged -= OnFlagChanged;
            }

            if (time != null)
            {
                time.TimeAdvanced -= OnTimeAdvanced;
            }
        }

        private void Update()
        {
            var visible = Time.unscaledTime < _hideAt;
            var step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime);
            _messageGroup.alpha = Mathf.MoveTowards(_messageGroup.alpha, visible ? 1f : 0f, step);

            // Каждый кадр, пока надпись видна: список задач меняет высоту, когда задачи
            // приходят и уходят, и надпись должна ехать вместе с его нижним краем.
            if (_messageGroup.alpha > 0f)
            {
                PlaceMessage();
            }

            if (!Debug.isDebugBuild)
            {
                return;
            }

            // Одного wasPressedThisFrame мало: он верен только в кадре обработки события,
            // и нажатие при мигнувшем фокусе окна игры теряется. Поэтому фронт удержания
            // ловим ещё и сами, а wasPressedThisFrame оставляем для касания короче кадра.
            var keyboard = Keyboard.current;
            var held = keyboard != null && keyboard.rightCtrlKey.isPressed;
            var tapped = keyboard != null && keyboard.rightCtrlKey.wasPressedThisFrame;

            if (tapped || (held && !_toggleHeld))
            {
                var show = !_debugLabel.gameObject.activeSelf;
                _debugLabel.gameObject.SetActive(show);
                RefreshDebug();
                Debug.Log($"{nameof(StoryStatsHud)}: сводка параметров {(show ? "открыта" : "закрыта")}.", this);
            }

            _toggleHeld = held;

            if (_debugLabel.gameObject.activeSelf)
            {
                ReadTimeInput(keyboard);
            }
        }

        /// <summary>
        /// Пока сводка открыта, цифры подряд переводят часы в формате ЧЧММНН:
        /// шесть цифр — время и ночь (010002 — 01:00 второй ночи), четыре цифры — время
        /// в текущей ночи (0130 — 01:30). Четыре цифры применяются после короткой паузы,
        /// чтобы успеть добрать номер ночи. Верхний ряд и цифровой блок равноправны.
        /// </summary>
        private void ReadTimeInput(Keyboard keyboard)
        {
            if (keyboard == null)
            {
                return;
            }

            var idle = Time.unscaledTime - _timeInputAt;

            if (_timeInput.Length == 4 && idle > timeOnlyDelay)
            {
                ApplyTimeInput();
                RefreshDebug();
            }
            else if (_timeInput.Length > 0 && idle > timeInputTimeout)
            {
                _timeInput.Clear();
                RefreshDebug();
            }

            var digit = PressedDigit(keyboard);

            if (digit < 0)
            {
                return;
            }

            _timeInput.Append((char)('0' + digit));
            _timeInputAt = Time.unscaledTime;

            if (_timeInput.Length == 6)
            {
                ApplyTimeInput();
            }

            RefreshDebug();
        }

        private void ApplyTimeInput()
        {
            var hour = (_timeInput[0] - '0') * 10 + (_timeInput[1] - '0');
            var minute = (_timeInput[2] - '0') * 10 + (_timeInput[3] - '0');
            var night = _timeInput.Length == 6 ? (_timeInput[4] - '0') * 10 + (_timeInput[5] - '0') : -1;
            var typed = _timeInput.ToString();
            _timeInput.Clear();

            if (time == null)
            {
                return;
            }

            if (minute > 59 || hour * 60 + minute > TimeManager.ShiftEndMinutes)
            {
                Debug.LogWarning($"{nameof(StoryStatsHud)}: время {typed} вне смены 00:00–06:00.", this);
                return;
            }

            if (night != -1 && (night < 1 || night > 7))
            {
                Debug.LogWarning($"{nameof(StoryStatsHud)}: ночи {night} нет, их 1–7.", this);
                return;
            }

            var before = time.DebugClock;

            // Другая ночь — сперва начинаем её с 00:00, потом переводим часы внутри неё,
            // как при обычном вводе: события раньше отметки отменяют себя сами.
            if (night != -1 && night != time.Night)
            {
                time.DebugBeginNight(night);
            }

            // События и задачи более раннего времени отменяют себя сами,
            // по TimeManager.DebugJumped.
            time.DebugSetTime(hour, minute);
            Debug.Log($"{nameof(StoryStatsHud)}: часы смены {before} → {time.DebugClock}, " +
                      "события более раннего времени отменены.", this);
        }

        private static readonly Key[] DigitKeys =
        {
            Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
            Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        private static readonly Key[] NumpadKeys =
        {
            Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4,
            Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9,
        };

        private static int PressedDigit(Keyboard keyboard)
        {
            for (var i = 0; i < 10; i++)
            {
                if (keyboard[DigitKeys[i]].wasPressedThisFrame || keyboard[NumpadKeys[i]].wasPressedThisFrame)
                {
                    return i;
                }
            }

            return -1;
        }

        private void OnFlagChanged(string name)
        {
            if (!StoryStats.TryFind(name, out var stat))
            {
                return;
            }

            var value = Read(name);
            var changed = !_known.TryGetValue(name, out var previous) || !Mathf.Approximately(previous, value);
            _known[name] = value;

            if (changed && stat.Consequential)
            {
                // Повторное изменение продлевает показ, а не запускает надпись заново.
                _hideAt = Time.unscaledTime + holdTime;
            }

            RefreshDebug();
        }

        private void OnTimeAdvanced(int from, int to) => RefreshDebug();

        private float Read(string name) => world.TryGet<float>(name, out var value) ? value : 0f;

        private void RefreshDebug()
        {
            if (!_debugLabel.gameObject.activeSelf || world == null)
            {
                return;
            }

            _debugText.Clear();

            foreach (var stat in StoryStats.All)
            {
                _debugText.Append(stat.Name).Append(": ").Append(Read(stat.Name).ToString("0.##"))
                          .Append("  <alpha=#88>[").Append(stat.Min).Append("..").Append(stat.Max).Append("]<alpha=#FF>\n");
            }

            if (time != null)
            {
                _debugText.Append("GameTime: ").Append(time.DebugClock)
                          .Append("  <alpha=#88>[ЧЧ:ММ:ночь, ").Append(time.Minutes).Append(" / ").Append(TimeManager.ShiftEndMinutes)
                          .Append(" мин]<alpha=#FF>\n");

                // Подсказка и уже набранные цифры: ввод вслепую легко сбить.
                var typed = _timeInput.ToString().PadRight(6, '_');
                _debugText.Append("<alpha=#88>Ввести время: ").Append(typed, 0, 2).Append(':').Append(typed, 2, 2)
                          .Append(':').Append(typed, 4, 2)
                          .Append(" (6 цифр — с ночью, 4 — в текущей ночи)<alpha=#FF>\n");
            }

            _debugLabel.text = _debugText.ToString();
        }

        private void Build()
        {
            // TryGetComponent, а не «GetComponent ?? AddComponent»: в редакторе отсутствующий
            // компонент возвращается поддельным null, и оператор ?? его не замечает.
            if (!TryGetComponent<Canvas>(out var canvas))
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            if (!TryGetComponent<UnityEngine.UI.CanvasScaler>(out var scaler))
            {
                scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            }

            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Рейкастера на холсте нет намеренно: надписи не должны перехватывать клики.
            var messageLabel = CreateText("Consequences", message, 30f, messageColor, Vector2.zero);
            messageLabel.fontStyle = FontStyles.Italic;
            messageLabel.outlineColor = messageOutlineColor;
            messageLabel.outlineWidth = messageOutlineWidth;
            messageLabel.alignment = TextAlignmentOptions.TopRight;
            _messageRect = messageLabel.rectTransform;
            _messageRect.anchorMin = Vector2.one;
            _messageRect.anchorMax = Vector2.one;
            _messageRect.pivot = Vector2.one;
            PlaceMessage();
            _messageGroup = messageLabel.gameObject.AddComponent<CanvasGroup>();
            _messageGroup.alpha = 0f;
            _messageGroup.blocksRaycasts = false;

            // Сводка ниже надписи, чтобы они не перекрывали друг друга.
            _debugLabel = CreateText("StatsDebug", string.Empty, 22f, debugColor, new Vector2(margin.x, -margin.y - 48f));
            _debugLabel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Надпись встаёт под список задач, выровненная по его правому краю. Холсты у них
        /// с одним эталонным разрешением, поэтому единицы совпадают.
        /// </summary>
        private void PlaceMessage()
        {
            var top = taskHud != null ? taskHud.Bottom + gapBelowTasks : margin.y;
            var right = taskHud != null ? taskHud.RightMargin : margin.x;
            _messageRect.anchoredPosition = new Vector2(-right, -top);
        }

        private TextMeshProUGUI CreateText(string objectName, string text, float size, Color color, Vector2 position)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(700f, 200f);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }
    }
}
