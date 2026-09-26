using System.Collections.Generic;
using System.Text;
using Radio.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Radio.UI
{
    /// <summary>
    /// Левый верхний угол: «Это будет иметь последствия», когда меняется параметр сюжета,
    /// и отладочная сводка всех параметров по правому Ctrl.
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

        [Header("Последствия")]
        [SerializeField] private string message = "Это будет иметь последствия";

        [Tooltip("Сколько секунд надпись держится после последнего изменения.")]
        [SerializeField] private float holdTime = 3f;

        [SerializeField] private float fadeTime = 0.35f;

        [SerializeField] private Color messageColor = new Color(1f, 0.85f, 0.55f, 0.95f);

        [Header("Вид")]
        [Tooltip("Порядок холста. Выше диалога и мини-игр, как у списка задач.")]
        [SerializeField] private int sortingOrder = 50;

        [SerializeField] private Vector2 margin = new Vector2(36f, 32f);

        [SerializeField] private Color debugColor = new Color(0.6f, 1f, 0.6f, 0.95f);

        [Tooltip("Через сколько секунд без нажатий недобранные цифры времени сбрасываются.")]
        [SerializeField] private float timeInputTimeout = 3f;

        private readonly Dictionary<string, float> _known = new Dictionary<string, float>();
        private readonly StringBuilder _debugText = new StringBuilder();

        private CanvasGroup _messageGroup;
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
        /// Пока сводка открыта, четыре цифры подряд ставят часы смены: 0130 — это 01:30.
        /// Верхний ряд и цифровой блок равноправны.
        /// </summary>
        private void ReadTimeInput(Keyboard keyboard)
        {
            if (keyboard == null)
            {
                return;
            }

            if (_timeInput.Length > 0 && Time.unscaledTime - _timeInputAt > timeInputTimeout)
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

            if (_timeInput.Length == 4)
            {
                ApplyTimeInput();
            }

            RefreshDebug();
        }

        private void ApplyTimeInput()
        {
            var hour = (_timeInput[0] - '0') * 10 + (_timeInput[1] - '0');
            var minute = (_timeInput[2] - '0') * 10 + (_timeInput[3] - '0');
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

            // События и задачи более раннего времени отменяют себя сами,
            // по TimeManager.DebugJumped.
            var before = time.Clock;
            time.DebugSetTime(hour, minute);
            Debug.Log($"{nameof(StoryStatsHud)}: часы смены {before} → {time.Clock}, " +
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
                _debugText.Append("GameTime: ").Append(time.Clock)
                          .Append("  <alpha=#88>[ночь ").Append(time.Night).Append(", ")
                          .Append(time.Minutes).Append(" / ").Append(TimeManager.ShiftEndMinutes).Append(" мин]<alpha=#FF>\n");

                // Подсказка и уже набранные цифры: ввод вслепую легко сбить.
                var typed = _timeInput.ToString().PadRight(4, '_');
                _debugText.Append("<alpha=#88>Ввести время: ").Append(typed, 0, 2).Append(':').Append(typed, 2, 2)
                          .Append(" (4 цифры)<alpha=#FF>\n");
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
            var messageLabel = CreateText("Consequences", message, 30f, messageColor, new Vector2(margin.x, -margin.y));
            messageLabel.fontStyle = FontStyles.Italic;
            _messageGroup = messageLabel.gameObject.AddComponent<CanvasGroup>();
            _messageGroup.alpha = 0f;
            _messageGroup.blocksRaycasts = false;

            // Сводка ниже надписи, чтобы они не перекрывали друг друга.
            _debugLabel = CreateText("StatsDebug", string.Empty, 22f, debugColor, new Vector2(margin.x, -margin.y - 48f));
            _debugLabel.gameObject.SetActive(false);
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
