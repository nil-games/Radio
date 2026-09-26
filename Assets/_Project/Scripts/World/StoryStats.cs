namespace Radio.World
{
    /// <summary>
    /// Числовые параметры сюжета, от которых зависит концовка. Живут в <see cref="WorldState"/>
    /// обычными числовыми флагами, так что диалог меняет их простым set: $Crime = $Crime + 1.
    /// </summary>
    /// <remarks>
    /// Диапазоны описаны здесь, а не в инспекторе: их знают и код, и объявления в Stats.yarn,
    /// и расхождение между ними заметить было бы некому. Выход за границы не ошибка —
    /// значение молча прижимается к краю, иначе каждое изменение в диалоге пришлось бы
    /// предварять проверкой.
    /// </remarks>
    public static class StoryStats
    {
        public const string Crime = "Crime";
        public const string Cult = "Cult";
        public const string GirlFriend = "GirlFriend";
        public const string CatHunger = "CatHunger";

        public readonly struct Stat
        {
            public readonly string Name;
            public readonly float Min;
            public readonly float Max;
            public readonly float Start;

            /// <summary>
            /// Изменение заметно игроку: в углу экрана появляется «Это будет иметь последствия».
            /// </summary>
            public readonly bool Consequential;

            public Stat(string name, float min, float max, float start, bool consequential)
            {
                Name = name;
                Min = min;
                Max = max;
                Start = start;
                Consequential = consequential;
            }
        }

        public static readonly Stat[] All =
        {
            new Stat(Crime, -5f, 5f, 0f, true),
            new Stat(Cult, -5f, 5f, 0f, true),
            new Stat(GirlFriend, -5f, 5f, 0f, true),
            new Stat(CatHunger, 0f, 100f, 0f, false),
        };

        public static bool TryFind(string name, out Stat stat)
        {
            foreach (var candidate in All)
            {
                if (candidate.Name == name)
                {
                    stat = candidate;
                    return true;
                }
            }

            stat = default;
            return false;
        }

        /// <summary>Прижать значение к диапазону параметра. Обычные флаги проходят как есть.</summary>
        public static float Clamp(string name, float value) =>
            TryFind(name, out var stat)
                ? UnityEngine.Mathf.Clamp(value, stat.Min, stat.Max)
                : value;
    }
}
