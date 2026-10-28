# Радио 103 FM — карта проекта

Нарративный симулятор ночного радиоведущего, конец 90-х. Unity 6.4, URP, Input System,
Yarn Spinner 3.2.8. Весь код в `Assets/_Project/Scripts` (одна сборка Assembly-CSharp,
пространства имён `Radio.*`). Общаемся и пишем документы и комментарии **на русском**.

Документы: начинать с `Docs/Index.md` (карта всех доков). Источник правды по дизайну —
`Docs/GDD.md`; механики там со статусами (02.xx).

> Этот файл ведёт Claude. Обновлять при изменении архитектуры: новая система, новое
> правило, новая точка входа. Держать коротким — он загружается в каждую сессию.

## Сцены

| Сцена | Что |
|---|---|
| `Scenes/MainMenu` | Меню (`UI/MainMenuUI`), «Продолжить» грузит сохранение, «Играть» — вступление (`IntroVideo`) → квартира |
| `Scenes/Apartment` | Вся игра: квартира-радиоточка, ночи идут без перезагрузки сцены |
| `Scenes/Dream_N1`, `Dream_N2` | Сны-Изнанки (мини-локации, квартира выгружается); ночь 3 пока снова `Dream_N1` |

## Системы и где их искать

| Система | Главные файлы | Связи |
|---|---|---|
| Сессия | `World/GameSession` (статика `Current`) + `TimeManager` + `WorldState` на одном объекте | Всё берёт часы и флаги отсюда |
| Часы смены | `World/TimeManager` — ночь 1–7, минуты 0–360, `TotalMinutes` сквозь ночи | Двигаются только из диалогов (`advance_time`, `advance_time_to`) |
| События по часам | `World/GameTimeMark` (`GameTimeMark` «ЧЧ:ММ[:НН]» + `TimedEvent`); владельцы: `TaskAtTime`, `ScheduleRunner`, `Interaction/PhoneRinger`, `DoorKnocker`, `CorridorGlitch` | Отладочный прыжок → `DebugJumped` отменяет ранние; `StoryBeatSkipper` ставит флаги пройденных шагов (`Data/StoryBeats.asset`) |
| Флаги и Yarn | `World/WorldState` (bool/float/string), `Dialogue/RadioVariableStorage` — все `$переменные` Yarn живут в WorldState; `$night`, `$time_minutes` только для чтения | Шкалы сюжета — `World/StoryStats` (Crime = Криминал ⟷ Полиция, Cult, GirlFriend, Apophenia, CatHunger, счётчики Garage, Alice) |
| Диалоги | `Dialogue/DialogueController` (запуск узлов, `DialogueFinished`), команды: `RadioCommands`, `StageDirector` (show/hide), `TapeDeck` (`play_mood`, `play_sfx` — клип по имени, нет клипа = тишина), `CameraShots`, `DialoguePortrait`; дрожащий текст — `ShakyDialogueText` (по имени говорящего); Yarn — `Assets/_Project/Yarn` | Мини-игры и сон ждут конца диалога |
| Задачи | `World/TaskLog` (+ флаги `TASK_<id>`), каталог `Data/Tasks.asset`, HUD `UI/TaskHud` | `task_add` / `task_done` из Yarn |
| Телефон | `Interaction/PhoneRinger` (звонки по часам, «обрывается сам», «не звенит при флаге»), `Dialogue/PhoneInteractable`, маршруты — `Yarn/Phone.yarn` | Звонок по часам — с условием `phone_was_ringing()` |
| Гости у двери | `Interaction/DoorKnocker` (время, `doneFlag`, «только если флаг», «уходит сам» → узел), `EntranceDoor` → «Visitors» (кто → узел визита) | Портрет — `<<character id>>` |
| Компьютер | `UI/ComputerMail` (письмо по флагу; вложение-картинка; узел мыслей после закрытия письма) | |
| Квартира по ночам | `World/ActivateAtTime`, `ActivateOnFlag` (вкл/выкл по флагу), `AmbientLoop` (фоновый звук до флага), `Interaction/RadioVoiceTrigger` (частота + задача → узел) | Пропы ночи 2 — `Night2Props` в сцене |
| Эфир (мини-игра «Эфир») | `Broadcast/*` (`BroadcastGame` — логика, `BroadcastData` — CSV из `Data/Broadcast`), запуск — `Minigames/MinigameHost` (`start_minigame`, «Запуски» по ночам), найденные кассеты и снятая реклама — `BroadcastSettings` (по флагу). Сюжетные элементы: звонок, объявление, концерт (длинный, заявки ждут), событие (само в свою секунду, пока идёт концерт); условия `FLAG;!FLAG;FLAG=a|b`; `<<broadcast_skip>>`, `<<broadcast_quick>>` | Итоги копятся во флагах `BROADCAST_POINTS`, `MONEY`. Старая игра «Провода» (`WirePuzzle*`) не запускается |
| Сон / смена ночи | `World/SleepSequence` (`go_to_sleep`): затемнение → сцена сна или текст `Night{N}_Dream` → `Night{N}_DreamEnd` → утро → титры → `BeginNextNight` → пробуждение | Сцены сна по ночам — поле «Сцены сна» |
| Мини-локации | `World/MiniLocationTrip` (уход/возврат по снимку), `Dream/DreamExit` (жёлтая точка) | Снимок тот же, что у сохранения |
| Сохранения (GDD 02.12) | `World/SaveData` (снимок), `SaveSystem` (диск), `SessionRestorer` (единственное место восстановления, Start с порядком 1000), `AutoSaver` (начало ночи + флаги `StoryBeats` в спокойный момент), `ISaveBlocker` | Событие `GameSession.Restored` |
| Игрок | `Player/FirstPersonController`, `Interaction/PlayerInteractor`, `Interactable` (база), `SitPoint`, `DeskView` | Занятость игрока — `TimedEvent.PlayerBusy` |
| Сон-Изнанка визуал | `Dream/PointCloudSurface` (меши → облако точек), шейдер `Shaders/Dream_PointCloud` (мерцание, сканлайны, нити, волны, глитч), материалы `Materials/Dream/` | `PointCloudSkip` — исключить меш |

