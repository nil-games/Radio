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
| `Scenes/Dream_N1` | Сон-Изнанка ночи 1 (мини-локация, квартира выгружается) |

## Системы и где их искать

| Система | Главные файлы | Связи |
|---|---|---|
| Сессия | `World/GameSession` (статика `Current`) + `TimeManager` + `WorldState` на одном объекте | Всё берёт часы и флаги отсюда |
| Часы смены | `World/TimeManager` — ночь 1–7, минуты 0–360, `TotalMinutes` сквозь ночи | Двигаются только из диалогов (`advance_time`, `advance_time_to`) |
| События по часам | `World/GameTimeMark` (`GameTimeMark` «ЧЧ:ММ[:НН]» + `TimedEvent`); владельцы: `TaskAtTime`, `ScheduleRunner`, `Interaction/PhoneRinger`, `DoorKnocker`, `CorridorGlitch` | Отладочный прыжок → `DebugJumped` отменяет ранние; `StoryBeatSkipper` ставит флаги пройденных шагов (`Data/StoryBeats.asset`) |
| Флаги и Yarn | `World/WorldState` (bool/float/string), `Dialogue/RadioVariableStorage` — все `$переменные` Yarn живут в WorldState; `$night`, `$time_minutes` только для чтения | Шкалы сюжета — `World/StoryStats` (Crime, Cult, GirlFriend, CatHunger) |
| Диалоги | `Dialogue/DialogueController` (запуск узлов, `DialogueFinished`), команды: `RadioCommands`, `StageDirector` (show/hide), `TapeDeck`, `CameraShots`, `DialoguePortrait`; Yarn — `Assets/_Project/Yarn` | Мини-игры и сон ждут конца диалога |
| Задачи | `World/TaskLog` (+ флаги `TASK_<id>`), каталог `Data/Tasks.asset`, HUD `UI/TaskHud` | `task_add` / `task_done` из Yarn |
| Эфир (мини-игра «Эфир») | `Broadcast/*` (`BroadcastGame` — логика, `BroadcastData` — CSV из `Data/Broadcast`), запуск — `Minigames/MinigameHost` (`start_minigame`) | Итоги копятся во флагах `BROADCAST_POINTS`, `MONEY`. Старая игра «Провода» (`WirePuzzle*`) не запускается |
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
- **Всё событийное — на часах смены** (+ по желанию реальная задержка), не на секундах от
  загрузки и не на одних флагах: иначе не проверить отладкой.
- Интерфейс строится кодом (свои холсты, TMP-шрифт с кириллицей `LiberationSans SDF RU`).
- Unity: `TryGetComponent`, не `GetComponent ?? AddComponent` (фальшивый null). У
  `MaskableGraphic` нужен `CanvasRenderer`. Имя класса = имя файла. Ссылки из инспектора
  в приоритете, `GameSession.Current` / `FindAnyObjectByType` — подстраховка.
- Комментарии объясняют «почему», `<summary>` на русском, стиль — как в соседних файлах.

## Правила Yarn

- Двоеточие в ремарке экранировать: `(Он смотрит\: никого.)` — иначе Yarn примет текст до
  `:` за имя говорящего.
- Выбранный вариант не повторяется репликой (`runSelectedOptionAsLine` выключен): тело
  варианта начинается с ответа.
- Реплики слушателей в `Broadcast.xlsx` — строго в голосе из «Характер и речь» (`Docs/Персонажи.md`).
- В расписании ночи не ставить подряд два события одного типа (дверь/дверь, эфир/эфир).

## Работа с Unity через MCP

- **Никогда не компилировать во время Play**: сначала `stop`, потом `refresh_unity`.
- После правки скриптов — `refresh_unity(compile=request)` и `read_console` на ошибки.
- Сцены и ассеты менять через `execute_code` с `SerializedObject`, после — сохранить сцену.
- Проверка в Play: отладочные прыжки по часам и прямые вызовы через `execute_code`.

## Отладка в игре (только debug-сборка)

- **Правый Ctrl** — сводка параметров; цифры ЧЧММ или ЧЧММНН — перевести часы (ночь НН).
- В сводке **B + цифра** — сразу эфир N текущей ночи; в эфире **F9** — к концу.
- **Правый Ctrl + S** — сохранить сейчас. Сохранения: `%USERPROFILE%\AppData\LocalLow\nil-games\Radio 103FM\` (`save.json`, `night_<N>.json`).
- Сцена квартиры, запущенная из редактора, = новая игра (перезапишет сохранение).

## Как добавить в ночь…

- **Событие по времени:** строка в `Docs/Расписание.xlsx` → компонент (`TaskAtTime` /
  `PhoneRinger` / `DoorKnocker` / запуск в `MinigameHost`) с `GameTimeMark` → узел Yarn
  (в конце — `advance_time_to`) → шаг в `StoryBeats.asset` → проверить отладочным прыжком.
- **Гостя:** `DoorKnocker` (+ `doneFlag`, `giveUpAfter`) у двери, узел визита в Yarn.
- **Элемент эфира** (звонок, объявление): лист «Сюжетные элементы» в `Broadcast.xlsx` → `StoryElements.csv`.

## Git

Работаем в `dev-nil`. Коммитить только по просьбе автора; затем `git push origin dev-nil`
и `git push origin dev-nil:main` (без PR).
