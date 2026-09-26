using TMPro;
using UnityEngine;

namespace Radio.UI
{
    /// <summary>
    /// Программа с собственным содержимым в окне компьютера. Значок без программы
    /// просто показывает свой текст; значок с программой отдаёт окно ей.
    /// </summary>
    public abstract class ComputerProgram : MonoBehaviour
    {
        /// <summary>Окно открыто на этой программе. Текст окна уже пуст, заголовок выставлен.</summary>
        /// <param name="body">Текст окна. Программа вольна писать в него что угодно.</param>
        public abstract void Open(TextMeshProUGUI body);

        /// <summary>Окно закрыли или переключили на другой значок: убрать за собой.</summary>
        public abstract void Close();
    }
}
