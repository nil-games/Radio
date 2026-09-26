using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Yarn.Unity;

namespace Radio.Dialogue
{
    /// <summary>
    /// Обёртка над стандартным окном выбора: пока играет кассета, показывает варианты
    /// погашенными, а когда запись кончилась — сама выбирает скрытый вариант с тегом
    /// <c>#tape_end</c>, и диалог заново строит список уже с доступными вариантами.
    /// </summary>
    /// <remarks>
    /// Обёртка, а не наследник: <see cref="OptionsPresenter"/> запечатан. В списке
    /// презентеров Dialogue Runner стоит она, а внутреннее окно туда не добавляется —
    /// иначе выбор показывался бы дважды.
    /// <para/>
    /// В Yarn это выглядит так — обычные варианты гасятся условием, а скрытый доступен,
    /// только пока запись играет:
    /// <code>
    /// -> MIC — микрофон &lt;&lt;if not tape_playing()&gt;&gt;
    /// -> … &lt;&lt;if tape_playing()&gt;&gt; #tape_end
    ///     &lt;&lt;jump Mixer_Channels&gt;&gt;
    /// </code>
    /// </remarks>
    public sealed class TapeGatedOptionsPresenter : DialoguePresenterBase
    {
        private const string TapeEndTag = "tape_end";

        [Header("Ссылки")]
        [Tooltip("Стандартное окно выбора, которое на самом деле рисует варианты.")]
        [SerializeField] private OptionsPresenter inner;

        [Tooltip("Кассетник, конца записи которого ждём.")]
        [SerializeField] private TapeDeck deck;

        private void Awake()
        {
            if (inner == null || deck == null)
            {
                Debug.LogError($"{nameof(TapeGatedOptionsPresenter)}: не заданы окно выбора или кассетник. " +
                               "Выбор в диалогах не будет показываться.", this);
            }
        }

        public override YarnTask OnDialogueStartedAsync() =>
            inner != null ? inner.OnDialogueStartedAsync() : YarnTask.CompletedTask;

        public override YarnTask OnDialogueCompleteAsync() =>
            inner != null ? inner.OnDialogueCompleteAsync() : YarnTask.CompletedTask;

        /// <summary>Строки тоже пересылаем: окно выбора показывает над вариантами последнюю реплику.</summary>
        public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token) =>
            inner != null ? inner.RunLineAsync(line, token) : YarnTask.CompletedTask;

        public override async YarnTask<DialogueOption> RunOptionsAsync(DialogueOption[] options, LineCancellationToken token)
        {
            if (inner == null)
            {
                return null;
            }

            DialogueOption tapeEnd = null;
            var shown = new List<DialogueOption>(options.Length);

            foreach (var option in options)
            {
                if (tapeEnd == null && HasTag(option, TapeEndTag))
                {
                    tapeEnd = option;
                }
                else
                {
                    shown.Add(option);
                }
            }

            // Скрытый вариант недоступен — значит, запись не играет и ждать нечего.
            if (tapeEnd == null || !tapeEnd.IsAvailable || deck == null)
            {
                return await inner.RunOptionsAsync(shown.ToArray(), token);
            }

            // Запись доиграла, пока диалог дошёл до выбора: сразу перестраиваем список.
            if (!deck.IsPlaying)
            {
                return tapeEnd;
            }

            return await WaitForTapeAsync(shown, tapeEnd, token);
        }

        private async YarnTask<DialogueOption> WaitForTapeAsync(List<DialogueOption> shown, DialogueOption tapeEnd,
                                                                LineCancellationToken token)
        {
            // Своя отмена поверх общей: ею закрываем окно, когда запись кончилась.
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(token.NextContentToken);

            var innerToken = new LineCancellationToken
            {
                NextContentToken = stop.Token,
                HurryUpToken = token.HurryUpToken,
            };

            // Окно выбора не показывается вовсе, если доступных вариантов нет, — а пока
            // играет запись, погашены как раз все. Поэтому отдаём ему копии, помеченные
            // доступными, и сразу после раскладки возвращаем пунктам настоящие варианты.
            var copies = new DialogueOption[shown.Count];

            for (var i = 0; i < shown.Count; i++)
            {
                copies[i] = new DialogueOption
                {
                    DialogueOptionID = shown[i].DialogueOptionID,
                    TextID = shown[i].TextID,
                    Line = shown[i].Line,
                    IsAvailable = true,
                };
            }

            DialogueOption chosen = null;
            var done = false;

            async YarnTask Run()
            {
                chosen = await inner.RunOptionsAsync(copies, innerToken);
                done = true;
            }

            // До первого ожидания окно раскладывает пункты синхронно,
            // так что к следующей строке они уже созданы.
            Run().Forget();
            RestoreAvailability(shown);

            while (!done && deck.IsPlaying && !token.NextContentToken.IsCancellationRequested)
            {
                await YarnTask.Yield();
            }

            if (!done)
            {
                stop.Cancel();

                while (!done)
                {
                    await YarnTask.Yield();
                }
            }

            if (token.NextContentToken.IsCancellationRequested)
            {
                return null;
            }

            // Нажать можно было только настоящий доступный вариант: погашенные не кликаются.
            if (chosen != null)
            {
                foreach (var option in shown)
                {
                    if (option.DialogueOptionID == chosen.DialogueOptionID)
                    {
                        return option;
                    }
                }
            }

            return tapeEnd;
        }

        /// <summary>
        /// Возвращает пунктам настоящие варианты: заодно пункт сам применит зачёркивание
        /// и погашенный вид, как для любого недоступного варианта.
        /// </summary>
        private void RestoreAvailability(List<DialogueOption> shown)
        {
            foreach (var item in inner.GetComponentsInChildren<OptionItem>())
            {
                foreach (var option in shown)
                {
                    if (option.DialogueOptionID == item.Option.DialogueOptionID)
                    {
                        item.Option = option;
                        break;
                    }
                }
            }
        }

        private static bool HasTag(DialogueOption option, string tag)
        {
            var metadata = option.Line.Metadata;

            if (metadata == null)
            {
                return false;
            }

            foreach (var entry in metadata)
            {
                if (entry == tag)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
