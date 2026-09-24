using System.Collections.Generic;
using Radio.World;
using UnityEngine;
using Yarn;
using Yarn.Unity;

namespace Radio.Dialogue
{
    /// <summary>
    /// Хранилище переменных Yarn, за которым стоит состояние самой игры.
    /// Флаги уходят в <see cref="WorldState"/>, а время читается прямо из <see cref="TimeManager"/>.
    /// </summary>
    /// <remarks>
    /// Смысл в том, что второй копии состояния не существует: присваивание флага в тексте
    /// диалога меняет тот же флаг, который читает остальная игра, и синхронизировать нечего.
    /// Иначе пришлось бы гонять значения туда-обратно и ловить расхождения.
    /// </remarks>
    public sealed class RadioVariableStorage : VariableStorageBehaviour
    {
        /// <summary>Номер ночи, 1–7.</summary>
        public const string NightVariable = "$night";

        /// <summary>Минут от начала смены. Удобно для сравнений: 90 это 01:30.</summary>
        public const string TimeMinutesVariable = "$time_minutes";

        public const string HourVariable = "$hour";

        public const string MinuteVariable = "$minute";

        [Header("Ссылки")]
        [Tooltip("Если пусто, берётся из GameSession в сцене.")]
        [SerializeField] private TimeManager time;

        [SerializeField] private WorldState world;

        private void Awake()
        {
            if (time != null && world != null)
            {
                return;
            }

            var session = GameSession.Current;

            if (session == null)
            {
                Debug.LogError($"{nameof(RadioVariableStorage)}: в сцене нет {nameof(GameSession)}, " +
                               "а ссылки не заданы вручную. Диалоги не увидят состояние игры.", this);
                return;
            }

            time = time != null ? time : session.Time;
            world = world != null ? world : session.World;
        }

        public override bool TryGetValue<T>(string variableName, out T result)
        {
            if (TryGetReserved(variableName, out var reserved))
            {
                if (typeof(T) == typeof(float))
                {
                    result = (T)(object)reserved;
                    return true;
                }

                Debug.LogError($"{nameof(RadioVariableStorage)}: {variableName} — число, " +
                               $"а запрошено как {typeof(T).Name}.", this);
                result = default;
                return false;
            }

            // Разбор по видам повторяет штатное хранилище Yarn. Без ветки Smart перестают
            // работать вычисляемые переменные, а на них держится доступность объектов.
            switch (GetVariableKind(variableName))
            {
                case VariableKind.Stored:
                    if (world != null && world.TryGet(StripPrefix(variableName), out result))
                    {
                        return true;
                    }

                    // Значение по умолчанию из declare: флаг, которого игрок ещё не касался,
                    // обязан читаться, иначе каждое условие в диалоге пришлось бы страховать.
                    if (Program != null)
                    {
                        return Program.TryGetInitialValue(variableName, out result);
                    }

                    result = default;
                    return false;

                case VariableKind.Smart:
                    if (SmartVariableEvaluator == null)
                    {
                        Debug.LogError($"{nameof(RadioVariableStorage)}: некому вычислить {variableName}.", this);
                        result = default;
                        return false;
                    }

                    return SmartVariableEvaluator.TryGetSmartVariable(variableName, out result);

                default:
                    result = default;
                    return false;
            }
        }

        public override void SetValue(string variableName, bool value)
        {
            if (RejectWrite(variableName))
            {
                return;
            }

            world.Set(StripPrefix(variableName), value);
            NotifyVariableChanged(variableName, value);
        }

        public override void SetValue(string variableName, float value)
        {
            if (RejectWrite(variableName))
            {
                return;
            }

            world.Set(StripPrefix(variableName), value);
            NotifyVariableChanged(variableName, value);
        }

        public override void SetValue(string variableName, string value)
        {
            if (RejectWrite(variableName))
            {
                return;
            }

            world.Set(StripPrefix(variableName), value);
            NotifyVariableChanged(variableName, value);
        }

        public override bool Contains(string variableName) =>
            IsReserved(variableName) || (world != null && world.Contains(StripPrefix(variableName)));

        public override void Clear()
        {
            if (world != null)
            {
                world.Clear();
            }
        }

        public override void SetAllVariables(
            Dictionary<string, float> floats,
            Dictionary<string, string> strings,
            Dictionary<string, bool> bools,
            bool clear = true)
        {
            if (world == null)
            {
                return;
            }

            if (clear)
            {
                world.Clear();
            }

            foreach (var pair in floats)
            {
                SetValue(pair.Key, pair.Value);
            }

            foreach (var pair in strings)
            {
                SetValue(pair.Key, pair.Value);
            }

            foreach (var pair in bools)
            {
                SetValue(pair.Key, pair.Value);
            }
        }

        public override (Dictionary<string, float> FloatVariables,
                         Dictionary<string, string> StringVariables,
                         Dictionary<string, bool> BoolVariables) GetAllVariables()
        {
            var floats = new Dictionary<string, float>();
            var strings = new Dictionary<string, string>();
            var bools = new Dictionary<string, bool>();

            if (world != null)
            {
                foreach (var pair in world.Numbers)
                {
                    floats[AddPrefix(pair.Key)] = pair.Value;
                }

                foreach (var pair in world.Texts)
                {
                    strings[AddPrefix(pair.Key)] = pair.Value;
                }

                foreach (var pair in world.Bools)
                {
                    bools[AddPrefix(pair.Key)] = pair.Value;
                }
            }

            // Время наружу не отдаём: его хранит TimeManager, и попавшее в сохранение
            // значение при загрузке разошлось бы с настоящими часами.
            return (floats, strings, bools);
        }

        private static string StripPrefix(string variableName) =>
            variableName.Length > 0 && variableName[0] == '$'
                ? variableName.Substring(1)
                : variableName;

        private static string AddPrefix(string flagName) => "$" + flagName;

        private static bool IsReserved(string variableName) =>
            variableName == NightVariable
            || variableName == TimeMinutesVariable
            || variableName == HourVariable
            || variableName == MinuteVariable;

        private bool TryGetReserved(string variableName, out float value)
        {
            value = 0f;

            if (time == null || !IsReserved(variableName))
            {
                return false;
            }

            switch (variableName)
            {
                case NightVariable:
                    value = time.Night;
                    return true;
                case TimeMinutesVariable:
                    value = time.Minutes;
                    return true;
                case HourVariable:
                    value = time.Hour;
                    return true;
                default:
                    value = time.Minute;
                    return true;
            }
        }

        /// <summary>
        /// Часы двигает игра, а не текст диалога: присваивание в переменные времени молча
        /// разошлось бы с настоящими часами, поэтому это ошибка, а не тихий игнор.
        /// Для сдвига времени есть команда advance_time.
        /// </summary>
        private bool RejectWrite(string variableName)
        {
            if (IsReserved(variableName))
            {
                Debug.LogError($"{nameof(RadioVariableStorage)}: {variableName} менять из диалога нельзя. " +
                               "Для сдвига времени есть команда advance_time.", this);
                return true;
            }

            if (world == null)
            {
                Debug.LogError($"{nameof(RadioVariableStorage)}: нет состояния мира, {variableName} потеряется.", this);
                return true;
            }

            return false;
        }
    }
}
