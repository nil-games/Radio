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

        [Tooltip("ПРОДОЛЖИТЬ — сохранений пока нет, показывает фразу-заглушку.")]
        [SerializeField] private Button continueButton;

        [Tooltip("НАСТРОЙКИ — настроек пока нет, показывают фразу-заглушку.")]
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

        [Tooltip("Вступительный ролик перед игрой. Пусто — сразу в игровую сцену.")]
        [SerializeField] private IntroVideo intro;

        [Header("Сохранение")]
        [Tooltip("Фраза на ПРОДОЛЖИТЬ, когда сохранения нет.")]
        [SerializeField] private string noSaveText = "Сохранений нет";

        [Tooltip("Фраза на ИГРАТЬ, когда сохранение есть: второе нажатие начинает заново.")]
        [SerializeField] private string overwriteText = "Сохранение будет перезаписано. Нажми ещё раз";

        [Header("Заглушки")]

        [Tooltip("Фраза на НАСТРОЙКИ, пока их нет.")]
        [SerializeField] private string settingsStubText = "Их нет";

        private bool _initialized;
        private bool _confirmNewGame;

        private void Awake() => EnsureInitialized();

        private void OnEnable() => EnsureInitialized();

        private void OnDestroy()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(NewGame);
            }

            if (doNotPressButton != null)
            {
                doNotPressButton.onClick.RemoveListener(ShowPhrase);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(QuitGame);
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(ContinueGame);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(ShowSettingsStub);
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

            playButton.onClick.AddListener(NewGame);
            doNotPressButton.onClick.AddListener(ShowPhrase);
            quitButton.onClick.AddListener(QuitGame);

            // ПРОДОЛЖИТЬ без сохранения и НАСТРОЙКИ намеренно остаются включёнными и отвечают фразой:
            // штатная подсветка выключенной кнопки роняет плашку до половинной
            // прозрачности и обесцвечивает светящийся текст — на тёмном фоне это
            // читается как «картинка не загрузилась», а не «пока недоступно».
            if (continueButton != null)
            {
                continueButton.onClick.AddListener(ContinueGame);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(ShowSettingsStub);
            }
        }

        /// <summary>
        /// Новая игра. Если сохранение есть, первое нажатие только предупреждает:
        /// слот один, и новая игра перезапишет его первым же автосохранением.
        /// </summary>
        private void NewGame()
        {
            if (Radio.World.SaveSystem.HasSave && !_confirmNewGame)
            {
                _confirmNewGame = true;
                phrasePlayer.AlignTo((RectTransform)playButton.transform);
                phrasePlayer.Play(overwriteText);
                return;
            }

            StartGame();
        }

        /// <summary>«Продолжить»: загрузить сохранение без вступительного ролика.</summary>
        private void ContinueGame()
        {
            var data = Radio.World.SaveSystem.Load();

            if (data == null)
            {
                phrasePlayer.AlignTo((RectTransform)continueButton.transform);
                phrasePlayer.Play(noSaveText);
                return;
            }

            Radio.World.SaveSystem.PendingLoad = data;
            SceneManager.LoadScene(data.scene);
        }

        private void StartGame()
        {
            if (intro != null)
            {
                intro.Play(gameplaySceneName);
                return;
            }

            SceneManager.LoadScene(gameplaySceneName);
        }

        private void ShowPhrase()
        {
            phrasePlayer.AlignTo((RectTransform)doNotPressButton.transform);
            phrasePlayer.Play();
        }

        private void ShowSettingsStub()
        {
            phrasePlayer.AlignTo((RectTransform)settingsButton.transform);
            phrasePlayer.Play(settingsStubText);
        }

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
