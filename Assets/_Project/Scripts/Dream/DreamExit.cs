using System.Collections;
using Radio.UI;
using Radio.World;
using UnityEngine;

namespace Radio.Dream
{
    /// <summary>
    /// Выход из сна (Сон-Изнанка, GDD 02.21б): игрок входит в жёлтую точку — экран
    /// гаснет, и игра возвращается в квартиру, где он просыпается. Заодно проявляет
    /// сон из черноты при входе в сцену.
    /// </summary>
    /// <remarks>
    /// Временная схема, пока у сна нет забега: одна точка у колеса обозрения.
    /// Возврат и восстановление квартиры — <see cref="MiniLocationTrip"/>.
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    public sealed class DreamExit : MonoBehaviour
    {
        [Tooltip("Куда вернуться, если сон запущен в редакторе напрямую, а не из игры.")]
        [SerializeField] private string fallbackScene = "Apartment";

        [SerializeField] private float fadeInSeconds = 1.5f;

        [SerializeField] private float fadeOutSeconds = 1.2f;

        [Tooltip("Порядок холста затемнения: поверх всего.")]
        [SerializeField] private int sortingOrder = 200;

        private ScreenFader _fader;
        private Coroutine _fadeIn;
        private bool _leaving;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            _fader = ScreenFader.Create(transform, sortingOrder, 1f);
        }

        private void Start() => _fadeIn = StartCoroutine(_fader.FadeTo(0f, fadeInSeconds));

        private void OnTriggerEnter(Collider other)
        {
            if (_leaving || other.GetComponentInParent<DreamController>() == null)
            {
                return;
            }

            StartCoroutine(Leave(other.GetComponentInParent<DreamController>()));
        }

        private IEnumerator Leave(DreamController player)
        {
            _leaving = true;

            if (_fadeIn != null)
            {
                StopCoroutine(_fadeIn);
            }

            player.enabled = false;
            yield return _fader.FadeTo(1f, fadeOutSeconds);
            MiniLocationTrip.Return(fallbackScene);
        }
    }
}
