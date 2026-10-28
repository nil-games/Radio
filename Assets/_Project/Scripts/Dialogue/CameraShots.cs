using System;
using System.Collections;
using Radio.Interaction;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

namespace Radio.Dialogue
{
    /// <summary>
    /// Смена планов посреди разговора: <c>&lt;&lt;camera bathroom&gt;&gt;</c> переводит взгляд на
    /// заранее поставленную камеру, <c>&lt;&lt;camera_back&gt;&gt;</c> возвращает туда, откуда пришли.
    /// Переход — через короткое затемнение, и разговор ждёт, пока оно не закончится.
    /// </summary>
    /// <remarks>
    /// Собеседник при этом переезжает сам: он не стоит в мире, а нарисован поверх экрана,
    /// поэтому сменили камеру — и он уже «в ванной».
    /// <para/>
    /// Живёт в сцене, а не в префабе диалога: камеры планов — объекты сцены, а ссылка
    /// из префаба на объект сцены не сохраняется.
    /// <para/>
    /// Звук не трогаем: слушатель один, на камере игрока, и он работает и тогда, когда
    /// сама камера выключена. Так уже устроены камеры кресла, двери и радио.
    /// </remarks>
    public sealed class CameraShots : MonoBehaviour
    {
        [Serializable]
        public struct Shot
        {
            [Tooltip("Имя плана для диалога: <<camera bathroom>>. Латиницей, без пробелов.")]
            public string id;

            [Tooltip("Камера плана. В сцене должна быть выключена.")]
            public Camera camera;

            [Tooltip("Сдвиг собеседника на этом плане, в долях экрана: x вправо, y вверх. " +
                     "Отрицательный y опускает его ниже; ноги при этом могут уйти за край кадра.")]
            public Vector2 portraitOffset;

            [Tooltip("Во сколько раз собеседник крупнее обычного на этом плане (камера ближе к нему). " +
                     "0 или 1 — как обычно. Растёт вверх от низа фигуры: крупный план идёт с отрицательным y, " +
                     "чтобы ноги ушли за нижний край кадра.")]
            public float portraitScale;
        }

        [Header("Планы")]
        [SerializeField] private Shot[] shots = Array.Empty<Shot>();

        [Header("Затемнение")]
        [Tooltip("Длительность половины перехода, с: столько же уходит в чёрное и столько же выходит.")]
        [SerializeField] private float fadeTime = 0.3f;

        [Tooltip("Сколько держать чёрный экран между половинами, с.")]
        [SerializeField] private float holdTime = 0.1f;

        [Tooltip("Порядок холста затемнения. Выше диалога (10), чтобы гасла и панель с собеседником.")]
        [SerializeField] private int sortingOrder = 90;

        [Header("Ссылки")]
        [Tooltip("Если пусто, ищутся в сцене.")]
        [SerializeField] private DialogueRunner runner;

        [SerializeField] private DialogueController dialogue;

        [SerializeField] private DialoguePortrait portrait;

        private PlayerInteractor _interactor;
        private CanvasGroup _fade;

        // Камера, с которой ушли на первый план, и камера, на которой стоим сейчас.
        // Пока _current пусто, планов нет и возвращать нечего.
        private Camera _return;
        private Camera _current;
        private Coroutine _running;

        private void Awake()
        {
            // Страхуемся от камеры плана, сохранённой включённой: двух активных камер
            // быть не должно, иначе Camera.main становится случайным.
            foreach (var shot in shots)
            {
                if (shot.camera != null)
                {
                    shot.camera.enabled = false;
                }
            }

            BuildFade();
        }

        // Start, а не Awake: диалоговая система — префаб со своим порядком пробуждения.
        private void Start()
        {
            if (runner == null)
            {
                runner = FindFirstObjectByType<DialogueRunner>();
            }

            if (dialogue == null)
            {
                dialogue = FindFirstObjectByType<DialogueController>();
            }

            if (portrait == null)
            {
                portrait = FindFirstObjectByType<DialoguePortrait>(FindObjectsInactive.Include);
            }

            if (runner == null)
            {
                Debug.LogError($"{nameof(CameraShots)}: не найден Dialogue Runner, планы не будут работать.", this);
                enabled = false;
                return;
            }

            // Команды возвращают корутину: Yarn её дожидается, и следующая реплика
            // появляется уже на новом плане, а не посреди затемнения.
            runner.AddCommandHandler("camera", (Func<string, Coroutine>)ShowShot);
            runner.AddCommandHandler("camera_back", (Func<Coroutine>)ShowBack);

            if (dialogue != null)
            {
                dialogue.DialogueFinished += OnDialogueFinished;
            }
        }

