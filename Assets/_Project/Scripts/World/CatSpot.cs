using UnityEngine;

namespace Radio.World
{
    /// <summary>
    /// Место в квартире, куда может прийти кошка: у миски, у окна, у дивана.
    /// Кошка встаёт в точку и поворачивается туда, куда смотрит синяя стрелка объекта.
    /// </summary>
    /// <remarks>
    /// Точки расставляются в редакторе руками и ставятся на пол: кошка ходит по NavMesh,
    /// а он запечён только по полу. Точку на мебели кошка не достанет.
    /// </remarks>
    public sealed class CatSpot : MonoBehaviour
    {
        public enum Pose
        {
            /// <summary>Сесть.</summary>
            Sit,

            /// <summary>Лечь. Пока своей анимации нет, аниматор играет «сесть».</summary>
            Lie,
        }

        [Tooltip("Что кошка делает, когда пришла.")]
        [SerializeField] private Pose pose = Pose.Sit;

        [Tooltip("Как часто кошка выбирает эту точку относительно других. 0 — никогда.")]
        [Min(0f)]
        [SerializeField] private float weight = 1f;

        public Pose SpotPose => pose;

        public float Weight => weight;

        private void OnDrawGizmos()
        {
            Gizmos.color = pose == Pose.Sit ? new Color(1f, 0.6f, 0.2f) : new Color(0.4f, 0.7f, 1f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.12f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.1f,
                            transform.position + Vector3.up * 0.1f + transform.forward * 0.3f);
        }
    }
}
