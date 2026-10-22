using System;
using System.Collections.Generic;
using Radio.World;
using UnityEngine;
using Yarn.Unity;

namespace Radio.Dialogue
{
    /// <summary>
    /// Показывает и прячет предметы в кадре по команде из диалога:
    /// газетная вырезка на столе, кассета в руке, человек в дверях.
    /// </summary>
    /// <remarks>
    /// Предметы перечисляются в инспекторе под собственными короткими именами, а не
    /// разыскиваются по сцене через Find: так переименование объекта ничего не ломает,
    /// а в диалоге остаются понятные слова вместо путей вида Apartment_Blockout/...
    /// </remarks>
    public sealed class StageDirector : MonoBehaviour
    {
        [Serializable]
        public struct StageObject
        {
            [Tooltip("Имя для диалога. Латиницей и без пробелов: show вырезка выглядит красиво, " +
                     "но Yarn разбирает аргументы по пробелам.")]
            public string id;

            public GameObject target;

            [Tooltip("Виден ли предмет в начале смены.")]
            public bool visibleAtStart;
        }

        [Header("Ссылки")]
        [SerializeField] private DialogueRunner runner;

        [Header("Предметы сцены")]
        [SerializeField] private StageObject[] objects = Array.Empty<StageObject>();

        private readonly Dictionary<string, GameObject> _byId =
            new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            if (runner == null)
            {
                runner = GetComponent<DialogueRunner>();
            }

            foreach (var entry in objects)
            {
                if (string.IsNullOrWhiteSpace(entry.id) || entry.target == null)
                {
                    continue;
                }

                if (_byId.ContainsKey(entry.id))
                {
                    Debug.LogError($"{nameof(StageDirector)}: имя {entry.id} занято дважды. " +
                                   "Взят первый предмет.", this);
                    continue;
                }

                _byId.Add(entry.id, entry.target);
                entry.target.SetActive(entry.visibleAtStart);
            }

            if (runner == null)
            {
                Debug.LogError($"{nameof(StageDirector)}: не задан Dialogue Runner. " +
                               "Команды показа предметов не будут работать.", this);
                enabled = false;
                return;
            }

            runner.AddCommandHandler("show", (Action<string>)(id => SetVisible(id, true)));
            runner.AddCommandHandler("hide", (Action<string>)(id => SetVisible(id, false)));
            runner.AddCommandHandler("toggle", (Action<string>)Toggle);
        }

        // Показанное и спрятанное диалогом помнит флаг OBJ_STAGE_<имя> (сохранение, GDD 02.12).
        private const string FlagPrefix = "OBJ_STAGE_";

        private void Start()
        {
            if (GameSession.Current != null)
            {
                GameSession.Current.Restored += OnRestored;
            }
        }

        private void OnDestroy()
        {
            if (GameSession.Current != null)
            {
                GameSession.Current.Restored -= OnRestored;
            }
        }

        private void OnRestored()
        {
            foreach (var pair in _byId)
            {
                if (GameSession.Current.World.TryGet<bool>(FlagPrefix + pair.Key.ToUpperInvariant(), out var visible))
                {
                    pair.Value.SetActive(visible);
                }
            }
        }

        private void SetVisible(string id, bool visible)
        {
            if (TryGet(id, out var target))
            {
                Apply(id, target, visible);
            }
        }

        private void Toggle(string id)
        {
            if (TryGet(id, out var target))
            {
                Apply(id, target, !target.activeSelf);
            }
        }

        private static void Apply(string id, GameObject target, bool visible)
        {
            target.SetActive(visible);

            if (GameSession.Current != null)
            {
                GameSession.Current.World.Set(FlagPrefix + id.ToUpperInvariant(), visible);
            }
        }

        private bool TryGet(string id, out GameObject target)
        {
            if (_byId.TryGetValue(id, out target))
            {
                return true;
            }

            // Имя из диалога проверить заранее невозможно: опечатка всплывает только здесь,
            // поэтому она обязана быть громкой.
            Debug.LogError($"{nameof(StageDirector)}: предмет {id} не перечислен в инспекторе.", this);
            return false;
        }
    }
}
