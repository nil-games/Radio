using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Yarn.Unity;

namespace Radio.Dialogue
{
    /// <summary>
    /// Поле ввода цифр посреди разговора: <c>&lt;&lt;ask_number zina_number&gt;&gt;</c> просит
    /// игрока набрать номер и кладёт ответ строкой в переменную Yarn <c>$zina_number</c>.
    /// Разговор ждёт, пока игрок не подтвердит ввод.
    /// </summary>
    /// <remarks>
    /// Цифры набираются с клавиатуры (верхний ряд или цифровой блок), Backspace стирает,
    /// Enter подтверждает. Полноценное поле ввода мышью здесь не нужно: номер короткий,
    /// а клавиатура у игрока и так под рукой.
    /// </remarks>
    public sealed class NumberPrompt : MonoBehaviour
    {
        [SerializeField] private DialogueRunner runner;

        [Tooltip("Сколько цифр в номере.")]
        [SerializeField] private int digits = 6;

        [SerializeField] private string title = "Введите номер";

        [SerializeField] private string hint = "Цифры — ввод · Backspace — стереть · Enter — готово";

        [Tooltip("Шрифт. Обязан содержать кириллицу.")]
        [SerializeField] private TMP_FontAsset font;

        [Tooltip("Порядок холста: поверх окна разговора (у него 10).")]
        [SerializeField] private int sortingOrder = 30;

        // Списком, а не Key.Digit0 + i: в Input System верхний ряд идёт 1…9, 0,
        // и сложение сдвинуло бы все цифры кроме нуля.
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

        private GameObject _root;
        private TextMeshProUGUI _value;
        private string _typed = string.Empty;
        private bool _active;

        private void Awake()
        {
            if (runner == null)
            {
                runner = FindFirstObjectByType<DialogueRunner>();
            }

            Build();

            if (runner != null)
            {
                runner.AddCommandHandler("ask_number", (System.Func<string, YarnTask>)Ask);
            }
        }

        private async YarnTask Ask(string variable)
        {
            _typed = string.Empty;
            _active = true;
            _root.SetActive(true);
            Refresh();

            while (_active)
            {
                await YarnTask.Yield();
            }

            _root.SetActive(false);

            if (runner != null && runner.VariableStorage != null)
            {
                runner.VariableStorage.SetValue("$" + variable, _typed);
            }
        }

        private void Update()
        {
            if (!_active)
            {
                return;
            }

            var keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (keyboard.backspaceKey.wasPressedThisFrame && _typed.Length > 0)
            {
                _typed = _typed.Substring(0, _typed.Length - 1);
                Refresh();
                return;
            }

            if ((keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                && _typed.Length == digits)
            {
                _active = false;
                return;
            }

            if (_typed.Length >= digits)
            {
                return;
            }

            for (var i = 0; i <= 9; i++)
            {
                if (keyboard[DigitKeys[i]].wasPressedThisFrame || keyboard[NumpadKeys[i]].wasPressedThisFrame)
                {
                    _typed += (char)('0' + i);
                    Refresh();
                    return;
                }
            }
        }

        private void Refresh()
        {
            // Пары цифр через пробел — так номер пишут в объявлениях: 03 20 00.
            var shown = _typed.PadRight(digits, '_');
            var spaced = new System.Text.StringBuilder();

            for (var i = 0; i < shown.Length; i++)
            {
                if (i > 0 && i % 2 == 0)
                {
                    spaced.Append(' ');
                }

                spaced.Append(shown[i]);
            }

            _value.text = $"<size=70%>{title}</size>\n{spaced}\n<size=45%><alpha=#99>{hint}</size>";
        }

        private void Build()
        {
            // Строится кодом, как и остальной интерфейс проекта.
            _root = new GameObject("NumberPrompt", typeof(RectTransform));
            _root.transform.SetParent(transform, false);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject("Panel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(_root.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.72f);
            rect.sizeDelta = new Vector2(760f, 220f);
            panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            var label = new GameObject("Value", typeof(RectTransform));
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

            _value = label.AddComponent<TextMeshProUGUI>();
            _value.font = font;
            _value.fontSize = 56f;
            _value.alignment = TextAlignmentOptions.Center;
            _value.color = Color.white;
            _value.raycastTarget = false;

            _root.SetActive(false);
        }
    }
}
