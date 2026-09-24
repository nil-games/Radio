using TMPro;
using UnityEngine;

namespace Radio.UI
{
    /// <summary>
    /// Показывает случайную фразу в ответ на кнопку «Не нажимать!» и плавно её гасит.
    /// </summary>
    public sealed class MenuPhrasePlayer : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Текст фразы. Шрифт обязан содержать кириллицу, иначе вместо букв будут квадраты.")]
        [SerializeField] private TextMeshProUGUI phraseLabel;

        [Tooltip("Через его прозрачность делается появление и угасание.")]
        [SerializeField] private CanvasGroup phraseGroup;

        [Header("Фразы")]
        [Tooltip("Варианты ответа. Выбирается случайный; подряд одна и та же не повторяется.")]
        [SerializeField]
        private string[] phrases =
        {
            "Это не та клавиша",
            "Не жми",
            "Здесь ничего нет",
            "Отстань от этой кнопки!",
            "Я же просил",
            "Ну и зачем?",
            "Последнее предупреждение",
            "Ты серьёзно?",
            "Тут правда ничего нет",
            "Хватит",
        };

        [Header("Тайминги")]
        [Tooltip("Время появления, с. Короткое: реакция должна читаться как мгновенная.")]
        [SerializeField] private float fadeInTime = 0.15f;

        [Tooltip("Сколько фраза висит непрозрачной, с.")]
        [SerializeField] private float holdTime = 1.6f;

        [Tooltip("Время угасания, с. Длиннее появления, чтобы фраза растворялась, а не мигала.")]
        [SerializeField] private float fadeOutTime = 0.5f;

        private int _lastIndex = -1;
        private float _elapsed;
        private bool _playing;

        private void Awake()
        {
            if (phraseLabel == null || phraseGroup == null)
            {
                Debug.LogError($"{nameof(MenuPhrasePlayer)}: не заданы текст фразы или группа прозрачности.", this);
                enabled = false;
                return;
            }

            phraseGroup.alpha = 0f;
            phraseGroup.interactable = false;
            phraseGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// Показать новую фразу. Повторный вызов перезапускает показ с новой фразой,
        /// а не ставит в очередь: очередь сделала бы кнопку «залипающей».
        /// </summary>
        public void Play()
        {
            if (phrases == null || phrases.Length == 0)
            {
                return;
            }

            phraseLabel.text = PickPhrase();
            _elapsed = 0f;
            _playing = true;
        }

        private string PickPhrase()
        {
            if (phrases.Length == 1)
            {
                return phrases[0];
            }

            // Первый показ: берём из полного списка.
            if (_lastIndex < 0)
            {
                _lastIndex = Random.Range(0, phrases.Length);
                return phrases[_lastIndex];
            }

            // Дальше тянем из списка без предыдущего варианта и сдвигаем индекс:
            // так повтор подряд невозможен и не нужен цикл «перекинь, если совпало».
            var index = Random.Range(0, phrases.Length - 1);

            if (index >= _lastIndex)
            {
                index++;
            }

            _lastIndex = index;
            return phrases[index];
        }

        private void Update()
        {
            if (!_playing)
            {
                return;
            }

            // unscaledDeltaTime, чтобы показ пережил будущую паузу.
            _elapsed += Time.unscaledDeltaTime;

            var fadeOutStart = fadeInTime + holdTime;
            float alpha;

            if (_elapsed < fadeInTime)
            {
                alpha = _elapsed / Mathf.Max(0.0001f, fadeInTime);
            }
            else if (_elapsed < fadeOutStart)
            {
                alpha = 1f;
            }
            else if (_elapsed < fadeOutStart + fadeOutTime)
            {
                alpha = 1f - (_elapsed - fadeOutStart) / Mathf.Max(0.0001f, fadeOutTime);
            }
            else
            {
                alpha = 0f;
                _playing = false;
            }

            phraseGroup.alpha = alpha;
        }
    }
}
