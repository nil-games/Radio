using System;
using System.Collections;
using UnityEngine;

namespace Radio.Interaction
{
    /// <summary>
    /// Обёртка над Animator для взаимодействий. Сценарий просит проиграть состояние
    /// и получает колбэк по окончании, не зная ничего про устройство аниматора.
    /// </summary>
    public sealed class InteractionAnimator : MonoBehaviour
    {
        [Tooltip("Аниматор предмета. Если пусто — ищется на этом объекте и его потомках.")]
        [SerializeField] private Animator animator;

        [Tooltip("Слой аниматора, в котором лежат состояния взаимодействия.")]
        [SerializeField] private int layer;

        private Coroutine _running;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        /// <summary>
        /// Проигрывает состояние и вызывает onComplete по его окончании.
        /// Если аниматора нет, колбэк срабатывает немедленно.
        /// </summary>
        public void Play(string stateName, Action onComplete)
        {
            if (animator == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (_running != null)
            {
                StopCoroutine(_running);
            }

            _running = StartCoroutine(PlayRoutine(stateName, onComplete));
        }

        private IEnumerator PlayRoutine(string stateName, Action onComplete)
        {
            animator.Play(stateName, layer, 0f);

            // Ждём кадр: состояние переключается не мгновенно, и сразу после Play
            // аниматор ещё отдаёт предыдущее состояние.
            yield return null;

            var state = animator.GetCurrentAnimatorStateInfo(layer);
            yield return new WaitForSeconds(state.length);

            _running = null;
            onComplete?.Invoke();
        }
    }
}
