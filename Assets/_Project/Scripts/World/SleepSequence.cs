using System.Collections;
using Radio.Dialogue;
using Radio.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
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
    /// Видео сна (по ночам) встаёт туда же и идёт по кругу, пока длится разговор.
    /// Команда не начинает сон сразу, а ждёт конца диалога, как и мини-игра: иначе
    /// затемнение легло бы поверх ещё открытого окна разговора.
    /// Если для ночи задана сцена сна, вместо текста игрок уходит в неё (квартира
    /// выгружается, <see cref="MiniLocationTrip"/>). Вернувшись, квартира грузится
    /// заново, <see cref="SessionRestorer"/> восстанавливает её и зовёт
    /// <see cref="ResumeAfterDream"/>: этот компонент доигрывает ночь с чёрного экрана —
    /// конец сна (Night{N}_DreamEnd), утро, титры, пробуждение у дивана.
    /// Проснувшись, игра сохраняется: начало ночи — точка сохранения (GDD 02.12).
    /// </remarks>
    public sealed class SleepSequence : MonoBehaviour, ISaveBlocker
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
        [Tooltip("Узел разговора во сне. {0} — номер ночи, которая заканчивается. " +
                 "Нужен, только если для ночи нет сцены сна.")]
        [SerializeField] private string dreamNodeFormat = "Night{0}_Dream";

        [Tooltip("Сцены сна по ночам: элемент 0 — сон после ночи 1. Пусто — сон текстом " +
                 "(узел сна). Сцена должна быть в списке сборки.")]
        [SerializeField] private string[] dreamScenes = System.Array.Empty<string>();

        [Tooltip("Узел после возвращения из сцены сна, на чёрном экране. {0} — номер ночи. " +
                 "Узла нет — сразу утро.")]
        [SerializeField] private string dreamEndNodeFormat = "Night{0}_DreamEnd";

        [Tooltip("Картинка сна на чёрном фоне. Пусто — только чёрный экран.")]
        [SerializeField] private Sprite dreamImage;

        [Tooltip("Видео на фоне разговора во сне, по ночам: элемент 0 — сон после ночи 1, " +
                 "элемент 1 — после ночи 2. Нет видео для ночи — картинка или чёрный фон.")]
        [SerializeField] private VideoClip[] dreamVideos = System.Array.Empty<VideoClip>();

        [Tooltip("Играть звук видео сна.")]
        [SerializeField] private bool dreamVideoAudio = true;

        [Range(0f, 1f)]
        [SerializeField] private float dreamVideoVolume = 0.6f;

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

        [Header("Утро")]
        [Tooltip("Разговор утром, после сна и до титра конца ночи. {0} — номер ночи, " +
                 "которая заканчивается. Узла нет — утра нет.")]
        [SerializeField] private string morningNodeFormat = "Night{0}_Morning";

        [Tooltip("Звук перед утренним разговором (звонок телефона). Пусто — без звука.")]
        [SerializeField] private AudioClip morningSound;

        [Tooltip("Пауза после звука до начала разговора, с.")]
        [SerializeField] private float morningSoundDelay = 1.5f;

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
        private RawImage _video;
        private AspectRatioFitter _videoFitter;
        private VideoPlayer _videoPlayer;
        private RenderTexture _videoTexture;
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

        /// <summary>Сон или утро идут — сохраняться посреди нельзя.</summary>
        public bool BlocksSave => _pending || _running;

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

            var dreamScene = DreamSceneFor(GameSession.Current);

            if (!string.IsNullOrEmpty(dreamScene))
            {
                // Квартира выгружается; ночь доиграет Resume, когда она загрузится снова.
                ReleasePlayer();
                MiniLocationTrip.Leave(dreamScene);
                yield break;
            }

            yield return PlayCutscene();
            yield return FinishNight();
        }

        /// <summary>
        /// Вернулись из сцены сна: квартира загружена заново и уже восстановлена.
        /// Экран сразу чёрный — игрок не должен увидеть квартиру до пробуждения, —
        /// потом ночь доигрывается.
        /// </summary>
        public void ResumeAfterDream()
        {
            _running = true;
            _canvas.gameObject.SetActive(true);
            _fade.gameObject.SetActive(true);
            _fade.alpha = 1f;
            TakeOverPlayer();
            StartCoroutine(ResumeRoutine());
        }

        private IEnumerator ResumeRoutine()
        {
            // Кадр на то, чтобы диалоговая система и остальные успели проснуться.
            yield return null;

            var session = GameSession.Current;
            var node = session != null ? string.Format(dreamEndNodeFormat, session.Time.Night) : null;

            if (runner != null && dialogue != null && !string.IsNullOrEmpty(node) && runner.Dialogue.NodeExists(node))
            {
                yield return PlayDream(node, false);
            }

            yield return FinishNight();
        }

        private string DreamSceneFor(GameSession session)
        {
            var index = session != null ? session.Time.Night - 1 : -1;
            return index >= 0 && index < dreamScenes.Length ? dreamScenes[index] : null;
        }

        /// <summary>Утро, титры, новая ночь и пробуждение у дивана.</summary>
        private IEnumerator FinishNight()
        {
            yield return PlayMorning();

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

            // Начало ночи — точка сохранения: до первых её событий и мыслей героя.
            if (session != null && session.TryGetComponent<AutoSaver>(out var saver))
            {
                saver.SaveNow(true);
            }

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

        /// <summary>
        /// Утро после сна (Night{N}_Morning): например, звонок начальника с итогами смены.
        /// Идёт на чистом чёрном экране, до титра «Ночь N завершена». Нет узла — нет утра.
        /// </summary>
        private IEnumerator PlayMorning()
        {
            var session = GameSession.Current;
            var node = session != null ? string.Format(morningNodeFormat, session.Time.Night) : null;

            if (runner == null || dialogue == null || string.IsNullOrEmpty(node) || !runner.Dialogue.NodeExists(node))
            {
                yield break;
            }

            if (morningSound != null)
            {
                AudioSource.PlayClipAtPoint(morningSound, _interactor != null ? _interactor.transform.position : transform.position);
                yield return new WaitForSeconds(morningSoundDelay);
            }

            yield return PlayDream(node, false);
        }

        private IEnumerator PlayDream(string node, bool dreamVisuals = true)
        {
            _image.sprite = dreamImage;
            _image.gameObject.SetActive(dreamVisuals && dreamImage != null);

            var session = GameSession.Current;

            if (dreamVisuals)
            {
                StartDreamVideo(session != null ? session.Time.Night : 1);
            }

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
            StopDreamVideo();
        }

        private void StartDreamVideo(int night)
        {
            var index = night - 1;
            var clip = index >= 0 && index < dreamVideos.Length ? dreamVideos[index] : null;

            if (clip == null)
            {
                return;
            }

            _videoTexture = new RenderTexture((int)clip.width, (int)clip.height, 0) { name = "DreamVideo" };
            _video.texture = _videoTexture;
            _videoFitter.aspectRatio = (float)clip.width / clip.height;

            // Прозрачно до первого кадра: иначе мелькнёт мусор из пустой текстуры.
            _video.color = new Color(1f, 1f, 1f, 0f);
            _video.gameObject.SetActive(true);

            _videoPlayer.clip = clip;
            _videoPlayer.targetTexture = _videoTexture;
            _videoPlayer.audioOutputMode = dreamVideoAudio ? VideoAudioOutputMode.Direct : VideoAudioOutputMode.None;

            if (dreamVideoAudio)
            {
                _videoPlayer.SetDirectAudioVolume(0, dreamVideoVolume);
            }

            _videoPlayer.Prepare();
        }

        private void OnDreamVideoPrepared(VideoPlayer player)
        {
            _video.color = Color.white;
            player.Play();
        }

        private void StopDreamVideo()
        {
            _videoPlayer.Stop();
            _video.gameObject.SetActive(false);

            if (_videoTexture != null)
            {
                _video.texture = null;
                _videoTexture.Release();
                Destroy(_videoTexture);
                _videoTexture = null;
            }
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

            // Видео сна заполняет экран целиком: лишнее по краям обрезается.
            var videoObject = new GameObject("DreamVideo", typeof(RectTransform));
            var videoRect = (RectTransform)videoObject.transform;
            videoRect.SetParent(canvasObject.transform, false);
            videoRect.anchorMin = Vector2.zero;
            videoRect.anchorMax = Vector2.one;
            videoRect.offsetMin = Vector2.zero;
            videoRect.offsetMax = Vector2.zero;
            _videoFitter = videoObject.AddComponent<AspectRatioFitter>();
            _videoFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            _video = videoObject.AddComponent<RawImage>();
            _video.raycastTarget = false;
            _videoPlayer = videoObject.AddComponent<VideoPlayer>();
            _videoPlayer.playOnAwake = false;
            _videoPlayer.isLooping = true;
            _videoPlayer.source = VideoSource.VideoClip;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.prepareCompleted += OnDreamVideoPrepared;
            videoObject.SetActive(false);

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
