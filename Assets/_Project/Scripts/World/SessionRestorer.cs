using System.Collections;
using Radio.Interaction;
using Radio.UI;
using UnityEngine;

namespace Radio.World
{
    /// <summary>
    /// Единственное место, где сцена восстанавливается из снимка (GDD 02.12):
    /// «Продолжить» из меню или возвращение из мини-локации. Без снимка — новая игра,
    /// и сразу автосохранение начала ночи.
    /// </summary>
    /// <remarks>
    /// Работает в Start с поздним порядком исполнения: к этому моменту все объекты
    /// сцены прошли свой Start (завели события по часам, подписались на
    /// <see cref="GameSession.Restored"/>), а первого Update ещё не было — события
    /// 00:00 первой ночи не успевают сработать.
    /// Порядок восстановления: часы → флаги → события по часам → задачи → игрок →
    /// <see cref="GameSession.Restored"/> для объектов.
    /// </remarks>
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(GameSession))]
    public sealed class SessionRestorer : MonoBehaviour
    {
        [Tooltip("Сколько проявляется картинка после загрузки сохранения, с.")]
        [SerializeField] private float fadeInSeconds = 1.5f;

        [Tooltip("Порядок холста затемнения: поверх всего интерфейса.")]
        [SerializeField] private int sortingOrder = 210;

        private void Start()
        {
            var session = GetComponent<GameSession>();

            if (MiniLocationTrip.TryTakeReturn(out var trip))
            {
                Apply(trip, session);
                Debug.Log($"{nameof(SessionRestorer)}: возврат из мини-локации, {session.Time.DebugClock}.", this);

                // Ночь доигрывает сон: конец сна, утро, пробуждение. Чёрный экран — его.
                var sleep = FindAnyObjectByType<SleepSequence>();

                if (sleep != null)
                {
                    sleep.ResumeAfterDream();
                }

                return;
            }

            var pending = SaveSystem.PendingLoad;
            SaveSystem.PendingLoad = null;

            if (pending != null)
            {
                var fader = ScreenFader.Create(transform, sortingOrder, 1f);
                Apply(pending, session);
                Debug.Log($"{nameof(SessionRestorer)}: загружено сохранение от {pending.savedAt}, {session.Time.DebugClock}.", this);
                StartCoroutine(FadeIn(fader));
                return;
            }

            // Новая игра: точка сохранения — начало первой ночи, до её первых событий.
            if (TryGetComponent<AutoSaver>(out var saver))
            {
                saver.SaveNow(true);
            }
        }

        private IEnumerator FadeIn(ScreenFader fader)
        {
            yield return fader.FadeTo(0f, fadeInSeconds);
            Destroy(fader.gameObject);
        }

        public static void Apply(SaveData data, GameSession session)
        {
            if (data == null || session == null)
            {
                return;
            }

            session.Time.Restore(data.night, data.minutes);

            var world = session.World;
            world.Clear();

            foreach (var flag in data.bools)
            {
                world.Set(flag.name, flag.value);
            }

            foreach (var flag in data.numbers)
            {
                world.Set(flag.name, flag.value);
            }

            foreach (var flag in data.texts)
            {
                world.Set(flag.name, flag.value);
            }

            TimedEvent.RestoreDone(data.firedEvents, data.skippedEvents);

            var taskLog = FindAnyObjectByType<TaskLog>();

            if (taskLog != null)
            {
                taskLog.Restore(data.tasks, world);
            }

            if (data.hasPlayer)
            {
                PlacePlayer(data.playerPosition, data.playerYaw);
            }

            session.NotifyRestored();
        }

        private static void PlacePlayer(Vector3 position, float yaw)
        {
            var player = FindAnyObjectByType<PlayerInteractor>();

            if (player == null)
            {
                return;
            }

            // CharacterController перетирает позицию, заданную в обход него.
            var controller = player.GetComponent<CharacterController>();
            var wasEnabled = controller != null && controller.enabled;

            if (controller != null)
            {
                controller.enabled = false;
            }

            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            if (controller != null)
            {
                controller.enabled = wasEnabled;
            }
        }
    }
}
