using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Radio.UI
{
    /// <summary>
    /// Интерфейс режима «за столом»: стрелки поворота по краям экрана и строка подсказки.
    /// Показывается, только пока игрок сидит.
    /// Внешний вид целиком задаётся в префабе — код лишь связывает кнопки с логикой.
    /// </summary>
    public sealed class DeskModeUI : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Корень панели. Гасится целиком, когда игрок встаёт.")]
        [SerializeField] private GameObject panel;

        [Tooltip("Кнопка поворота к левой половине стола.")]
        [SerializeField] private Button leftArrow;

        [Tooltip("Кнопка возврата к правой половине стола.")]
        [SerializeField] private Button rightArrow;

        [Tooltip("Строка подсказки. Сюда пойдут сообщения о приборах на столе.")]
        [SerializeField] private TextMeshProUGUI hint;

        /// <summary>Нажата стрелка влево.</summary>
        public event Action TurnLeftRequested;

        /// <summary>Нажата стрелка вправо.</summary>
        public event Action TurnRightRequested;

        private bool _initialized;

        private void Awake() => EnsureInitialized();

        private void OnEnable() => EnsureInitialized();

        private void OnDestroy()
        {
            if (leftArrow != null)
            {
                leftArrow.onClick.RemoveListener(RaiseTurnLeft);
            }

            if (rightArrow != null)
            {
                rightArrow.onClick.RemoveListener(RaiseTurnRight);
            }
        }

        public void Show()
        {
            EnsureInitialized();
            panel.SetActive(true);
        }

        public void Hide()
        {
            EnsureInitialized();
            panel.SetActive(false);
        }

        /// <summary>
        /// Какие повороты сейчас доступны. Недоступная стрелка прячется целиком,
        /// а не просто гаснет: половин стола всего две, и показывать ход,
        /// которого нет, значит обманывать игрока.
        /// </summary>
        public void SetTurnAvailability(bool canTurnLeft, bool canTurnRight)
        {
            EnsureInitialized();
            leftArrow.gameObject.SetActive(canTurnLeft);
            rightArrow.gameObject.SetActive(canTurnRight);
        }

        /// <summary>Текст подсказки. Пустая строка прячет строку целиком.</summary>
        public void SetHint(string text)
        {
            if (hint == null)
            {
                return;
            }

            hint.text = text;
            hint.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>
        /// Подписки делаются здесь, а не только в Awake: панель гасится из SitPoint.Awake,
        /// а порядок вызова Awake между объектами Unity не гарантирует.
        /// </summary>
        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            if (panel == null || leftArrow == null || rightArrow == null)
            {
                Debug.LogError($"{nameof(DeskModeUI)}: не заданы панель или кнопки поворота.", this);
                enabled = false;
                return;
            }

            _initialized = true;

            leftArrow.onClick.AddListener(RaiseTurnLeft);
            rightArrow.onClick.AddListener(RaiseTurnRight);

            panel.SetActive(false);
            SetHint(string.Empty);
        }

        private void RaiseTurnLeft() => TurnLeftRequested?.Invoke();

        private void RaiseTurnRight() => TurnRightRequested?.Invoke();
    }
}
