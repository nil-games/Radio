using System;
using TMPro;
using UnityEngine;

namespace Radio.Dialogue
{
    /// <summary>
    /// Дрожащий текст реплик (GDD 02.7): буквы мелко трясутся, пока говорит один из
    /// перечисленных персонажей. Первый — Дежурный (ночь 2): страшно не словами, а тем,
    /// что с текстом что-то не так.
    /// </summary>
    /// <remarks>
    /// Висит рядом с окном реплики и смотрит на имя говорящего: так в Yarn не нужна
    /// разметка в каждой строке — достаточно того, что реплику произносит Дежурный.
    /// Текст сам не перестраивается: печатная машинка Yarn строит его постоянно, и
    /// вмешательство в построение ломает её. Поэтому после каждой постройки (событие
    /// TextMeshPro) запоминаем вершины, а в LateUpdate сдвигаем копию и отдаём её в меш.
    /// </remarks>
    public sealed class ShakyDialogueText : MonoBehaviour
    {
        [SerializeField] private TMP_Text lineText;

        [Tooltip("Где окно реплики показывает имя говорящего.")]
        [SerializeField] private TMP_Text nameText;

        [Tooltip("Чьи реплики дрожат — по имени, как в Yarn.")]
        [SerializeField] private string[] speakers = { "Дежурный" };

        [Tooltip("Размах дрожи, доли размера шрифта.")]
        [SerializeField] private float amplitude = 0.04f;

        [Tooltip("Сколько раз в секунду буквы меняют положение.")]
        [SerializeField] private float rate = 18f;

        private Vector3[][] _original = Array.Empty<Vector3[]>();
        private bool _shaken;

        private void OnEnable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);

        private void OnDisable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

        // Текст построен заново: это и есть «чистые» вершины, от которых считается дрожь.
        private void OnTextChanged(UnityEngine.Object changed)
        {
            if (changed != lineText)
            {
                return;
            }

            var meshes = lineText.textInfo.meshInfo;

            if (_original.Length != meshes.Length)
            {
                _original = new Vector3[meshes.Length][];
            }

            for (var m = 0; m < meshes.Length; m++)
            {
                var source = meshes[m].vertices;

                if (source == null)
                {
                    _original[m] = null;
                    continue;
                }

                if (_original[m] == null || _original[m].Length != source.Length)
                {
                    _original[m] = new Vector3[source.Length];
                }

                Array.Copy(source, _original[m], source.Length);
            }

            _shaken = false;
        }

        private void LateUpdate()
        {
            if (lineText == null || nameText == null || !lineText.isActiveAndEnabled)
            {
                return;
            }

            var shaky = IsShakySpeaker();

            if (!shaky && !_shaken)
            {
                return;
            }

            var info = lineText.textInfo;

            if (info == null || info.meshInfo.Length != _original.Length)
            {
                return;
            }

            var size = lineText.fontSize * amplitude;

            // Дрожь ступеньками, а не плавно: так похоже на сбой, а не на волну.
            var tick = Mathf.Floor(Time.unscaledTime * rate);

            for (var i = 0; i < info.characterCount; i++)
            {
                var character = info.characterInfo[i];
                var mesh = character.materialReferenceIndex;

                if (!character.isVisible || _original[mesh] == null
                    || character.vertexIndex + 3 >= _original[mesh].Length)
                {
                    continue;
                }

                var offset = shaky
                    ? new Vector3(Noise(i, tick, 0.13f), Noise(i, tick, 7.31f), 0f) * size
                    : Vector3.zero;
                var vertices = info.meshInfo[mesh].vertices;

                for (var v = 0; v < 4; v++)
                {
                    vertices[character.vertexIndex + v] = _original[mesh][character.vertexIndex + v] + offset;
                }
            }

            lineText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            _shaken = shaky;
        }

        private bool IsShakySpeaker()
        {
            // У ремарки имени нет: поле имени прячется, но хранит прошлое имя.
            if (!nameText.isActiveAndEnabled)
            {
                return false;
            }

            var name = nameText.text.Trim();

            foreach (var speaker in speakers)
            {
                if (!string.IsNullOrEmpty(speaker) && string.Equals(name, speaker, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static float Noise(int index, float tick, float salt)
        {
            var value = Mathf.Sin(index * 12.9898f + tick * 78.233f + salt) * 43758.5453f;
            return (value - Mathf.Floor(value)) * 2f - 1f;
        }
    }
}
