using System.Collections;
using Radio.Dialogue;
using Radio.Interaction;
using UnityEngine;

namespace Radio.World
{
    /// <summary>
    /// Глюк в коридоре (ночь 1, N1-04): свет тускнеет, в конце коридора стоит силуэт;
    /// подошёл ближе или постоял — свет мигает, и всё снова как было.
    /// </summary>
    /// <remarks>
    /// Не скример: ни резкого звука, ни прыжка в камеру — должно оставаться место списать
    /// на усталость. Срабатывает один раз, когда часы дошли до отметки и игрок вошёл в зону.
    /// Зона — коллайдер на этом же объекте (размер и место правятся мышью в сцене); игрок
    /// ищется по положению, а не по физическим событиям, чтобы не зависеть от слоёв.
    /// <para/>
    /// Заглушка: коридор пока не «вытягивается» — только свет и силуэт.
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    public sealed class CorridorGlitch : MonoBehaviour, ISaveBlocker
    {
        [Header("Когда")]
        [Tooltip("С какой отметки на часах глюк может случиться.")]
        [SerializeField] private GameTimeMark activeFrom = new GameTimeMark("01:30");

        [Tooltip("Флаг мира: глюк уже был. Ставится в конце; при отладочном переводе часов " +
                 "его ставит StoryBeats.")]
        [SerializeField] private string doneFlag = "STORY_N1_CORRIDOR_SEEN";

        [Header("Что")]
        [Tooltip("Лампы коридора, которые тускнеют и мигают.")]
        [SerializeField] private Light[] lights = System.Array.Empty<Light>();

        [Tooltip("Во сколько раз гаснет свет (доля от исходной яркости).")]
        [Range(0f, 1f)]
        [SerializeField] private float dimTo = 0.2f;

        [Tooltip("За сколько секунд свет тускнеет.")]
        [SerializeField] private float dimSeconds = 2f;

        [Tooltip("Силуэт в конце коридора. В сцене выключен, включается на время глюка.")]
        [SerializeField] private GameObject silhouette;

        [Tooltip("Где может стоять силуэт: концы коридора. Берётся самый дальний от игрока — " +
                 "силуэт всегда «в конце», с какой стороны ни зайди. Пусто — где стоит в сцене.")]
        [SerializeField] private Transform[] silhouetteSpots = System.Array.Empty<Transform>();

        [Tooltip("С какого расстояния до силуэта, м, всё развеивается.")]
        [SerializeField] private float vanishDistance = 2f;

        [Tooltip("Если игрок не подходит — через сколько секунд развеивается само.")]
        [SerializeField] private float vanishAfter = 6f;

        [Header("После")]
        [Tooltip("Мысль героя после глюка; в узле — флаги и перевод часов.")]
        [SerializeField] private string thoughtNode = "Night1_Corridor";

        private Collider _zone;
        private TimedEvent _from;
        private PlayerInteractor _player;
        private float[] _intensity;
        private bool _running;

        /// <summary>Глюк идёт — сохраниться посреди нельзя.</summary>
        public bool BlocksSave => _running;

        private void Start()
        {
            _zone = GetComponent<Collider>();
            _zone.isTrigger = true;
            _from = new TimedEvent(activeFrom, TimedEvent.KeyFor(this));

            if (silhouette != null)
            {
                silhouette.SetActive(false);
            }
        }

        private void Update()
        {
            var session = GameSession.Current;

            if (_running || session == null)
            {
                return;
            }

            _from.Poll(session.Time);

            if (!_from.Fired || (session.World.TryGet<bool>(doneFlag, out var done) && done))
            {
                return;
            }

            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerInteractor>();
            }

            // Только когда игрок идёт сам: сидя за столом или в разговоре по коридору не ходят.
            if (_player == null || (_player.Movement != null && _player.Movement.IsSuspended)
                || !_zone.bounds.Contains(_player.transform.position))
            {
                return;
            }

            StartCoroutine(Run(session));
        }

        private IEnumerator Run(GameSession session)
        {
            _running = true;
            _intensity = new float[lights.Length];

            for (var i = 0; i < lights.Length; i++)
            {
                _intensity[i] = lights[i] != null ? lights[i].intensity : 0f;
            }

            if (silhouette != null)
            {
                PlaceSilhouette();
                silhouette.SetActive(true);
            }

            for (var t = 0f; t < dimSeconds; t += Time.deltaTime)
            {
                SetLights(Mathf.Lerp(1f, dimTo, t / Mathf.Max(0.01f, dimSeconds)));
                yield return null;
            }

            SetLights(dimTo);

            // Ждём, пока игрок подойдёт, — или пока ему не надоест стоять.
            for (var waited = 0f; waited < vanishAfter; waited += Time.deltaTime)
            {
                if (silhouette != null && _player != null
                    && Vector3.Distance(Flat(_player.transform.position), Flat(silhouette.transform.position)) <= vanishDistance)
                {
                    break;
                }

                yield return null;
            }

            // Мигание: пара коротких вспышек темноты — и всё как было.
            for (var i = 0; i < 3; i++)
            {
                SetLights(0f);
                yield return new WaitForSeconds(0.07f);
                SetLights(i == 2 ? 1f : dimTo);
                yield return new WaitForSeconds(0.1f);

                if (i == 1 && silhouette != null)
                {
                    silhouette.SetActive(false);
                }
            }

            SetLights(1f);
            session.World.Set(doneFlag, true);

            var dialogue = FindAnyObjectByType<DialogueController>();

            if (dialogue != null && !string.IsNullOrWhiteSpace(thoughtNode))
            {
                dialogue.StartDialogue(thoughtNode);
            }

            _running = false;
        }

        private void PlaceSilhouette()
        {
            Transform far = null;
            var best = -1f;

            foreach (var spot in silhouetteSpots)
            {
                if (spot == null)
                {
                    continue;
                }

                var distance = Vector3.Distance(Flat(spot.position), Flat(_player.transform.position));

                if (distance > best)
                {
                    best = distance;
                    far = spot;
                }
            }

            if (far != null)
            {
                silhouette.transform.SetPositionAndRotation(far.position, far.rotation);
            }
        }

        private void SetLights(float factor)
        {
            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                {
                    lights[i].intensity = _intensity[i] * factor;
                }
            }
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
