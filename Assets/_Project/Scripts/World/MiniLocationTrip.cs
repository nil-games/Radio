using UnityEngine;
using UnityEngine.SceneManagement;

namespace Radio.World
{
    /// <summary>
    /// Переход в мини-локацию (сон, эпизод Хтони) и обратно, GDD 02.21а. Квартира
    /// выгружается, а её снимок (<see cref="SaveData"/>, тот же, что у сохранения)
    /// держится здесь, в памяти, и по возвращении восстанавливается
    /// <see cref="SessionRestorer"/>.
    /// </summary>
    /// <remarks>
    /// Статика, а не объект сцены: она переживает выгрузку обеих сцен.
    /// </remarks>
    public static class MiniLocationTrip
    {
        private static SaveData _away;
        private static SaveData _returned;

        /// <summary>Игрок сейчас в мини-локации, и квартира ждёт его обратно.</summary>
        public static bool IsAway => _away != null;

        // Без перезагрузки домена при входе в Play статика пережила бы прошлый запуск.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _away = null;
            _returned = null;
        }

        /// <summary>Снять снимок квартиры и уйти в сцену мини-локации.</summary>
        public static void Leave(string scene)
        {
            _away = SaveData.Capture();
            SceneManager.LoadScene(scene);
        }

        /// <summary>
        /// Вернуться из мини-локации. Если в неё попали не из игры (сцену запустили
        /// в редакторе напрямую), грузится <paramref name="fallbackScene"/> с начала.
        /// </summary>
        public static void Return(string fallbackScene)
        {
            var scene = _away != null ? _away.scene : fallbackScene;

            if (_away == null)
            {
                Debug.LogWarning($"{nameof(MiniLocationTrip)}: в мини-локацию пришли не из игры — " +
                                 $"«{fallbackScene}» грузится с начала.");
            }

            _returned = _away;
            _away = null;
            SceneManager.LoadScene(scene);
        }

        /// <summary>Забрать снимок, с которым вернулись. Отдаётся один раз.</summary>
        public static bool TryTakeReturn(out SaveData snapshot)
        {
            snapshot = _returned;
            _returned = null;
            return snapshot != null;
        }
    }
}
