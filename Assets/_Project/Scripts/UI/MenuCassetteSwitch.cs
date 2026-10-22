using Radio.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Radio.UI
{
    /// <summary>
    /// «◄ Переключить кассету ►» в углу меню: обе стрелки ставят другую случайную песню
    /// из той же папки, что играет в меню. Над стрелками — название того, что играет.
    /// </summary>
    /// <remarks>
    /// Стрелок две, хотя делают они одно и то же: так привычнее рукам, а «назад»
    /// в случайном плейлисте всё равно означал бы просто «другую».
    /// </remarks>
    public sealed class MenuCassetteSwitch : MonoBehaviour
    {
        [SerializeField] private PlaylistPlayer player;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        [Tooltip("Название играющего трека — имя файла без расширения.")]
        [SerializeField] private TextMeshProUGUI trackLabel;

        [SerializeField] private string trackFormat = "♪ {0}";

        private void Awake()
        {
            if (player == null || previousButton == null || nextButton == null)
            {
                Debug.LogError($"{nameof(MenuCassetteSwitch)}: не заданы проигрыватель или кнопки.", this);
                enabled = false;
                return;
            }

            previousButton.onClick.AddListener(player.Skip);
            nextButton.onClick.AddListener(player.Skip);
            player.TrackStarted += ShowTrack;

            // Проигрыватель мог запустить первый трек раньше, чем мы подписались.
            ShowTrack(player.Current);
        }

        private void ShowTrack(AudioClip clip)
        {
            if (trackLabel != null)
            {
                trackLabel.text = clip != null ? string.Format(trackFormat, clip.name) : string.Empty;
            }
        }

        private void OnDestroy()
        {
            if (player == null)
            {
                return;
            }

            player.TrackStarted -= ShowTrack;

            if (previousButton != null)
            {
                previousButton.onClick.RemoveListener(player.Skip);
            }

            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(player.Skip);
            }
        }
    }
}
