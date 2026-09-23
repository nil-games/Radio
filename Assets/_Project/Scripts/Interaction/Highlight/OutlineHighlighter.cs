using System;
using UnityEngine;

namespace Radio.Interaction.Highlight
{
    /// <summary>
    /// Обводка шейдером-оболочкой. Материал обводки добавляется вторым в массив материалов
    /// рендерера и убирается обратно — сами материалы предмета при этом не меняются.
    /// </summary>
    public sealed class OutlineHighlighter : InteractableHighlighter
    {
        [Tooltip("Материал обводки на шейдере Radio/InteractionOutline. Общий для всех предметов.")]
        [SerializeField] private Material outlineMaterial;

        private Material[][] _withoutOutline;
        private Material[][] _withOutline;
        private bool _shown;

        protected override void Awake()
        {
            base.Awake();

            if (!enabled)
            {
                return;
            }

            if (outlineMaterial == null)
            {
                Debug.LogError($"{nameof(OutlineHighlighter)}: не задан материал обводки. Подсветка отключена.", this);
                enabled = false;
                return;
            }

            // Оба набора готовим заранее: переключение подсветки не должно ничего выделять в куче.
            _withoutOutline = new Material[Renderers.Length][];
            _withOutline = new Material[Renderers.Length][];

            for (var i = 0; i < Renderers.Length; i++)
            {
                // sharedMaterials возвращает копию массива, поэтому её можно спокойно хранить.
                // Именно sharedMaterials, а не materials: последний создал бы копии материалов
                // на каждом предмете и выбил бы их из SRP Batcher.
                var original = Renderers[i].sharedMaterials;
                _withoutOutline[i] = original;

                var extended = new Material[original.Length + 1];
                Array.Copy(original, extended, original.Length);
                extended[original.Length] = outlineMaterial;
                _withOutline[i] = extended;
            }
        }

        public override void Show()
        {
            if (_shown)
            {
                return;
            }

            _shown = true;
            Apply(_withOutline);
        }

        public override void Hide()
        {
            if (!_shown)
            {
                return;
            }

            _shown = false;
            Apply(_withoutOutline);
        }

        private void OnDisable()
        {
            // Иначе подсветка останется висеть на предмете, который выключили под прицелом.
            Hide();
        }

        private void Apply(Material[][] sets)
        {
            for (var i = 0; i < Renderers.Length; i++)
            {
                Renderers[i].sharedMaterials = sets[i];
            }
        }
    }
}
