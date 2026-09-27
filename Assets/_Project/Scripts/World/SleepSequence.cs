using System.Collections;
using Radio.Dialogue;
using Radio.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

namespace Radio.World
{
    /// <summary>
    /// Сон между ночами: экран темнеет, на чёрном фоне идёт разговор во сне, игрок
    /// просыпается у дивана, и начинается следующая ночь. Запускается командой диалога
    /// <c>&lt;&lt;go_to_sleep&gt;&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Узел сна берётся по номеру ночи: Night1_Dream, Night2_Dream… Если для ночи узла
    /// нет, на чёрном экране стоит заглушка с подписью. Картинку сна можно положить
    /// в поле «Картинка сна» — она встанет поверх чёрного, под окном разговора.
    /// Команда не начинает сон сразу, а ждёт конца диалога, как и мини-игра: иначе
    /// затемнение легло бы поверх ещё открытого окна разговора.
    /// </remarks>
    public sealed class SleepSequence : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private DialogueRunner runner;

        [SerializeField] private DialogueController dialogue;

        [Tooltip("Где игрок стоит, когда проснулся. Смотрит туда же, куда синяя стрелка объекта.")]
        [SerializeField] private Transform wakePoint;

        [Header("Затемнение")]
        [SerializeField] private float fadeOutSeconds = 1.5f;

        [SerializeField] private float fadeInSeconds = 1.5f;

        [Header("Сон")]
        [Tooltip("Узел разговора во сне. {0} — номер ночи, которая заканчивается.")]
        [SerializeField] private string dreamNodeFormat = "Night{0}_Dream";

        [Tooltip("Картинка сна на чёрном фоне. Пусто — только чёрный экран.")]
        [SerializeField] private Sprite dreamImage;

        [Header("Смена ночи")]
        [Tooltip("Надпись после сна, пока экран ещё чёрный. {0} — номер закончившейся ночи. " +
                 "Пусто — без надписи.")]
        [SerializeField] private string nightEndText = "Ночь {0} завершена";

        [Tooltip("Надпись перед пробуждением. {0} — номер новой ночи. Пусто — без надписи.")]
        [SerializeField] private string nightStartText = "Ночь {0}\n00:00";

        [Tooltip("Сколько держится каждая надпись, с.")]
        [SerializeField] private float titleSeconds = 2f;

        [Tooltip("Звук будильника при пробуждении. Пусто — без звука (заглушка: положи сюда свой клип).")]
        [SerializeField] private AudioClip wakeSound;

        [Tooltip("Мысли после пробуждения. {0} — номер новой ночи. Узла нет — без мыслей.")]
        [SerializeField] private string wakeNodeFormat = "Night{0}_Wake";

        [Tooltip("Сколько держится заглушка, если для ночи нет узла сна, с.")]
        [SerializeField] private float cutsceneSeconds = 3f;

        [SerializeField] private string cutscenePlaceholder = "Катсцена (заглушка)";

        [Tooltip("Шрифт подписи. Обязан содержать кириллицу.")]
        [SerializeField] private TMP_FontAsset font;

        [Tooltip("Порядок холста: поверх всего интерфейса, включая задачи.")]
        [SerializeField] private int sortingOrder = 200;

        [Tooltip("Порядок холста, пока идёт разговор во сне: ниже окна разговора (у него 10).")]
        [SerializeField] private int dialogueSortingOrder = 9;

        private Canvas _canvas;
        private CanvasGroup _fade;
        private TextMeshProUGUI _label;
        private Image _image;
        private bool _dreamFinished;
        private PlayerInteractor _interactor;
        private bool _pending;
        private bool _running;

        private void Awake()
        {
            Build();

            if (runner == null)
            {
                runner = FindFirstObjectByType<DialogueRunner>();
            }

            if (dialogue == null)
            {
                dialogue = FindFirstObjectByType<DialogueController>();
            }

            if (runner != null)
            {
                runner.AddCommandHandler("go_to_sleep", (System.Action)Request);
            }

            if (dialogue != null)
            {
                dialogue.DialogueFinished += StartPendingIfAny;
            }
        }

        private void OnDestroy()
        {
            if (dialogue != null)
            {
                dialogue.DialogueFinished -= StartPendingIfAny;
            }
        }

        private void Request()
        {
            _pending = true;

            if (dialogue == null || !dialogue.IsRunning)
            {
                StartPendingIfAny();
            }
        }

        private void StartPendingIfAny()
        {
            if (!_pending || _running)
            {
                return;
            }

            _pending = false;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _running = true;
            TakeOverPlayer();

            yield return Fade(0f, 1f, fadeOutSeconds);
            yield return PlayCutscene();

            // Пока экран чёрный: новая ночь и игрок у дивана. Так игрок не увидит ни
            // скачка часов, ни телепорта.
            var session = GameSession.Current;

            if (session != null)
            {
                yield return ShowTitle(string.Format(nightEndText, session.Time.Night));
                session.Time.BeginNextNight();
                Debug.Log($"{nameof(SleepSequence)}: началась ночь {session.Time.Night}, {session.Time.DebugClock}.", this);
                yield return ShowTitle(string.Format(nightStartText, session.Time.Night));
            }

            MovePlayerToWakePoint();

            if (wakeSound != null)
            {
                AudioSource.PlayClipAtPoint(wakeSound, _interactor != null ? _interactor.transform.position : transform.position);
            }

            yield return Fade(1f, 0f, fadeInSeconds);

            ReleasePlayer();
            _running = false;

            // Мысли после пробуждения — обычным разговором, уже с управлением.
            var wakeNode = session != null ? string.Format(wakeNodeFormat, session.Time.Night) : null;

            if (dialogue != null && runner != null && !string.IsNullOrEmpty(wakeNode) && runner.Dialogue.NodeExists(wakeNode))
            {
                dialogue.StartDialogue(wakeNode);
            }
        }

