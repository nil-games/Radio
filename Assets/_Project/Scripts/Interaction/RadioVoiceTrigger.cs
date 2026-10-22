using Radio.Dialogue;
using Radio.World;
using UnityEngine;
using Yarn.Unity;

namespace Radio.Interaction
{
    /// <summary>
    /// Голос на частоте: пока висит задача, радио включено и игрок поймал нужную
    /// частоту, начинается разговор. Так Алиса говорит из радио в ночь 2 (04:00:02).
    /// </summary>
    /// <remarks>
    /// Срабатывает один раз. Команда диалога <c>&lt;&lt;radio_off&gt;&gt;</c> выключает радио —
    /// для ветки «выключить радио».
    /// Когда задача появляется, радио само включается и шипит на той частоте, где стоит:
    /// «Шум радио» в сценарии — это и есть приглашение подойти и поискать голос.
    /// </remarks>
    public sealed class RadioVoiceTrigger : MonoBehaviour
    {
        [SerializeField] private RadioTuner tuner;
        [SerializeField] private RadioReceiver receiver;

        [Tooltip("На какой частоте голос, МГц.")]
        [SerializeField] private float frequency = 96.6f;

        [Tooltip("Насколько можно промахнуться мимо частоты, МГц.")]
        [SerializeField] private float tolerance = 0.3f;

        [Tooltip("Разговор начинается, только пока висит эта задача.")]
        [SerializeField] private string requiredTask = "n2_radio";

        [Tooltip("Узел разговора.")]
        [SerializeField] private string node = "Night2_AlisaRadio";

        [SerializeField] private DialogueController dialogue;

        [Tooltip("Включить радио само, как только появилась задача.")]
        [SerializeField] private bool powerOnWithTask = true;

        private TaskLog _tasks;
        private bool _done;
        private bool _poweredOn;

        private void Awake()
        {
            if (tuner == null)
            {
                tuner = GetComponent<RadioTuner>();
            }

            if (receiver == null)
            {
                receiver = GetComponent<RadioReceiver>();
            }

            var runner = FindFirstObjectByType<DialogueRunner>();

            if (runner != null && receiver != null)
            {
                runner.AddCommandHandler("radio_off", (System.Action)(() => receiver.SetPower(false)));
            }
        }

        private void Update()
        {
            if (_done || tuner == null || receiver == null)
            {
                return;
            }

            if (_tasks == null)
            {
                _tasks = FindAnyObjectByType<TaskLog>();
            }

            if (powerOnWithTask && !_poweredOn && _tasks != null && _tasks.IsActive(requiredTask))
            {
                _poweredOn = true;
                receiver.SetPower(true);
            }

            if (!receiver.IsOn)
            {
                return;
            }

            if (Mathf.Abs(tuner.Frequency - frequency) > tolerance)
            {
                return;
            }

            if (_tasks == null)
            {
                _tasks = FindAnyObjectByType<TaskLog>();
            }

            if (_tasks == null || !_tasks.IsActive(requiredTask))
            {
                return;
            }

            if (dialogue == null)
            {
                dialogue = FindAnyObjectByType<DialogueController>();
            }

            if (dialogue == null || dialogue.IsRunning)
            {
                return;
            }

            _done = true;
            dialogue.StartDialogue(node);
        }
    }
}
