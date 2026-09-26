using System;
using System.Collections.Generic;
using Radio.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Radio.UI
{
    /// <summary>
    /// Почта на компьютере в студии: список писем-вкладок, по клику — письмо целиком.
    /// Письмо появляется, когда поставлен его флаг мира, так что сюжет «присылает» почту
    /// обычным флагом, без отдельной команды.
    /// </summary>
    /// <remarks>
    /// Вкладки строятся кодом при каждом открытии: писем немного, а список обязан
    /// совпадать с флагами на момент открытия, а не на момент первого запуска.
    /// </remarks>
    public sealed class ComputerMail : ComputerProgram
    {
        [Serializable]
        public struct Message
        {
            [Tooltip("Флаг мира, после которого письмо лежит в ящике. Пусто — лежит всегда.")]
            public string arrivedFlag;

            [Tooltip("Флаг мира, который ставится, когда письмо открыли. Пусто — никакого.")]
            public string readFlag;

            [Tooltip("Задача, которая снимается, когда письмо открыли. Пусто — никакая.")]
            public string completesTask;

            public string from;
            public string subject;

            [Tooltip("Время отправления, как его видит игрок: «01:00».")]
            public string sentAt;

            [TextArea(6, 20)]
            public string text;
        }

        [Header("Письма")]
        [SerializeField] private Message[] messages = Array.Empty<Message>();

        [SerializeField] private string emptyText = "Новых сообщений нет.";

        [Header("Вид")]
        [Tooltip("Шрифт вкладок. Обязан содержать кириллицу.")]
        [SerializeField] private TMP_FontAsset font;

        [SerializeField] private float rowHeight = 64f;
        [SerializeField] private Color rowColor = new Color(1f, 1f, 1f, 0.08f);
        [SerializeField] private Color rowNewColor = new Color(0.16f, 0.36f, 0.34f, 0.9f);
        [SerializeField] private Color textColor = new Color(0.9f, 0.93f, 0.88f, 1f);
        [SerializeField] private Color accentColor = new Color(1f, 0.85f, 0.55f, 1f);

        [Header("Ссылки")]
        [Tooltip("Если пусто, ищется в сцене.")]
        [SerializeField] private TaskLog tasks;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private TextMeshProUGUI _body;
        private float _bodyFontSize;
        private bool _bodyAutoSize;
        private Vector4 _bodyMargin;

        public override void Open(TextMeshProUGUI body)
        {
            _body = body;
            _bodyFontSize = body.fontSize;
            _bodyAutoSize = body.enableAutoSizing;
            _bodyMargin = body.margin;
            ShowInbox();
        }

        public override void Close()
        {
            Clear();

            // Окно общее для всех значков: вернуть тексту тот вид, в котором его взяли.
            if (_body != null)
            {
                RestoreBody();
                _body = null;
            }
        }

        private void ShowInbox()
        {
            Clear();
            RestoreBody();
            _body.text = string.Empty;

            var shown = 0;

            for (var i = 0; i < messages.Length; i++)
            {
                if (!HasArrived(messages[i]))
                {
                    continue;
                }

                CreateRow(i, shown);
                shown++;
            }

            if (shown == 0)
            {
                _body.text = emptyText;
            }
        }

        private void ShowMessage(int index)
        {
            var message = messages[index];
            Clear();

            // Письмо длиннее окна при обычном кегле: пусть текст сам ужмётся, а не обрежется.
            _body.enableAutoSizing = true;
            _body.fontSizeMin = 12f;
            _body.fontSizeMax = _bodyFontSize;

            // Снизу место под кнопку «Входящие», чтобы она не легла на последние строки.
            _body.margin = _bodyMargin + new Vector4(0f, 0f, 0f, 48f);
            _body.text =
                $"<color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>От:</color> {message.from}\n" +
                $"<color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>Тема:</color> {message.subject}\n" +
                $"<color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>Время отправления:</color> {message.sentAt}\n\n" +
                message.text;

            CreateBackButton();
            MarkRead(message);
        }

        private void RestoreBody()
        {
            _body.enableAutoSizing = _bodyAutoSize;
            _body.fontSize = _bodyFontSize;
            _body.margin = _bodyMargin;
        }

        private bool HasArrived(Message message)
        {
            if (string.IsNullOrWhiteSpace(message.arrivedFlag))
            {
                return true;
            }

            var session = GameSession.Current;
            return session != null && session.World.TryGet<bool>(message.arrivedFlag, out var arrived) && arrived;
        }

        private static bool IsRead(Message message)
        {
            if (string.IsNullOrWhiteSpace(message.readFlag))
            {
                return false;
            }

            var session = GameSession.Current;
            return session != null && session.World.TryGet<bool>(message.readFlag, out var read) && read;
        }

        private void MarkRead(Message message)
        {
            var session = GameSession.Current;

            if (!string.IsNullOrWhiteSpace(message.readFlag) && session != null)
            {
                session.World.Set(message.readFlag, true);
            }

            if (string.IsNullOrWhiteSpace(message.completesTask))
            {
                return;
            }

            if (tasks == null)
            {
                tasks = FindAnyObjectByType<TaskLog>();
            }

            if (tasks != null)
            {
                tasks.Complete(message.completesTask);
            }
        }

        private void CreateRow(int index, int position)
        {
            var message = messages[index];
            var isNew = !IsRead(message);

            var rect = CreateRect("Mail_" + index);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, rowHeight);
            rect.anchoredPosition = new Vector2(0f, -position * (rowHeight + 6f));

            var image = rect.gameObject.AddComponent<Image>();
            image.color = isNew ? rowNewColor : rowColor;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => ShowMessage(index));

            var label = CreateLabel(rect, 20f);
            label.margin = new Vector4(14f, 6f, 14f, 6f);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            var marker = isNew
                ? $"<color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>НОВОЕ СООБЩЕНИЕ</color>   "
                : string.Empty;
            label.text = $"{marker}{message.sentAt}\n<b>{message.subject}</b>   <alpha=#AA>{message.from}";
        }

        private void CreateBackButton()
        {
            var rect = CreateRect("MailBack");
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(170f, 40f);
            rect.anchoredPosition = Vector2.zero;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = rowColor;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(ShowInbox);

            var label = CreateLabel(rect, 20f);
            label.alignment = TextAlignmentOptions.Center;
            label.text = "← Входящие";
        }

        /// <summary>
        /// Вкладки ложатся поверх текста окна, в той же рамке: так почта занимает
        /// ровно то место, которое занимал бы текст любой другой программы.
        /// </summary>
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
        }
    }
}
