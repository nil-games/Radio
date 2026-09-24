using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Radio.UI
{
    /// <summary>
    /// Главное меню: связывает кнопки с действиями.
    /// Внешний вид целиком задаётся в префабе.
    /// </summary>
    public sealed class MainMenuUI : MonoBehaviour
    {
        [Header("Кнопки")]
        [Tooltip("ИГРАТЬ — загружает игровую сцену.")]
        [SerializeField] private Button playButton;

        [Tooltip("ПРОДОЛЖИТЬ — пока ничего не делает, обработчик появится позже.")]
        [SerializeField] private Button continueButton;

        [Tooltip("НАСТРОЙКИ — пока ничего не делают, обработчик появится позже.")]
        [SerializeField] private Button settingsButton;

        [Tooltip("НЕ НАЖИМАТЬ! — показывает случайную фразу.")]
        [SerializeField] private Button doNotPressButton;

        [Tooltip("ВЫХОД — закрывает игру.")]
        [SerializeField] private Button quitButton;

        [Header("Ссылки")]
        [Tooltip("Показывает фразы справа от колонки кнопок.")]
        [SerializeField] private MenuPhrasePlayer phrasePlayer;

        [Header("Переходы")]
        [Tooltip("Имя игровой сцены. Она должна быть в Build Settings. " +
                 "По имени, а не по индексу: индекс поедет, как только добавят новые сцены.")]
        [SerializeField] private string gameplaySceneName = "Apartment";

        private bool _initialized;

        private void Awake() => EnsureInitialized();

        private void OnEnable() => EnsureInitialized();

        private void OnDestroy()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(StartGame);
            }

            if (doNotPressButton != null)
            {
                doNotPressButton.onClick.RemoveListener(ShowPhrase);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(QuitGame);
            }
        }

        /// <summary>
        /// Подписки идемпотентны и делаются из Awake и из OnEnable:
        /// порядок Awake между объектами Unity не гарантирует.
        /// </summary>
        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            if (playButton == null || doNotPressButton == null || quitButton == null || phrasePlayer == null)
            {
                Debug.LogError($"{nameof(MainMenuUI)}: не заданы кнопки меню или проигрыватель фраз.", this);
                enabled = false;
                return;
            }

            _initialized = true;

            playButton.onClick.AddListener(StartGame);
            doNotPressButton.onClick.AddListener(ShowPhrase);
            quitButton.onClick.AddListener(QuitGame);

            // ПРОДОЛЖИТЬ и НАСТРОЙКИ намеренно остаются без обработчиков и включёнными:
            // штатная подсветка выключенной кнопки роняет плашку до половинной
            // прозрачности и обесцвечивает светящийся текст — на тёмном фоне это
            // читается как «картинка не загрузилась», а не «пока недоступно».
        }

        private void StartGame() => SceneManager.LoadScene(gameplaySceneName);

        private void ShowPhrase() => phrasePlayer.Play();

        private void QuitGame()
        {
#if UNITY_EDITOR
            // В редакторе Application.Quit ничего не делает,
            // поэтому кнопку иначе невозможно проверить до сборки.
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
