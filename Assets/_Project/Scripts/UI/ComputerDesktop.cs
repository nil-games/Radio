using System;
using Radio.Interaction;
using Radio.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Radio.UI
{
    /// <summary>
    /// Рабочий стол компьютера в студии: значки, окно с содержимым и часы смены.
    /// Живёт на холсте, который лежит на месте стекла монитора.
    /// </summary>
    /// <remarks>
    /// Это оболочка, а не операционная система: значок открывает одно и то же окно
    /// с разным заголовком и текстом. Пока содержимое программ не написано, семь
    /// отдельных окон отличались бы друг от друга только строкой текста.
    /// </remarks>
    public sealed class ComputerDesktop : MonoBehaviour
    {
        [Serializable]
        public struct DesktopIcon
        {
            [Tooltip("Подпись под значком.")]
            public string title;

            [Tooltip("Кнопка значка на рабочем столе.")]
            public Button button;

            [Tooltip("Что покажется в окне. Позже заменится на настоящее содержимое программы.")]
            [TextArea(2, 6)]
            public string body;
        }

        [Header("Значки")]
        [SerializeField] private DesktopIcon[] icons = Array.Empty<DesktopIcon>();

        [Header("Окно")]
        [SerializeField] private GameObject window;
        [SerializeField] private TextMeshProUGUI windowTitle;
        [SerializeField] private TextMeshProUGUI windowBody;
        [SerializeField] private Button windowCloseButton;

        [Header("Панель задач")]
        [Tooltip("Часы смены в углу. Берут время из GameSession.")]
        [SerializeField] private TextMeshProUGUI clock;

        [Header("Выход")]
        [Tooltip("Кнопка «отойти от монитора».")]
        [SerializeField] private Button leaveButton;

        [SerializeField] private ScreenFocus screen;

        private bool _initialized;

        private void Awake() => EnsureInitialized();

        private void OnEnable()
        {
            EnsureInitialized();

            // Каждый подход к монитору начинается с чистого стола: окно, открытое
            // в прошлый раз, иначе встречало бы игрока уже развёрнутым.
            CloseWindow();
            RefreshClock();
        }

        /// <summary>
        /// Подписки идемпотентны и делаются из Awake и из OnEnable: холст выключен
        /// в сцене, и до первого включения Awake на нём не отработает.
        /// </summary>
        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            if (window == null || windowTitle == null || windowBody == null)
            {
                Debug.LogError($"{nameof(ComputerDesktop)}: не задано окно программы.", this);
                enabled = false;
                return;
            }

            _initialized = true;

            for (var i = 0; i < icons.Length; i++)
            {
                if (icons[i].button == null)
                {
                    continue;
                }

                // Копия индекса: без неё все значки открыли бы последнюю программу.
                var index = i;
                icons[i].button.onClick.AddListener(() => OpenWindow(index));
            }

            if (windowCloseButton != null)
            {
                windowCloseButton.onClick.AddListener(CloseWindow);
            }

            if (leaveButton != null)
            {
                leaveButton.onClick.AddListener(Leave);
            }
        }

        private void Update()
        {
            // Часы смены стоят, пока игрок бездействует, но меняются от его действий,
            // поэтому дешевле обновлять строку, чем подписываться и ловить все случаи.
            RefreshClock();
        }

        private void RefreshClock()
        {
            if (clock == null)
            {
                return;
            }

            var session = GameSession.Current;
            clock.text = session != null ? session.Time.Clock : "--:--";
        }

        private void OpenWindow(int index)
        {
            if (index < 0 || index >= icons.Length)
            {
                return;
            }

            windowTitle.text = icons[index].title;
            windowBody.text = icons[index].body;
            window.SetActive(true);
        }

        private void CloseWindow() => window.SetActive(false);

        private void Leave()
        {
            if (screen != null)
            {
                screen.Leave();
            }
        }
    }
}
