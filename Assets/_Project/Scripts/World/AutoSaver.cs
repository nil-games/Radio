using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Radio.World
{
    /// <summary>
    /// Когда игра сохраняется (GDD 02.12): в начале ночи и после сюжетных шагов —
    /// в первый спокойный момент. Ручного сохранения нет, слот один.
    /// </summary>
    /// <remarks>
    /// Сюжетный шаг пройден, когда ставится один из его флагов из <see cref="StoryBeats"/>.
    /// Сразу сохраняться нельзя: флаг ставится посреди разговора или мини-игры. Поэтому
    /// сохранение откладывается, пока игрок свободен и ничто в сцене не мешает
    /// (<see cref="ISaveBlocker"/>), и держится так <see cref="quietSeconds"/> подряд.
    /// Отладка: правый Ctrl + S — сохранить сейчас, без ожидания.
    /// Порядок исполнения позже <see cref="SessionRestorer"/>: на флаги подписываемся
    /// уже после восстановления, иначе загрузка сохранения тут же сохранялась бы снова.
    /// </remarks>
    [DefaultExecutionOrder(1001)]
    [RequireComponent(typeof(GameSession))]
    public sealed class AutoSaver : MonoBehaviour
    {
        [Tooltip("Сюжетные шаги: их флаги завершения запускают сохранение.")]
        [SerializeField] private StoryBeats beats;

        [Tooltip("Сколько секунд подряд должно быть спокойно, чтобы сохраниться.")]
        [SerializeField] private float quietSeconds = 1.5f;

        [Header("Надпись")]
        [SerializeField] private string savedText = "Сохранено";

        [Tooltip("Шрифт надписи. Обязан содержать кириллицу.")]
        [SerializeField] private TMP_FontAsset font;

        [SerializeField] private float labelSeconds = 2f;

        private readonly HashSet<string> _beatFlags = new HashSet<string>();
        private readonly List<ISaveBlocker> _blockers = new List<ISaveBlocker>();
        private GameSession _session;
        private bool _pending;
        private float _quietFor;
        private TextMeshProUGUI _label;
        private float _hideAt;

        private void Awake()
        {
            _session = GetComponent<GameSession>();

            if (beats != null)
            {
                foreach (var beat in beats.Beats)
                {
                    foreach (var flag in beat.doneFlags ?? System.Array.Empty<string>())
                    {
                        if (!string.IsNullOrWhiteSpace(flag))
                        {
                            _beatFlags.Add(flag);
                        }
                    }
                }
            }

            BuildLabel();
        }

        private void Start()
        {
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (behaviour is ISaveBlocker blocker)
                {
                    _blockers.Add(blocker);
                }
            }

            _session.World.FlagChanged += OnFlagChanged;
            SaveSystem.Saved += ShowLabel;
        }

        private void OnDestroy()
        {
            if (_session != null && _session.World != null)
            {
                _session.World.FlagChanged -= OnFlagChanged;
            }

            SaveSystem.Saved -= ShowLabel;
        }

        /// <summary>Сохранить сейчас. <paramref name="nightStart"/> — снимок начала ночи.</summary>
        public void SaveNow(bool nightStart = false)
        {
            _pending = false;
            SaveSystem.Save(SaveData.Capture(), nightStart);
        }

        private void OnFlagChanged(string flag)
        {
            // Флаг шага, поставленный в true, — шаг пройден. Восстановление флагов из
            // снимка тоже их ставит, но тогда сохранять нечего: снимок и так этот.
            if (!_beatFlags.Contains(flag) || !_session.World.TryGet<bool>(flag, out var value) || !value)
            {
                return;
            }

            _pending = true;
            _quietFor = 0f;
        }

        private void Update()
        {
            if (Debug.isDebugBuild)
            {
                var keyboard = Keyboard.current;

                if (keyboard != null && keyboard.rightCtrlKey.isPressed && keyboard.sKey.wasPressedThisFrame)
                {
                    SaveNow();
                }
            }

            if (_label != null && _label.gameObject.activeSelf && Time.unscaledTime >= _hideAt)
            {
                _label.gameObject.SetActive(false);
            }

            if (!_pending)
            {
                return;
            }

            _quietFor = IsQuiet() ? _quietFor + Time.deltaTime : 0f;

            if (_quietFor >= quietSeconds)
            {
                SaveNow();
            }
        }

        private bool IsQuiet()
        {
            if (TimedEvent.PlayerBusy != null && TimedEvent.PlayerBusy())
            {
                return false;
            }

            foreach (var blocker in _blockers)
            {
                if (blocker is Object unityObject && unityObject != null && blocker.BlocksSave)
                {
                    return false;
                }
            }

            return true;
        }

        private void ShowLabel(SaveData data)
        {
            if (_label == null)
            {
                return;
            }

            _label.gameObject.SetActive(true);
            _hideAt = Time.unscaledTime + labelSeconds;
        }

        private void BuildLabel()
        {
            if (font == null || string.IsNullOrWhiteSpace(savedText))
            {
                return;
            }

            // Свой холст, как у остального интерфейса проекта: левый нижний угол, поверх всего.
            var canvasObject = new GameObject("SaveLabel", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;

            var scaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var labelObject = new GameObject("Text", typeof(RectTransform));
            var rect = (RectTransform)labelObject.transform;
            rect.SetParent(canvasObject.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(40f, 32f);
            rect.sizeDelta = new Vector2(400f, 50f);

            _label = labelObject.AddComponent<TextMeshProUGUI>();
            _label.font = font;
            _label.text = savedText;
            _label.fontSize = 28f;
            _label.color = new Color(1f, 1f, 1f, 0.7f);
            _label.alignment = TextAlignmentOptions.BottomLeft;
            _label.raycastTarget = false;
            labelObject.SetActive(false);
        }
    }
}
