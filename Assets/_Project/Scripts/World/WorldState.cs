using System;
using System.Collections.Generic;
using UnityEngine;

namespace Radio.World
{
    /// <summary>
    /// Флаги мира: что игрок сделал, что узнал, как к нему относятся персонажи.
    /// Имена по соглашению ГДД: STORY_*, NPC_*, KNOW_*, ACTION_*, SECRET_*.
    /// </summary>
    /// <remarks>
    /// Три словаря вместо одного с object: Yarn оперирует ровно этими тремя типами,
    /// и раздельное хранение снимает разбор типа при каждом чтении из диалога.
    /// Незаданный флаг читается как «пусто» и не считается ошибкой — иначе каждую
    /// проверку в диалоге пришлось бы предварять объявлением.
    /// </remarks>
    public sealed class WorldState : MonoBehaviour
    {
        public enum FlagKind
        {
            Bool,
            Number,
            Text,
        }

        [Serializable]
        public struct StartupFlag
        {
            [Tooltip("Имя флага без знака доллара: STORY_FOUND_SERGEY_KEYS.")]
            public string name;

            public FlagKind kind;
            public bool boolValue;
            public float numberValue;
            public string textValue;
        }

        [Header("Стартовые флаги")]
        [Tooltip("Выставляются при запуске сцены. Нужны для отладки: можно начать ночь так, " +
                 "будто игрок уже что-то нашёл, не проходя её заново.")]
        [SerializeField] private StartupFlag[] startupFlags = Array.Empty<StartupFlag>();

        private readonly Dictionary<string, bool> _bools = new Dictionary<string, bool>();
        private readonly Dictionary<string, float> _numbers = new Dictionary<string, float>();
        private readonly Dictionary<string, string> _texts = new Dictionary<string, string>();

        /// <summary>Сработало изменение флага. Имя передаётся без знака доллара.</summary>
        public event Action<string> FlagChanged;

        private void Awake()
        {
            foreach (var flag in startupFlags)
            {
                if (string.IsNullOrWhiteSpace(flag.name))
                {
                    continue;
                }

                switch (flag.kind)
                {
                    case FlagKind.Bool:
                        Set(flag.name, flag.boolValue);
                        break;
                    case FlagKind.Number:
                        Set(flag.name, flag.numberValue);
                        break;
                    case FlagKind.Text:
                        Set(flag.name, flag.textValue);
                        break;
                }
            }
        }

        public bool Contains(string name) =>
            _bools.ContainsKey(name) || _numbers.ContainsKey(name) || _texts.ContainsKey(name);

        /// <summary>
        /// Читает флаг. Возвращает false, если флага нет или он другого типа;
        /// result при этом равен значению по умолчанию.
        /// </summary>
        /// <remarks>
        /// Проверка через is, а не сравнение typeof(T) с bool, float и string:
        /// Yarn запрашивает значение в том числе как object, и точное сравнение типов
        /// тогда не совпадает ни с одним из трёх. Флаг молча читается как «пусто»,
        /// условия в диалогах всегда ложны, а ошибки при этом нет ни одной.
        /// </remarks>
        public bool TryGet<T>(string name, out T result)
        {
            if (_bools.TryGetValue(name, out var b) && b is T asBool)
            {
                result = asBool;
                return true;
            }

            if (_numbers.TryGetValue(name, out var f) && f is T asNumber)
            {
                result = asNumber;
                return true;
            }

            if (_texts.TryGetValue(name, out var t) && t is T asText)
            {
                result = asText;
                return true;
            }

            result = default;
            return false;
        }

        public void Set(string name, bool value)
        {
            Forget(name);
            _bools[name] = value;
            FlagChanged?.Invoke(name);
        }

        public void Set(string name, float value)
        {
            Forget(name);
            _numbers[name] = value;
            FlagChanged?.Invoke(name);
        }

        public void Set(string name, string value)
        {
            Forget(name);
            _texts[name] = value;
            FlagChanged?.Invoke(name);
        }

        public void Clear()
        {
            _bools.Clear();
            _numbers.Clear();
            _texts.Clear();
        }

        /// <summary>Прямой доступ для сохранения и отладки. Менять только через Set.</summary>
        public IReadOnlyDictionary<string, bool> Bools => _bools;

        public IReadOnlyDictionary<string, float> Numbers => _numbers;

        public IReadOnlyDictionary<string, string> Texts => _texts;

        /// <summary>
        /// Флаг живёт ровно в одном словаре: смена типа через присваивание иначе
        /// оставила бы старое значение, и следующее чтение вернуло бы его.
        /// </summary>
        private void Forget(string name)
        {
            _bools.Remove(name);
            _numbers.Remove(name);
            _texts.Remove(name);
        }
    }
}