        private IEnumerator ShowTitle(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            _label.text = text;
            _label.gameObject.SetActive(true);
            yield return new WaitForSeconds(titleSeconds);
            _label.gameObject.SetActive(false);
        }

        private IEnumerator PlayCutscene()
        {
            var session = GameSession.Current;
            var node = session != null ? string.Format(dreamNodeFormat, session.Time.Night) : null;

            if (runner != null && dialogue != null && !string.IsNullOrEmpty(node) && runner.Dialogue.NodeExists(node))
            {
                yield return PlayDream(node);
                yield break;
            }

            _label.text = cutscenePlaceholder;
            _label.gameObject.SetActive(true);
            yield return new WaitForSeconds(cutsceneSeconds);
            _label.gameObject.SetActive(false);
        }

        private IEnumerator PlayDream(string node)
        {
            _image.sprite = dreamImage;
            _image.gameObject.SetActive(dreamImage != null);

            // Чёрный фон опускаем под окно разговора, но оставляем над остальным миром.
            // Задачи и надпись о последствиях при этом видны: выбор во сне тоже меняет сюжет.
            var order = _canvas.sortingOrder;
            _canvas.sortingOrder = dialogueSortingOrder;

            // Своё управление отпускаем на время разговора: окно выбора берёт его само.
            ReleasePlayer();

            _dreamFinished = false;
            dialogue.DialogueFinished += OnDreamFinished;
            dialogue.StartDialogue(node);

            while (!_dreamFinished)
            {
                yield return null;
            }

            dialogue.DialogueFinished -= OnDreamFinished;
            TakeOverPlayer();

            _canvas.sortingOrder = order;
            _image.gameObject.SetActive(false);
        }

        private void OnDreamFinished() => _dreamFinished = true;

        private IEnumerator Fade(float from, float to, float seconds)
        {
            _fade.gameObject.SetActive(true);

            for (var t = 0f; t < seconds; t += Time.deltaTime)
            {
                _fade.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }

            _fade.alpha = to;

            if (to <= 0f)
            {
                _fade.gameObject.SetActive(false);
            }
        }

        private void TakeOverPlayer()
        {
            _interactor = FindAnyObjectByType<PlayerInteractor>();

            if (_interactor == null)
            {
                return;
            }

            // Тот же счётчик владельцев, что у диалога и мини-игр: отпускаем только своё.
            _interactor.AddInputBlock(this);

            if (_interactor.Movement != null)
            {
                _interactor.Movement.AddSuspendRequest(this);
            }
        }

        private void ReleasePlayer()
        {
            if (_interactor == null)
            {
                return;
            }

            _interactor.RemoveInputBlock(this);

            if (_interactor.Movement != null)
            {
                _interactor.Movement.RemoveSuspendRequest(this);
            }

            _interactor = null;
        }

        private void MovePlayerToWakePoint()
        {
            if (_interactor == null || wakePoint == null)
            {
                return;
            }

            var player = _interactor.transform;

            // CharacterController перетирает позицию, заданную в обход него: на время
            // переноса его выключаем.
            var controller = player.GetComponent<CharacterController>();
            var wasEnabled = controller != null && controller.enabled;

            if (controller != null)
            {
                controller.enabled = false;
            }

            var forward = wakePoint.forward;
            forward.y = 0f;
            player.SetPositionAndRotation(wakePoint.position,
                forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forward) : player.rotation);

            if (controller != null)
            {
                controller.enabled = wasEnabled;
            }
        }

        private void Build()
        {
            // Холст строится кодом, как и остальной интерфейс проекта.
            var canvasObject = new GameObject("SleepFade", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            _canvas = canvas;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var black = new GameObject("Black", typeof(RectTransform));
            var rect = (RectTransform)black.transform;
            rect.SetParent(canvasObject.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            black.AddComponent<Image>().color = Color.black;

            var imageObject = new GameObject("DreamImage", typeof(RectTransform));
            var imageRect = (RectTransform)imageObject.transform;
            imageRect.SetParent(canvasObject.transform, false);
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            _image = imageObject.AddComponent<Image>();
            _image.preserveAspect = true;
            _image.raycastTarget = false;
            imageObject.SetActive(false);

            _fade = canvasObject.AddComponent<CanvasGroup>();
            _fade.alpha = 0f;
            _fade.blocksRaycasts = false;

            var labelObject = new GameObject("CutscenePlaceholder", typeof(RectTransform));
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(canvasObject.transform, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            _label = labelObject.AddComponent<TextMeshProUGUI>();
            _label.font = font;
            _label.fontSize = 40f;
            _label.color = new Color(1f, 1f, 1f, 0.6f);
            _label.alignment = TextAlignmentOptions.Center;
            _label.raycastTarget = false;
            labelObject.SetActive(false);

            canvasObject.SetActive(false);
        }
    }
}
