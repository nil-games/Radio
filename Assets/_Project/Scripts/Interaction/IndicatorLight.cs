using Radio.World;
using UnityEngine;
using Yarn.Unity;

namespace Radio.Interaction
{
    /// <summary>
    /// Лампочка: табличка «В ЭФИРЕ», огонёк автоответчика. Светится по команде диалога
    /// или мигает, пока висит задача.
    /// </summary>
    /// <remarks>
    /// Свечение — через собственную копию материала с включённой эмиссией: общий материал
    /// таблички иначе загорелся бы везде, где он ещё используется.
    /// Команда диалога: <c>&lt;&lt;on_air on&gt;&gt;</c> / <c>&lt;&lt;on_air off&gt;&gt;</c> (имя задаётся в инспекторе).
    /// </remarks>
    public sealed class IndicatorLight : MonoBehaviour
    {
        [Tooltip("Что светится. Пусто — рендерер на этом же объекте.")]
        [SerializeField] private Renderer target;

        [Tooltip("Дополнительный источник света. Пусто — без него.")]
        [SerializeField] private Light glow;

        [SerializeField] private Color color = new Color(1f, 0.1f, 0.05f);

        [SerializeField] private float emission = 4f;

        [Header("Когда светиться")]
        [Tooltip("Команда диалога с аргументом on/off. Пусто — без команды.")]
        [SerializeField] private string command;

        [Tooltip("Мигать, пока висит эта задача. Пусто — не мигать.")]
        [SerializeField] private string blinkWhileTask;

        [SerializeField] private float blinkPeriod = 1f;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private Material _material;
        private TaskLog _tasks;
        private bool _on;

        private void Awake()
        {
            if (target == null)
            {
                target = GetComponent<Renderer>();
            }

            if (target != null)
            {
                _material = target.material;
                _material.EnableKeyword("_EMISSION");
                _material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            if (!string.IsNullOrWhiteSpace(command))
            {
                var runner = FindAnyObjectByType<DialogueRunner>();

                if (runner != null)
                {
                    runner.AddCommandHandler(command, (System.Action<string>)OnCommand);
                }
            }

            Apply(false);
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }

        private void OnCommand(string state) =>
            _on = state == "on" || state == "true" || state == "1";

        private void Update()
        {
            var lit = _on;

            if (!string.IsNullOrWhiteSpace(blinkWhileTask))
            {
                if (_tasks == null)
                {
                    _tasks = FindAnyObjectByType<TaskLog>();
                }

                if (_tasks != null && _tasks.IsActive(blinkWhileTask))
                {
                    lit = Mathf.Repeat(Time.time, blinkPeriod) < blinkPeriod * 0.5f;
                }
            }

            Apply(lit);
        }

        private void Apply(bool lit)
        {
            if (_material != null)
            {
                _material.SetColor(EmissionId, lit ? color * emission : Color.black);
            }

            if (glow != null)
            {
                glow.enabled = lit;
            }
        }
    }
}
