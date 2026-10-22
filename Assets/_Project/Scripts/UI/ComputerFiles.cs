using System;
using System.Collections.Generic;
using System.Text;
using Radio.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Radio.UI
{
    /// <summary>
    /// «Файлы» на компьютере в студии: список записей, часть из них под паролем.
    /// Пароль набирается прямо с клавиатуры, открытая запись запоминается флагом мира.
    /// </summary>
    /// <remarks>
    /// Поле ввода своё, а не TMP_InputField: экран компьютера — холст в мире, и штатному
    /// полю пришлось бы отдельно ловить фокус и камеру. Здесь символы берутся из
    /// <see cref="Keyboard.onTextInput"/> — он уже учитывает раскладку, так что кириллица
    /// набирается как есть.
    /// Пароль сравнивается без учёта регистра, пробелов, подчёркиваний и «ё»: «не спи»,
    /// «неспи» и «НЕ_СПИ» — одно и то же. Игрок угадывает смысл, а не написание.
    /// </remarks>
    public sealed class ComputerFiles : ComputerProgram
    {
        [Serializable]
        public struct Entry
        {
            public string title;

            [Tooltip("Пароль. Пусто — запись открыта всем. Сравнивается без регистра, пробелов, «_» и «ё».")]
            public string password;

            [Tooltip("Флаг мира, который ставится, когда запись открыли. Открытую запись " +
                     "больше не запирают. Пусто — пароль спрашивается каждый раз.")]
            public string unlockedFlag;

            [TextArea(3, 12)]
            public string text;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        [SerializeField] private string emptyText = "Папка пуста.";

        [Header("Вид")]
        [Tooltip("Шрифт. Обязан содержать кириллицу.")]
        [SerializeField] private TMP_FontAsset font;

        [SerializeField] private float rowHeight = 56f;
        [SerializeField] private Color rowColor = new Color(1f, 1f, 1f, 0.08f);
        [SerializeField] private Color textColor = new Color(0.9f, 0.93f, 0.88f, 1f);
        [SerializeField] private Color accentColor = new Color(1f, 0.85f, 0.55f, 1f);
        [SerializeField] private Color errorColor = new Color(1f, 0.45f, 0.45f, 1f);

        [SerializeField] private int maxPasswordLength = 32;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly StringBuilder _typed = new StringBuilder();
        private TextMeshProUGUI _body;
        private TextMeshProUGUI _inputLabel;
        private TextMeshProUGUI _status;
        private int _prompting = -1;
        private bool _listening;

        public override void Open(TextMeshProUGUI body)
        {
            _body = body;
            ShowList();
        }

        public override void Close()
        {
            StopListening();
            Clear();
            _body = null;
        }

        private void OnDisable() => StopListening();

        private void ShowList()
        {
            StopListening();
            Clear();
            _body.text = entries.Length == 0 ? emptyText : string.Empty;

            for (var i = 0; i < entries.Length; i++)
            {
                CreateRow(i);
            }
        }

        private void OpenEntry(int index)
        {
            if (IsLocked(entries[index]))
            {
                ShowPasswordPrompt(index);
                return;
            }

            ShowText(index);
        }

        private void ShowText(int index)
        {
            StopListening();
            Clear();
            _body.text = $"<color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>{entries[index].title}</color>\n\n{entries[index].text}";
            CreateButton("FilesBack", "← Файлы", new Vector2(1f, 0f), ShowList);
        }

        private void ShowPasswordPrompt(int index)
        {
            Clear();
            _prompting = index;
            _typed.Clear();
            _body.text = $"<color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>{entries[index].title}</color>\n\n" +
                         "Запись защищена паролем.\nВведите пароль:";

            // Строка ввода — под текстом приглашения.
            var field = CreateRect("PasswordField");
            field.anchorMin = new Vector2(0f, 1f);
            field.anchorMax = new Vector2(1f, 1f);
            field.pivot = new Vector2(0.5f, 1f);
            field.sizeDelta = new Vector2(0f, 48f);
            field.anchoredPosition = new Vector2(0f, -150f);
            field.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            _inputLabel = CreateLabel(field, 24f);
            _inputLabel.margin = new Vector4(14f, 0f, 14f, 0f);
            _inputLabel.alignment = TextAlignmentOptions.MidlineLeft;

            var statusRect = CreateRect("PasswordStatus");
            statusRect.anchorMin = new Vector2(0f, 1f);
            statusRect.anchorMax = new Vector2(1f, 1f);
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.sizeDelta = new Vector2(0f, 32f);
            statusRect.anchoredPosition = new Vector2(0f, -206f);
            _status = CreateLabel(statusRect, 20f);
            _status.alignment = TextAlignmentOptions.MidlineLeft;
            _status.text = "<alpha=#88>Enter — открыть";

            CreateButton("FilesBack", "← Файлы", new Vector2(1f, 0f), ShowList);
            CreateButton("FilesOpen", "Открыть", new Vector2(0f, 0f), Submit);

            RefreshInput();
            StartListening();
        }

        private void OnTextInput(char c)
        {
            if (_prompting < 0)
            {
                return;
            }

            if (c == '\b')
            {
                if (_typed.Length > 0)
                {
                    _typed.Length--;
                }
            }
            else if (c == '\r' || c == '\n')
            {
                Submit();
                return;
            }
            else if (!char.IsControl(c) && _typed.Length < maxPasswordLength)
            {
                _typed.Append(c);
            }

            RefreshInput();
        }

        private void Submit()
        {
            if (_prompting < 0)
            {
                return;
            }

            var index = _prompting;

            if (Normalize(_typed.ToString()) != Normalize(entries[index].password))
            {
                _typed.Clear();
                RefreshInput();
                _status.text = $"<color=#{ColorUtility.ToHtmlStringRGB(errorColor)}>Неверный пароль</color>";
                return;
            }

            var flag = entries[index].unlockedFlag;

            if (!string.IsNullOrWhiteSpace(flag) && GameSession.Current != null)
            {
                GameSession.Current.World.Set(flag, true);
            }

            ShowText(index);
        }

        private void RefreshInput()
        {
            if (_inputLabel == null)
            {
                return;
            }

            // Пароль видно как есть: игрок подбирает слова, звёздочки только мешали бы.
            _inputLabel.text = _typed + "<alpha=#AA>_";
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var result = new StringBuilder(value.Length);

            foreach (var c in value.ToLowerInvariant())
            {
                if (char.IsWhiteSpace(c) || c == '_' || c == '-')
                {
                    continue;
                }

                result.Append(c == 'ё' ? 'е' : c);
            }

            return result.ToString();
        }

        private static bool IsLocked(Entry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.password))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(entry.unlockedFlag))
            {
                return true;
            }

            var session = GameSession.Current;
            return session == null || !session.World.TryGet<bool>(entry.unlockedFlag, out var open) || !open;
        }

        private void StartListening()
        {
            if (_listening || Keyboard.current == null)
            {
                return;
            }

            Keyboard.current.onTextInput += OnTextInput;
            _listening = true;
        }

        private void StopListening()
        {
            _prompting = -1;

            if (!_listening)
            {
                return;
            }

            if (Keyboard.current != null)
            {
                Keyboard.current.onTextInput -= OnTextInput;
            }

            _listening = false;
        }

        private void CreateRow(int index)
        {
            var entry = entries[index];
            var rect = CreateRect("File_" + index);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, rowHeight);
            rect.anchoredPosition = new Vector2(0f, -index * (rowHeight + 6f));

            var image = rect.gameObject.AddComponent<Image>();
            image.color = rowColor;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => OpenEntry(index));

            var label = CreateLabel(rect, 22f);
            label.margin = new Vector4(14f, 0f, 14f, 0f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var mark = IsLocked(entry)
                ? $"   <color=#{ColorUtility.ToHtmlStringRGB(accentColor)}><size=80%>пароль</size></color>"
                : string.Empty;
            label.text = entry.title + mark;
        }

        private void CreateButton(string name, string text, Vector2 corner, UnityEngine.Events.UnityAction action)
        {
            var rect = CreateRect(name);
            rect.anchorMin = corner;
            rect.anchorMax = corner;
            rect.pivot = corner;
            rect.sizeDelta = new Vector2(170f, 40f);
            rect.anchoredPosition = Vector2.zero;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = rowColor;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            var label = CreateLabel(rect, 20f);
            label.alignment = TextAlignmentOptions.Center;
            label.text = text;
        }

        /// <summary>Всё содержимое ложится внутрь текста окна, в ту же рамку, что и у почты.</summary>
        private RectTransform CreateRect(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_body.rectTransform, false);
            _spawned.Add(go);
            return rect;
        }

        private TextMeshProUGUI CreateLabel(RectTransform parent, float size)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = font != null ? font : _body.font;
            label.fontSize = size;
            label.color = textColor;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private void Clear()
        {
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Destroy(go);
                }
            }

            _spawned.Clear();
            _inputLabel = null;
            _status = null;
        }
    }
}