        private void OnDestroy()
        {
            if (dialogue != null)
            {
                dialogue.DialogueFinished -= OnDialogueFinished;
            }
        }

        private Coroutine ShowShot(string id)
        {
            foreach (var shot in shots)
            {
                if (shot.camera == null || !string.Equals(shot.id, id, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return Run(shot.camera, shot.portraitOffset, shot.portraitScale);
            }

            // Опечатку в имени плана заранее не проверить, поэтому она обязана быть громкой.
            Debug.LogError($"{nameof(CameraShots)}: плана «{id}» нет в списке.", this);
            return null;
        }

        private Coroutine ShowBack() => _current == null ? null : Run(null, Vector2.zero, 1f);

        private Coroutine Run(Camera target, Vector2 portraitOffset, float portraitScale)
        {
            if (_running != null)
            {
                StopCoroutine(_running);
            }

            _running = StartCoroutine(Transition(target, portraitOffset, portraitScale));
            return _running;
        }

        private IEnumerator Transition(Camera target, Vector2 portraitOffset, float portraitScale)
        {
            yield return Fade(1f);
            Switch(target);

            // Собеседника переставляем в темноте вместе с камерой, чтобы прыжка не было видно.
            if (portrait != null)
            {
                portrait.SetPlacement(portraitOffset, portraitScale);
            }

            if (holdTime > 0f)
            {
                yield return new WaitForSecondsRealtime(holdTime);
            }

            yield return Fade(0f);
            _running = null;
        }

        /// <summary>
        /// Переключает камеру. <paramref name="target"/> = null — вернуться на исходную.
        /// </summary>
        private void Switch(Camera target)
        {
            if (_interactor == null)
            {
                _interactor = FindFirstObjectByType<PlayerInteractor>();
            }

            if (_interactor == null)
            {
                return;
            }

            var from = _interactor.ActiveCamera;

            if (target == null)
            {
                if (_current == null || _return == null)
                {
                    return;
                }

                target = _return;
            }
            else if (_current == null)
            {
                // Первый план разговора: запоминаем, куда потом вернуться.
                _return = from;
            }

            if (from == target)
            {
                return;
            }

            // Порядок важен, как и у двери с креслом: сперва гасим старую камеру,
            // потом зажигаем новую — иначе будет кадр с двумя активными камерами.
            if (from != null)
            {
                from.enabled = false;
            }

            target.enabled = true;
            _interactor.SetActiveCamera(target);
            _current = target == _return ? null : target;
        }

        private IEnumerator Fade(float to)
        {
            if (_fade == null)
            {
                yield break;
            }

            var from = _fade.alpha;
            _fade.blocksRaycasts = true;

            // unscaledDeltaTime: переход не должен зависеть от паузы и замедления времени.
            for (var t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                _fade.alpha = Mathf.Lerp(from, to, t / Mathf.Max(0.0001f, fadeTime));
                yield return null;
            }

            _fade.alpha = to;
            _fade.blocksRaycasts = to > 0f;
        }

        /// <summary>
        /// Разговор кончился, а камера всё ещё на чужом плане — например, его закрыли
        /// досрочно. Возвращаем сразу: иначе игрок застрянет в виде ванной, а дверь,
        /// отпуская его, выключит не ту камеру.
        /// </summary>
        private void OnDialogueFinished()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            if (_current != null)
            {
                Switch(null);
            }

            if (portrait != null)
            {
                portrait.ResetPlacement();
            }

            if (_fade != null)
            {
                _fade.alpha = 0f;
                _fade.blocksRaycasts = false;
            }
        }

        private void BuildFade()
        {
            var root = new GameObject("CameraShotsFade", typeof(Canvas), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var image = new GameObject("Black", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(root.transform, false);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            image.GetComponent<Image>().color = Color.black;

            _fade = root.GetComponent<CanvasGroup>();
            _fade.alpha = 0f;
            _fade.blocksRaycasts = false;
            _fade.interactable = false;
        }
    }
}
