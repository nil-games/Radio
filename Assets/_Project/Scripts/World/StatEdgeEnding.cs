using System;
using Radio.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Radio.World
{
    /// <summary>
    /// Концовка «перебрал»: как только параметр сюжета (Crime, Cult, GirlFriend) доходит
    /// до края шкалы, игра сразу заканчивается своей концовкой.
    /// </summary>
    /// <remarks>
    /// Пока концовок нет, на чёрном экране стоит заглушка с названием из списка ниже.
    /// Чтобы заменить: поменяй текст в списке «Концовки» в инспекторе, а когда появятся
    /// сцены концовок — запускай их из <see cref="ShowEnding"/> вместо надписи.
    /// </remarks>
    public sealed class StatEdgeEnding : MonoBehaviour
    {
        [Serializable]
        public struct Ending
        {
            [Tooltip("Параметр: Crime, Cult или GirlFriend.")]
            public string stat;

            [Tooltip("Край шкалы: true — максимум (+5), false — минимум (−5).")]
            public bool atMax;

            [TextArea(2, 6)]
            public string text;
        }

        [Tooltip("Концовки по краям шкал. Параметр без строки здесь концовку не вызывает.")]
        [SerializeField]
        private Ending[] endings =
        {
            new Ending { stat = "Crime", atMax = true, text = "КОНЦОВКА «Соучастник»\nКриминал на максимуме\n(заглушка концовки)" },
            new Ending { stat = "Crime", atMax = false, text = "КОНЦОВКА «Бабка Зина»\nПолиция на максимуме\n(заглушка концовки)" },
            new Ending { stat = "Cult", atMax = true, text = "КОНЦОВКА «Искажение»\nКульт на максимуме\n(заглушка концовки)" },
            new Ending { stat = "Cult", atMax = false, text = "КОНЦОВКА «Ритуал»\nКульт на минимуме\n(заглушка концовки)" },
            new Ending { stat = "GirlFriend", atMax = true, text = "КОНЦОВКА «Алиса»\nАлиса на максимуме\n(заглушка концовки)" },
            new Ending { stat = "GirlFriend", atMax = false, text = "КОНЦОВКА «Серёга»\nСерёга на максимуме\n(заглушка концовки)" },
        };

        [Tooltip("Шрифт надписи. Обязан содержать кириллицу.")]
        [SerializeField] private TMP_FontAsset font;

        [Tooltip("Порядок холста: поверх всего.")]
        [SerializeField] private int sortingOrder = 300;

        private WorldState _world;
        private GameObject _screen;
        private TextMeshProUGUI _label;
        private bool _ended;

        // Start, а не Awake: сессия заводится в своём Awake.
        private void Start()
        {
            _world = GameSession.Current != null ? GameSession.Current.World : null;

            if (_world == null)
            {
                Debug.LogError($"{nameof(StatEdgeEnding)}: нет состояния мира.", this);
                enabled = false;
                return;
            }

            Build();
            _world.FlagChanged += OnFlagChanged;
        }

        private void OnDestroy()
        {
            if (_world != null)
            {
                _world.FlagChanged -= OnFlagChanged;
            }
        }

        private void OnFlagChanged(string name)
        {
            if (_ended)
            {
                return;
            }

            foreach (var stat in StoryStats.All)
            {
                if (stat.Name != name || !_world.TryGet<float>(name, out var value))
                {
                    continue;
                }

                var atMax = value >= stat.Max;
                var atMin = value <= stat.Min;

                if (!atMax && !atMin)
                {
                    return;
                }

                foreach (var ending in endings)
                {
                    if (ending.stat == name && ending.atMax == atMax)
                    {
                        ShowEnding(ending);
                        return;
                    }
                }
            }
        }

        private void ShowEnding(Ending ending)
        {
            _ended = true;
            Debug.Log($"{nameof(StatEdgeEnding)}: {ending.stat} на {(ending.atMax ? "максимуме" : "минимуме")} — концовка.", this);

            _label.text = ending.text;
            _screen.SetActive(true);

            // Игра закончилась: управление игроку больше не возвращается.
            var interactor = FindAnyObjectByType<PlayerInteractor>();

            if (interactor != null)
            {
                interactor.AddInputBlock(this);

                if (interactor.Movement != null)
                {
                    interactor.Movement.AddSuspendRequest(this);
                }
            }
        }

        private void Build()
        {
            // Строится кодом, как и остальной интерфейс проекта.
            _screen = new GameObject("EndingScreen", typeof(RectTransform));
            _screen.transform.SetParent(transform, false);

            var canvas = _screen.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = _screen.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var black = new GameObject("Black", typeof(RectTransform));
            var rect = (RectTransform)black.transform;
            rect.SetParent(_screen.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            black.AddComponent<Image>().color = Color.black;

            var label = new GameObject("Text", typeof(RectTransform));
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(_screen.transform, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

            _label = label.AddComponent<TextMeshProUGUI>();
            _label.font = font;
            _label.fontSize = 48f;
            _label.color = Color.white;
            _label.alignment = TextAlignmentOptions.Center;
            _label.raycastTarget = false;

            _screen.SetActive(false);
        }
    }
}
