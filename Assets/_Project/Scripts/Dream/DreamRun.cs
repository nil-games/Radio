using System;
using System.Collections;
using UnityEngine;

namespace Radio.Dream
{
    /// <summary>
    /// Забег за девушкой по жёлтым точкам (Сон-Изнанка, GDD 02.21б): она ждёт на
    /// следующей точке, игрок добегает — она растворяется и появляется дальше. Упал в
    /// пустоту — возврат к последней пройденной точке, без наказания.
    /// </summary>
    /// <remarks>
    /// Точки задаются по порядку: точка — место, где игрок «засчитывает» её, и место,
    /// где стоит девушка, когда зовёт к ней. Последняя точка — финал сна.
    /// Итерация 1: геометрия и правила; вид (облака точек, шейдеры) — потом.
    /// </remarks>
    public sealed class DreamRun : MonoBehaviour
    {
        [Serializable]
        public struct Point
        {
            [Tooltip("Жёлтая точка цели: дошёл до неё — точка пройдена, здесь же возрождение.")]
            public Transform marker;

            [Tooltip("Где стоит девушка, пока зовёт к этой точке. Пусто — рядом с маркером.")]
            public Transform girlSpot;
        }

        [SerializeField] private DreamController player;
        [SerializeField] private Transform girl;
        [SerializeField] private Point[] points = Array.Empty<Point>();

        [Tooltip("Насколько близко подойти к точке, м (по горизонтали).")]
        [SerializeField] private float reachRadius = 1.2f;

        [Tooltip("Ниже этой высоты — упал в пустоту.")]
        [SerializeField] private float fallHeight = -15f;

        [Tooltip("За сколько секунд девушка растворяется и появляется.")]
        [SerializeField] private float girlFade = 0.35f;

        public event Action Finished;

        private int _next;
        private Vector3 _respawn;
        private Quaternion _respawnRotation;
        private Vector3 _girlScale;
        private bool _done;

        private void Start()
        {
            _respawn = player.transform.position;
            _respawnRotation = player.transform.rotation;
            _girlScale = girl != null ? girl.localScale : Vector3.one;

            for (var i = 0; i < points.Length; i++)
            {
                SetMarker(i, i == 0);
            }

            PlaceGirl(0);
        }

        private void Update()
        {
            if (_done || player == null)
            {
                return;
            }

            if (player.transform.position.y < fallHeight)
            {
                player.Teleport(_respawn, _respawnRotation);
                return;
            }

            if (_next >= points.Length)
            {
                return;
            }

            var marker = points[_next].marker;
            var flat = player.transform.position - marker.position;
            flat.y = 0f;

            if (flat.magnitude > reachRadius || Mathf.Abs(player.transform.position.y - marker.position.y) > 2.5f)
            {
                return;
            }

            Reach(_next);
        }

        private void Reach(int index)
        {
            var marker = points[index].marker;
            _respawn = marker.position + Vector3.up * 0.1f;
            _respawnRotation = player.transform.rotation;
            SetMarker(index, false);
            _next = index + 1;

            if (_next >= points.Length)
            {
                _done = true;
                Debug.Log($"{nameof(DreamRun)}: сон пройден.", this);
                Finished?.Invoke();
                return;
            }

            SetMarker(_next, true);
            StartCoroutine(MoveGirl(_next));
        }

        private void SetMarker(int index, bool visible)
        {
            if (points[index].marker != null)
            {
                points[index].marker.gameObject.SetActive(visible);
            }
        }

        private void PlaceGirl(int index)
        {
            if (girl == null || index >= points.Length)
            {
                return;
            }

            var spot = points[index].girlSpot != null ? points[index].girlSpot : points[index].marker;
            girl.position = spot.position;
        }

        private IEnumerator MoveGirl(int index)
        {
            if (girl == null)
            {
                yield break;
            }

            for (var t = 0f; t < girlFade; t += Time.deltaTime)
            {
                girl.localScale = _girlScale * (1f - t / girlFade);
                yield return null;
            }

            PlaceGirl(index);

            for (var t = 0f; t < girlFade; t += Time.deltaTime)
            {
                girl.localScale = _girlScale * (t / girlFade);
                yield return null;
            }

            girl.localScale = _girlScale;
        }
    }
}
