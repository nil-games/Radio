using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Radio.World
{
    /// <summary>
    /// Кошка в квартире: сидит на месте случайное время, потом выбирает случайную точку
    /// <see cref="CatSpot"/>, идёт туда по NavMesh и снова садится или ложится.
    /// </summary>
    /// <remarks>
    /// Ходит только по полу: NavMesh запечён по полу квартиры (объект CatNavMesh, агент «Cat»),
    /// прыжков на мебель пока нет. Анимации переключаются плавным переходом без переходов
    /// в аниматоре: состояния Walk, Seat и Lie лежат в Cat.controller отдельно, и кто
    /// из них играет, решает этот компонент. Скорость шага подстраивается под скорость
    /// агента, чтобы лапы не скользили по полу.
    /// </remarks>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CatWander : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private Animator animator;

        [Tooltip("Куда кошка может ходить. Пусто — все CatSpot в сцене.")]
        [SerializeField] private CatSpot[] spots = System.Array.Empty<CatSpot>();

        [Tooltip("Куда пойти первым делом, как только кошку впустили. Пусто — случайная точка.")]
        [SerializeField] private CatSpot firstSpot;

        [Header("Отдых")]
        [Tooltip("Сколько кошка сидит или лежит на месте, с: случайно между двумя числами.")]
        [SerializeField] private Vector2 restSeconds = new Vector2(10f, 40f);

        [Header("Анимация")]
        [Tooltip("С какой скоростью, м/с, анимация ходьбы идёт в обычном темпе. " +
                 "Подбирается так, чтобы лапы не скользили.")]
        [SerializeField] private float walkAnimationSpeed = 0.35f;

        [SerializeField] private float crossFade = 0.25f;

        [SerializeField] private string walkState = "Walk";
        [SerializeField] private string sitState = "Seat";
        [SerializeField] private string lieState = "Lie";

        [Tooltip("Как быстро кошка разворачивается к нужной стороне, придя на место, град/с.")]
        [SerializeField] private float faceTurnSpeed = 240f;

        private NavMeshAgent _agent;
        private CatSpot _target;
        private CatSpot _current;
        private float _restUntil;
        private bool _walking;
        private bool _facing;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();

            if (spots.Length == 0)
            {
                spots = FindObjectsByType<CatSpot>(FindObjectsSortMode.None);
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        // OnEnable, а не Start: кошку включают в момент, когда её впустили, и выбор
        // первой точки должен случиться именно тогда.
        private void OnEnable()
        {
            if (_agent == null || !_agent.isOnNavMesh)
            {
                return;
            }

            GoTo(firstSpot != null ? firstSpot : PickSpot());
        }

        private void Update()
        {
            if (!_agent.isOnNavMesh)
            {
                return;
            }

            if (_walking)
            {
                UpdateWalk();
                return;
            }

            if (_facing)
            {
                FaceSpot();
            }

            if (Time.time >= _restUntil)
            {
                GoTo(PickSpot());
            }
        }

        private void UpdateWalk()
        {
            if (animator != null)
            {
                animator.speed = Mathf.Clamp(_agent.velocity.magnitude / Mathf.Max(0.01f, walkAnimationSpeed), 0.3f, 2f);
            }

            if (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance)
            {
                return;
            }

            Arrive();
        }

        private void GoTo(CatSpot spot)
        {
            if (spot == null)
            {
                Rest();
                return;
            }

            _target = spot;
            _walking = true;
            _facing = false;
            _agent.isStopped = false;
            _agent.SetDestination(spot.transform.position);
            Play(walkState);
        }

        private void Arrive()
        {
            _walking = false;
            _current = _target;
            _agent.isStopped = true;
            _facing = true;

            if (animator != null)
            {
                animator.speed = 1f;
            }

            // Садится с начала клипа: «сесть» начинается со стойки.
            Play(_current.SpotPose == CatSpot.Pose.Lie ? lieState : sitState, fromStart: true);
            Rest();
        }

        private void Rest()
        {
            _restUntil = Time.time + Random.Range(restSeconds.x, restSeconds.y);
        }

        private void FaceSpot()
        {
            if (_current == null)
            {
                _facing = false;
                return;
            }

            var forward = _current.transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.001f)
            {
                _facing = false;
                return;
            }

            var goal = Quaternion.LookRotation(forward);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, faceTurnSpeed * Time.deltaTime);
            _facing = Quaternion.Angle(transform.rotation, goal) > 0.5f;
        }

        /// <summary>
        /// Случайная точка с учётом веса. Та, где кошка сидит сейчас, не выбирается:
        /// иначе она встала бы и тут же села на то же место.
        /// </summary>
        private CatSpot PickSpot()
        {
            var candidates = new List<CatSpot>(spots.Length);
            var total = 0f;

            foreach (var spot in spots)
            {
                if (spot == null || spot == _current || spot.Weight <= 0f || !spot.isActiveAndEnabled)
                {
                    continue;
                }

                candidates.Add(spot);
                total += spot.Weight;
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            var roll = Random.value * total;

            foreach (var spot in candidates)
            {
                roll -= spot.Weight;

                if (roll <= 0f)
                {
                    return spot;
                }
            }

            return candidates[candidates.Count - 1];
        }

        private void Play(string state, bool fromStart = false)
        {
            if (animator == null || string.IsNullOrEmpty(state))
            {
                return;
            }

            if (fromStart)
            {
                animator.CrossFadeInFixedTime(state, crossFade, 0, 0f);
            }
            else
            {
                animator.CrossFadeInFixedTime(state, crossFade);
            }
        }
    }
}
