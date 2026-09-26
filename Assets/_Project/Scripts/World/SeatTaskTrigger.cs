using Radio.Interaction;
using UnityEngine;

namespace Radio.World
{
    /// <summary>
    /// Событие «игрок вернулся за стол»: если висит нужная задача, при посадке звучит
    /// сигнал, задача сменяется следующей и ставится флаг мира.
    /// </summary>
    /// <remarks>
    /// Срабатывает по задаче, а не по времени: игрок может вернуться за стол когда угодно,
    /// и событие должно дождаться его, а не пройти мимо, пока он осматривает квартиру.
    /// Если задача появилась, когда игрок уже сидит, событие срабатывает сразу: звать
    /// «вернуться за стол» того, кто за ним сидит, бессмысленно.
    /// Задача снимается при первом же срабатывании, поэтому событие одноразовое само по себе.
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    public sealed class SeatTaskTrigger : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private SitPoint seat;

        [Tooltip("Если пусто, ищется в сцене.")]
        [SerializeField] private TaskLog tasks;

        [Header("Условие")]
        [Tooltip("Событие срабатывает, только пока эта задача в списке.")]
        [SerializeField] private string requiredTask = "return_to_desk";

        [Header("Итог")]
        [Tooltip("Звук при посадке. Пусто — без звука.")]
        [SerializeField] private AudioClip sound;

        [Tooltip("Задача, которая появляется вместо выполненной. Пусто — никакой.")]
        [SerializeField] private string nextTask = "check_computer";

        [Tooltip("Флаг мира, который ставится при срабатывании. Пусто — никакого.")]
        [SerializeField] private string setFlag = "STORY_POLICE_MAIL_ARRIVED";

        private AudioSource _source;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;

            // Сигнал из компьютера слышен как интерфейсный звук, а не из точки в комнате.
            _source.spatialBlend = 0f;

            if (seat == null)
            {
                Debug.LogError($"{nameof(SeatTaskTrigger)}: не задано кресло.", this);
                enabled = false;
                return;
            }

            seat.SatDown += OnSatDown;
        }

        // Журнал задач подписываем в Start: он мог проснуться позже нас.
        private void Start()
        {
            if (tasks == null)
            {
                tasks = FindAnyObjectByType<TaskLog>();
            }

            if (tasks != null)
            {
                tasks.Changed += OnTasksChanged;
            }
        }

        private void OnDestroy()
        {
            if (seat != null)
            {
                seat.SatDown -= OnSatDown;
            }

            if (tasks != null)
            {
                tasks.Changed -= OnTasksChanged;
            }
        }

        private void OnTasksChanged()
        {
            if (seat != null && seat.IsSeated)
            {
                OnSatDown();
            }
        }

        private void OnSatDown()
        {
            if (!enabled)
            {
                return;
            }

            if (tasks == null)
            {
                tasks = FindAnyObjectByType<TaskLog>();
            }

            if (tasks == null || !tasks.IsActive(requiredTask))
            {
                return;
            }

            tasks.Complete(requiredTask);

            if (!string.IsNullOrWhiteSpace(nextTask))
            {
                tasks.Add(nextTask);
            }

            if (!string.IsNullOrWhiteSpace(setFlag) && GameSession.Current != null)
            {
                GameSession.Current.World.Set(setFlag, true);
            }

            if (sound != null)
            {
                _source.PlayOneShot(sound);
            }
        }
    }
}
