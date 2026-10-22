using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Radio.Dream
{
    /// <summary>
    /// Превращает обычные модели в облако светящихся точек (Сон-Изнанка, GDD 02.21б).
    /// Берёт все MeshFilter под собой, раскидывает точки по их поверхности (по площади),
    /// гуще — по острым рёбрам, а часть точек делает «нитями», стекающими вниз. Сами
    /// модели прячет: видно только облако. Текстуры и UV моделям не нужны — важна форма.
    /// </summary>
    /// <remarks>
    /// Точка — четыре вершины с одним центром: шейдер Radio/Dream/PointCloud
    /// разворачивает их к камере в кружок. Так у точек есть размер и свечение, а не
    /// пиксель на экране, как у MeshTopology.Points.
    /// Коллайдеры исходных моделей остаются: по облаку можно ходить.
    /// </remarks>
    public sealed class PointCloudSurface : MonoBehaviour
    {
        [Tooltip("Материал с шейдером Radio/Dream/PointCloud. Цвет облака — в материале.")]
        [SerializeField] private Material material;

        [Header("Плотность")]
        [Tooltip("Точек на квадратный метр поверхности.")]
        [SerializeField] private float pointsPerSquareMeter = 90f;

        [Tooltip("Точек на метр острого ребра: по рёбрам читается форма.")]
        [SerializeField] private float pointsPerEdgeMeter = 45f;

        [Tooltip("С какого угла между гранями ребро считается острым, градусы.")]
        [SerializeField] private float edgeAngle = 35f;

        [Tooltip("Яркость точек ребра относительно поверхности.")]
        [SerializeField] private float edgeBrightness = 2.2f;

        [Tooltip("Доля точек, которые стекают вниз светящимися нитями.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float dripShare = 0.02f;

        [Tooltip("Верхний предел точек на объект — защита от огромных моделей.")]
        [SerializeField] private int maxPoints = 400000;

        [SerializeField] private int seed = 1;

        private void Start() => Build();

        private void Build()
        {
            if (material == null)
            {
                Debug.LogError($"{nameof(PointCloudSurface)}: не задан материал облака.", this);
                return;
            }

            var random = new System.Random(seed);
            var centers = new List<Vector3>();
            var data = new List<Vector4>(); // x — яркость, y — нить (0/1), z — фаза, w — случайное
            var cosEdge = Mathf.Cos(edgeAngle * Mathf.Deg2Rad);

            foreach (var filter in GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;

                if (mesh == null || filter.GetComponent<PointCloudSkip>() != null)
                {
                    continue;
                }

                var toRoot = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Sample(mesh, toRoot, random, cosEdge, centers, data);

                if (filter.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    renderer.enabled = false;
                }

                if (centers.Count >= maxPoints)
                {
                    break;
                }
            }

            BuildMesh(centers, data);
        }

        private void Sample(Mesh mesh, Matrix4x4 toRoot, System.Random random, float cosEdge,
            List<Vector3> centers, List<Vector4> data)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var world = new Vector3[vertices.Length];

            for (var i = 0; i < vertices.Length; i++)
            {
                world[i] = toRoot.MultiplyPoint3x4(vertices[i]);
            }

            // Рёбра: ключ — пара сваренных вершин, значение — нормали прилежащих граней.
            var edges = new Dictionary<(Vector3Int, Vector3Int), (Vector3 a, Vector3 b, Vector3 n1, Vector3 n2, int count)>();

            for (var t = 0; t < triangles.Length; t += 3)
            {
                var a = world[triangles[t]];
                var b = world[triangles[t + 1]];
                var c = world[triangles[t + 2]];
                var cross = Vector3.Cross(b - a, c - a);
                var area = cross.magnitude * 0.5f;

                if (area <= 1e-8f)
                {
                    continue;
                }

                var normal = cross.normalized;

                // Точки по площади: целая часть + вероятность для дробной.
                var expected = area * pointsPerSquareMeter;
                var count = (int)expected + (random.NextDouble() < expected - (int)expected ? 1 : 0);

                for (var k = 0; k < count && centers.Count < maxPoints; k++)
                {
                    var u = (float)random.NextDouble();
                    var v = (float)random.NextDouble();

                    if (u + v > 1f)
                    {
                        u = 1f - u;
                        v = 1f - v;
                    }

                    AddPoint(a + (b - a) * u + (c - a) * v, 1f, random, centers, data);
                }

                AddEdge(edges, a, b, normal);
                AddEdge(edges, b, c, normal);
                AddEdge(edges, c, a, normal);
            }

            // Острые рёбра и края без соседей — гуще и ярче.
            foreach (var edge in edges.Values)
            {
                var sharp = edge.count < 2 || Vector3.Dot(edge.n1, edge.n2) < cosEdge;

                if (!sharp)
                {
                    continue;
                }

                var length = Vector3.Distance(edge.a, edge.b);
                var count = Mathf.Max(1, Mathf.RoundToInt(length * pointsPerEdgeMeter));

                for (var k = 0; k < count && centers.Count < maxPoints; k++)
                {
                    var t = (k + (float)random.NextDouble()) / count;
                    AddPoint(Vector3.Lerp(edge.a, edge.b, t), edgeBrightness, random, centers, data);
                }
            }
        }

        private static void AddEdge(Dictionary<(Vector3Int, Vector3Int), (Vector3, Vector3, Vector3, Vector3, int)> edges,
            Vector3 a, Vector3 b, Vector3 normal)
        {
            var ka = Quantize(a);
            var kb = Quantize(b);
            var key = Compare(ka, kb) < 0 ? (ka, kb) : (kb, ka);

            if (edges.TryGetValue(key, out var existing))
            {
                edges[key] = (existing.Item1, existing.Item2, existing.Item3, normal, existing.Item5 + 1);
            }
            else
            {
                edges[key] = (a, b, normal, normal, 1);
            }
        }

        private static Vector3Int Quantize(Vector3 p) =>
            new Vector3Int(Mathf.RoundToInt(p.x * 1000f), Mathf.RoundToInt(p.y * 1000f), Mathf.RoundToInt(p.z * 1000f));

        private static int Compare(Vector3Int a, Vector3Int b) =>
            a.x != b.x ? a.x.CompareTo(b.x) : a.y != b.y ? a.y.CompareTo(b.y) : a.z.CompareTo(b.z);

        private void AddPoint(Vector3 position, float brightness, System.Random random,
            List<Vector3> centers, List<Vector4> data)
        {
            var drip = random.NextDouble() < dripShare ? 1f : 0f;
            centers.Add(position);
            data.Add(new Vector4(brightness * (0.6f + 0.4f * (float)random.NextDouble()), drip,
                (float)random.NextDouble(), (float)random.NextDouble()));
        }

        private void BuildMesh(List<Vector3> centers, List<Vector4> data)
        {
            var count = centers.Count;
            var vertices = new Vector3[count * 4];
            var corners = new Vector2[count * 4];
            var extra = new Vector4[count * 4];
            var indices = new int[count * 6];
            var cornerUv = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };

            for (var i = 0; i < count; i++)
            {
                for (var k = 0; k < 4; k++)
                {
                    vertices[i * 4 + k] = centers[i];
                    corners[i * 4 + k] = cornerUv[k];
                    extra[i * 4 + k] = data[i];
                }

                indices[i * 6] = i * 4;
                indices[i * 6 + 1] = i * 4 + 1;
                indices[i * 6 + 2] = i * 4 + 2;
                indices[i * 6 + 3] = i * 4;
                indices[i * 6 + 4] = i * 4 + 2;
                indices[i * 6 + 5] = i * 4 + 3;
            }

            var mesh = new Mesh { name = name + "_PointCloud", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = corners;
            mesh.SetUVs(1, extra);
            mesh.triangles = indices;

            // Границы с запасом: точки разворачиваются в шейдере, нити стекают вниз.
            mesh.RecalculateBounds();
            var bounds = mesh.bounds;
            bounds.Expand(new Vector3(1f, 6f, 1f));
            mesh.bounds = bounds;

            var holder = new GameObject("PointCloud");
            holder.transform.SetParent(transform, false);
            holder.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = holder.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Своё зерно глитча: облака с одним материалом сбоят не одновременно.
            var block = new MaterialPropertyBlock();
            block.SetFloat(GlitchSeedId, (float)new System.Random(seed * 7919 + name.GetHashCode()).NextDouble() * 100f);
            renderer.SetPropertyBlock(block);
        }

        private static readonly int GlitchSeedId = Shader.PropertyToID("_GlitchSeed");
    }
}
