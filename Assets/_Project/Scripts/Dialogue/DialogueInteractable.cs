using Radio.Interaction;
using UnityEngine;

namespace Radio.Dialogue
{
    /// <summary>
    /// Предмет, взаимодействие с которым начинает разговор.
    /// Что именно будет сказано, решает сам узел Yarn: время, ночь и флаги мира
    /// доступны ему как обычные переменные.
    /// </summary>
    public class DialogueInteractable : Interactable
    {
        [Header("Диалог")]
        [Tooltip("Имя узла в файле .yarn, который запустится при взаимодействии.")]
        [SerializeField] private string yarnNode;

        [Tooltip("Необязательно. Имя вычисляемой переменной Yarn со знаком доллара: пока она " +
                 "ложна, предмет не ловит прицел и не обводится — как будто говорить не о чем. " +
                 "Правило пишется в том же файле .yarn, например:\n" +
                 "<<declare $chair_has_something = $night == 1 and not $STORY_FOUND_SERGEY_KEYS>>")]
        [SerializeField] private string availabilityVariable;

        [Header("Ссылки")]
        [Tooltip("Если пусто, ищется в сцене при первом обращении.")]
        [SerializeField] private DialogueController dialogue;

        [SerializeField] private RadioVariableStorage variables;

        public override bool CanInteract
        {
            get
            {
                if (!base.CanInteract)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(availabilityVariable))
                {
                    return true;
                }

                EnsureReferences();

                // Переменной может не быть в проекте Yarn: пока диалоги не написаны,
                // это норма, и предмет должен оставаться доступным, а не пропадать молча.
                return variables == null
                       || !variables.TryGetValue<bool>(availabilityVariable, out var available)
                       || available;
            }
        }

        public override void Interact(PlayerInteractor interactor)
        {
            if (string.IsNullOrWhiteSpace(yarnNode))
            {
                Debug.LogError($"{nameof(DialogueInteractable)}: не задан узел диалога.", this);
                return;
            }

            EnsureReferences();

            if (dialogue == null)
            {
                Debug.LogError($"{nameof(DialogueInteractable)}: в сцене нет {nameof(DialogueController)}.", this);
                return;
            }

            // Через PlayThen, а не напрямую: если у предмета есть анимация взаимодействия,
            // разговор начнётся после неё, а не поверх.
            PlayThen("Interact", () => dialogue.StartDialogue(yarnNode));
        }

        private void EnsureReferences()
        {
            if (dialogue == null)
            {
                dialogue = FindFirstObjectByType<DialogueController>();
            }

            if (variables == null)
            {
                variables = FindFirstObjectByType<RadioVariableStorage>();
            }
        }
    }
}
