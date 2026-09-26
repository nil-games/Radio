using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Radio.Minigames
{
    /// <summary>
    /// Рисует мини-игру «Гитара»: дорожку, по которой к линии попадания едут буквы,
    /// строку песни с подсветкой спетого, счёт и итог.
    /// </summary>
    /// <remarks>
    /// Всё поле собирается кодом внутри <see cref="root"/>, как и в «Проводе»:
    /// сто с лишним карточек руками не расставить, а размеры проще держать в одном месте.
    /// </remarks>
    public sealed class KaraokeView : MonoBehaviour
    {
        public static readonly Color PerfectColor = new Color(0.45f, 0.95f, 0.55f);
        public static readonly Color GoodColor = new Color(0.95f, 0.85f, 0.4f);
        public static readonly Color MissColor = new Color(1f, 0.45f, 0.45f);

        [Header("Ссылки")]
        [Tooltip("Контейнер во весь экран. Поле строится внутри него.")]
        [SerializeField] private RectTransform root;

        [Tooltip("Шрифт подписей. Обязан содержать кириллицу.")]
        [SerializeField] private TMP_FontAsset font;

        [Header("Размеры")]
        [SerializeField] private Vector2 trackSize = new Vector2(1400f, 150f);

        [Tooltip("Где на дорожке линия попадания, пикселей от левого края.")]
        [SerializeField] private float hitLineX = 180f;

        [SerializeField] private Vector2 cardSize = new Vector2(92f, 112f);

        [Header("Цвета")]
        [SerializeField] private Color backdropColor = new Color(0f, 0f, 0f, 0.6f);
        [SerializeField] private Color trackColor = new Color(1f, 1f, 1f, 0.06f);
        [SerializeField] private Color hitLineColor = new Color(1f, 1f, 1f, 0.8f);
        [SerializeField] private Color cardColor = new Color(0.36f, 0.35f, 0.86f, 1f);
        [SerializeField] private Color cardNextColor = new Color(1f, 0.38f, 0.75f, 1f);
        [SerializeField] private Color sungColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color unsungColor = new Color(1f, 1f, 1f, 0.35f);

        private sealed class Card
        {
            public RectTransform Rect;
            public Image Background;
            public TextMeshProUGUI Letter;
            public TextMeshProUGUI Latin;
            public KaraokeGame.Judgement Judgement;
        }

        private readonly List<Card> _cards = new List<Card>();
        private readonly StringBuilder _text = new StringBuilder(256);

        private RectTransform _track;
        private RectTransform _cardsRoot;
        private Image _hitLine;
        private TextMeshProUGUI _currentLine;
        private TextMeshProUGUI _nextLine;
        private TextMeshProUGUI _score;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _hint;
        private TextMeshProUGUI _results;
        private bool _built;

        private KaraokeSong.Note[] _notes;
        private string[][] _lines;
        private float[][] _wordTimes;
        private float _leadTime;

        private int _shownLine = -1;
        private int _shownSung = -1;
        private float _flash;

        /// <summary>Готовит поле под песню. Зовётся на каждый запуск.</summary>
        public void Build(KaraokeSong.Note[] notes, string[][] lines, float leadTime)
        {
            if (root == null || font == null)
            {
                Debug.LogError($"{nameof(KaraokeView)}: не заданы контейнер или шрифт.", this);
                return;
            }

            if (!_built)
            {
                BuildStatic();
                _built = true;
            }

            _notes = notes;
            _lines = lines;
            _leadTime = leadTime;
            _shownLine = -1;
            _shownSung = -1;
            _flash = 0f;

            BuildWordTimes();
            BuildCards();

            _results.gameObject.SetActive(false);
            _track.gameObject.SetActive(true);
            _currentLine.gameObject.SetActive(true);
            _nextLine.gameObject.SetActive(true);
            _hint.text = "ЛКМ / ПКМ — выйти";
        }

        public void Render(float time, int firstPending)
        {
            if (_notes == null)
            {
                return;
            }

            RenderCards(time, firstPending);
            RenderLyrics(time, firstPending);

            _flash = Mathf.Max(0f, _flash - Time.unscaledDeltaTime * 4f);
            _hitLine.rectTransform.sizeDelta = new Vector2(4f + 8f * _flash, trackSize.y);
        }

        public void MarkNote(int index, KaraokeGame.Judgement judgement)
        {
            var card = _cards[index];
            card.Judgement = judgement;

            if (judgement == KaraokeGame.Judgement.Miss)
            {
                card.Background.color = MissColor * new Color(1f, 1f, 1f, 0.5f);
                return;
            }

            // Отыгранная буква пропадает сразу, а линия вспыхивает: взгляд должен
            // уйти к следующей, а не задерживаться на той, что уже засчитана.
            card.Rect.gameObject.SetActive(false);
            _hitLine.color = judgement == KaraokeGame.Judgement.Perfect ? PerfectColor : GoodColor;
            _flash = 1f;
        }

        public void SetScore(int hits, int total, int streak)
        {
            _score.text = streak > 1
                ? $"{hits} / {total}   серия {streak}"
                : $"{hits} / {total}";
        }

        public void SetStatus(string text, Color color)
        {
            _status.text = text;
            _status.color = color;
        }

        public void ShowResults(int perfect, int good, int total, int bestStreak)
        {
            var hits = perfect + good;
            var percent = total > 0 ? Mathf.RoundToInt(100f * hits / total) : 0;

            _track.gameObject.SetActive(false);
            _currentLine.gameObject.SetActive(false);
            _nextLine.gameObject.SetActive(false);
            _status.text = string.Empty;
            _score.text = string.Empty;

            _results.gameObject.SetActive(true);
            _results.text =
                $"<size=64>{percent}%</size>\n" +
                $"попаданий {hits} из {total}\n" +
                $"точно {perfect}   хорошо {good}\n" +
                $"лучшая серия {bestStreak}";

            _hint.text = "Щёлкни мышью, чтобы выйти";
        }

        private void RenderCards(float time, int firstPending)
        {
            var travel = trackSize.x - hitLineX - cardSize.x * 0.5f;

            for (var i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];

                if (card.Judgement == KaraokeGame.Judgement.Perfect || card.Judgement == KaraokeGame.Judgement.Good)
                {
                    continue;
                }

                var ahead = _notes[i].Time - time;
                var visible = ahead < _leadTime && ahead > -0.4f;
                card.Rect.gameObject.SetActive(visible);

                if (!visible)
                {
                    continue;
                }

                card.Rect.anchoredPosition = new Vector2(hitLineX + ahead / _leadTime * travel, 0f);

                if (card.Judgement == KaraokeGame.Judgement.None)
                {
                    card.Background.color = i == firstPending ? cardNextColor : cardColor;
                }
            }
        }

        private void RenderLyrics(float time, int firstPending)
        {
            // Показываем строку следующего неотыгранного слова: как только строка допета,
            // на её место встаёт следующая, и игрок видит, что его ждёт.
            var noteIndex = Mathf.Min(firstPending, _notes.Length - 1);
            var line = _notes[noteIndex].Line;
            var times = _wordTimes[line];

            var sung = 0;

            while (sung < times.Length && times[sung] <= time)
            {
                sung++;
            }

            if (line == _shownLine && sung == _shownSung)
            {
                return;
            }

            _shownLine = line;
            _shownSung = sung;

            _text.Clear();
            var words = _lines[line];

            for (var w = 0; w < words.Length; w++)
            {
                if (w > 0)
                {
                    _text.Append(' ');
                }

                _text.Append("<color=#")
                    .Append(ColorUtility.ToHtmlStringRGBA(w < sung ? sungColor : unsungColor))
                    .Append('>')
                    .Append(words[w])
                    .Append("</color>");
            }

            _currentLine.text = _text.ToString();
            _nextLine.text = line + 1 < _lines.Length ? string.Join(" ", _lines[line + 1]) : string.Empty;
        }

        private void BuildWordTimes()
        {
            _wordTimes = new float[_lines.Length][];

            for (var l = 0; l < _lines.Length; l++)
            {
                _wordTimes[l] = new float[_lines[l].Length];

                for (var w = 0; w < _wordTimes[l].Length; w++)
                {
                    _wordTimes[l][w] = float.PositiveInfinity;
                }
            }

            foreach (var note in _notes)
            {
                _wordTimes[note.Line][note.WordInLine] = note.Time;
            }

            // Слово без ноты (буквы нет на клавиатуре) загорается вместе с предыдущим,
            // иначе подсветка строки застряла бы на нём навсегда.
            foreach (var times in _wordTimes)
            {
                for (var w = 1; w < times.Length; w++)
                {
                    if (float.IsPositiveInfinity(times[w]))
                    {
                        times[w] = times[w - 1];
                    }
                }
            }
        }

        private void BuildCards()
        {
            for (var i = _cardsRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_cardsRoot.GetChild(i).gameObject);
            }

            _cards.Clear();

            foreach (var note in _notes)
            {
                var rect = CreateRect("Card", _cardsRoot, new Vector2(0f, 0.5f), Vector2.zero, cardSize);
                var background = rect.gameObject.AddComponent<Image>();
                background.color = cardColor;

                var letter = CreateText("Letter", rect, note.Letter, 64f, Color.white);
                letter.rectTransform.anchoredPosition = new Vector2(0f, 10f);

                var latin = CreateText("Latin", rect, note.Latin, 20f, new Color(1f, 1f, 1f, 0.55f));
                latin.rectTransform.anchoredPosition = new Vector2(0f, -38f);

                rect.gameObject.SetActive(false);

                _cards.Add(new Card { Rect = rect, Background = background, Letter = letter, Latin = latin });
            }
        }

        private void BuildStatic()
        {
            var backdrop = CreateRect("Backdrop", root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.gameObject.AddComponent<Image>().color = backdropColor;

            _track = CreateRect("Track", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), trackSize);
            _track.gameObject.AddComponent<Image>().color = trackColor;

            // Пропущенные буквы уезжают за линию и дальше: обрезаем их по краю дорожки,
            // чтобы они не налезали на подписи вокруг.
            _track.gameObject.AddComponent<RectMask2D>();

            _cardsRoot = CreateRect("Cards", _track, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            _cardsRoot.pivot = new Vector2(0f, 0.5f);

            var hitLine = CreateRect("HitLine", _track, new Vector2(0f, 0.5f), new Vector2(hitLineX, 0f),
                new Vector2(4f, trackSize.y));
            _hitLine = hitLine.gameObject.AddComponent<Image>();
            _hitLine.color = hitLineColor;
            _hitLine.raycastTarget = false;

            _status = CreateText("Status", root, string.Empty, 36f, Color.white);
            SetRect(_status.rectTransform, new Vector2(-trackSize.x * 0.5f + hitLineX, 80f + trackSize.y * 0.5f + 34f),
                new Vector2(300f, 50f));

            _score = CreateText("Score", root, string.Empty, 30f, new Color(1f, 1f, 1f, 0.8f));
            _score.alignment = TextAlignmentOptions.Right;
            SetRect(_score.rectTransform, new Vector2(trackSize.x * 0.5f - 250f, 80f + trackSize.y * 0.5f + 34f),
                new Vector2(500f, 50f));

            _currentLine = CreateText("CurrentLine", root, string.Empty, 46f, Color.white);
            SetRect(_currentLine.rectTransform, new Vector2(0f, -60f), new Vector2(1600f, 64f));

            _nextLine = CreateText("NextLine", root, string.Empty, 30f, new Color(1f, 1f, 1f, 0.3f));
            SetRect(_nextLine.rectTransform, new Vector2(0f, -120f), new Vector2(1600f, 44f));

            _results = CreateText("Results", root, string.Empty, 40f, Color.white);
            SetRect(_results.rectTransform, new Vector2(0f, 40f), new Vector2(1000f, 360f));

            _hint = CreateText("Hint", root, string.Empty, 24f, new Color(1f, 1f, 1f, 0.5f));
            SetRect(_hint.rectTransform, new Vector2(0f, -420f), new Vector2(800f, 40f));
        }

        private TextMeshProUGUI CreateText(string name, RectTransform parent, string text, float size, Color color)
        {
            var rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 80f));
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