## Правила кода

- **Сохранение.** Состояние объекта — во флагах `OBJ_*`: писать при изменении, читать по
  `GameSession.Restored` (подписка в Start). Каждый `TimedEvent` — с постоянным ключом:
  `new TimedEvent(mark, TimedEvent.KeyFor(this, i))`. Что нельзя восстановить посреди —
  `ISaveBlocker`. Новый сюжетный шаг — в `Data/StoryBeats.asset` (по нему же автосохранение).
- **Ночь — шагом в 30 минут, эфир — 30 игровых минут; сон с 06:00.** Пустые получасы (свободные окна) часы перескакивают. Хтонь (коридор, Дежурный) — поверх расписания, часы не двигает.
- **Всё событийное — на часах смены** (+ по желанию реальная задержка), не на секундах от
  загрузки и не на одних флагах: иначе не проверить отладкой.
- Интерфейс строится кодом (свои холсты, TMP-шрифт с кириллицей `LiberationSans SDF RU`).
- Unity: `TryGetComponent`, не `GetComponent ?? AddComponent` (фальшивый null). У
  `MaskableGraphic` нужен `CanvasRenderer`. Имя класса = имя файла. Ссылки из инспектора
  в приоритете, `GameSession.Current` / `FindAnyObjectByType` — подстраховка.
- Комментарии объясняют «почему», `<summary>` на русском, стиль — как в соседних файлах.

## Правила Yarn

- Двоеточие в ремарке **и в варианте ответа** экранировать: `(Он смотрит\: никого.)`,
  `-> Про погоду\: дождь` — иначе Yarn примет текст до `:` за имя говорящего.
- Переменную объявлять (`declare`) один раз на весь проект — повтор ломает импорт всего проекта.
- Задачи — свои id на каждую ночь (`go_to_sleep_n2`): сделанная задача повторно не появляется.
- Выбранный вариант не повторяется репликой (`runSelectedOptionAsLine` выключен): тело
  варианта начинается с ответа.
- Реплики слушателей в `Broadcast.xlsx` — строго в голосе из «Характер и речь» (`Docs/Персонажи.md`).
- В расписании ночи не ставить подряд два события одного типа (дверь/дверь, эфир/эфир).

## Работа с Unity через MCP

- **Никогда не компилировать во время Play**: сначала `stop`, потом `refresh_unity`.
- После правки скриптов — `refresh_unity(compile=request)` и `read_console` на ошибки.
- **Ошибки Yarn в консоль не выводятся.** После правки `.yarn` — `refresh_unity`, затем
  проверить `YarnProject.Program.Nodes.Count` (0 = импорт упал) и `ImportData.diagnostics`
  у импортёра `Radio.yarnproject`. Play подхватывает только импортированное.
- `execute_code` → `replay` ненадёжен (история из 50 записей сдвигается): повторять код целиком.
- Сцены и ассеты менять через `execute_code` с `SerializedObject`, после — сохранить сцену.
- Проверка в Play: отладочные прыжки по часам и прямые вызовы через `execute_code`.

## Отладка в игре (только debug-сборка)

- **Правый Ctrl** — сводка параметров; цифры ЧЧММ или ЧЧММНН — перевести часы (ночь НН).
- В сводке **B + цифра** — сразу эфир N текущей ночи; в эфире **F9** — к концу.
- В сводке **P** — эфир пройден: идущий заканчивается сразу, открытый по часам засчитывается
  без игры (флаг, часы, узел «после эфира» — как после настоящего). Сюжетные элементы эфира
  при этом пропадают (`MinigameHost.DebugCompleteBroadcast`).
- **Правый Ctrl + S** — сохранить сейчас. Сохранения: `%USERPROFILE%\AppData\LocalLow\nil-games\Radio 103FM\` (`save.json`, `night_<N>.json`).
- Сцена квартиры, запущенная из редактора, = новая игра (перезапишет сохранение).

## Как добавить в ночь…

- **Событие по времени:** строка в `Docs/Расписание.xlsx` → компонент (`TaskAtTime` /
  `PhoneRinger` / `DoorKnocker` / запуск в `MinigameHost`) с `GameTimeMark` → узел Yarn
  (в конце — `advance_time_to`) → шаг в `StoryBeats.asset` → проверить отладочным прыжком.
- **Гостя:** `DoorKnocker` (+ `doneFlag`, `giveUpAfter`) у двери, узел визита в Yarn.
- **Элемент эфира** (звонок, объявление): лист «Сюжетные элементы» в `Broadcast.xlsx` → `StoryElements.csv`.
- **Заглушку** (нет звука, портрета, модели): работающая механика + строка в `Docs/Нехватающие_ассеты.md`
  (имя клипа или объекта, куда положить). Пропы-заглушки — примитивы с `_STUB` в имени.

## Git

Работаем в `dev-nil`. Коммитить только по просьбе автора; затем `git push origin dev-nil`
и `git push origin dev-nil:main` (без PR).
