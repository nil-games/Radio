# Передача работы: сессия Claude Code 26–27.09.2026

Конспект сессии и состояние проекта на момент передачи. Нужен, чтобы продолжить
работу в новой сессии Claude Code или Codex без потери контекста.
Общий контекст проекта: `Docs/AI/UnityProjectContext.md`. Дизайн: `Docs/GDD.md`.
Таблица ночей: `F:\UnityProjects\RadioAssets\Сценарии\Ne_ta_chastota_Nights_1-3.xlsx`.
Сценарии диалогов: `F:\UnityProjects\RadioAssets\Сценарии\*.md`.

---

## 1. Как сейчас идёт ночь 1 (реализовано)

| Время | Событие | Как запускается |
|---|---|---|
| 00:00 + 2 с | Звонит телефон | `PhoneRinger.ringAt` — отметка на часах + задержка |
| 00:00 | Разговор с начальником | игрок снимает трубку; `Phone.yarn` → `Night1.yarn` |
| 00:00 | Задачи «Включить Tape A» → «Начать эфир» | Yarn: `DeskDevices.yarn` (микшер) |
| 00:00 | Мини-игра «Эфир» (в коде — Wire/«Провод»), 2 уровня | выбор «Начать эфир» на микшере |
| → 00:30 | Победа в «Эфире» ставит часы на 00:30 | `MinigameHost.timeAfterWin` |
| 00:30 | Стук Зины (Knock1) + задача «Кто-то постучал» | `DoorKnocker.arriveAt = 00:30` |
| — | Второй стук при первом вставании из-за стола (F) | `DoorKnocker`, один раз |
| — | Открыть дверь → разговор с бабкой Зиной | `EntranceDoor` → узел `Night1_GrannyVisit` |
| → 01:00 | Конец визита ставит часы на 01:00 | Yarn: `<<advance_time_to 1 0>>` |
| 01:00 | На столе появляется банка (осмотр один раз) | `ActivateAtTime` на `EvenyProps` |
| 01:00 | Задача «Вернуться за рабочий стол» | `TaskAtTime` на `PresenterChairSeat` |
| — | Сел за стол → звук ISQ, задача «Проверить компьютер», письмо из полиции | `SeatTaskTrigger` на кресле |
| — | Прочитал письмо в «Почте» → задача снята | `ComputerMail` на `MonitorScreen` |

Звонок начальника больше не двигает часы: после него по-прежнему 00:00.

---

## 2. Правила, о которых договорились

1. **Каждое событие привязано к игровым часам** (`TimeManager`), либо к отметке на часах
   плюс задержка в реальных секундах. Нельзя опираться только на реальные секунды от загрузки
   сцены или только на сюжетный флаг. Формат — `GameTimeMark { at: "ЧЧ:ММ", delaySeconds }`,
   ход события — `TimedEvent` (`Scripts/World/GameTimeMark.cs`).
2. **Отладочный перевод часов** должен отменять события раньше введённого времени и считать
   их пройденными. Поэтому для каждого нового события нужно:
   - подписаться на `TimeManager.DebugJumped(int minutes)` и отменять/глушить себя
     (`TimedEvent.SkipIfBefore`);
   - новую задачу вписывать в `Data/Tasks.asset` с полем **Time**;
   - новый сюжетный шаг вписывать в `Data/StoryBeats.asset` (время + флаги завершения).
3. **Имена.** Мини-игра «Провод» между нами называется **«Эфир»**. Код и ассеты не
   переименовывались (`WirePuzzle*`, `start_minigame wire`).
4. **Стиль кода** как в проекте: комментарии и XML-доки на русском, объясняющие «почему»;
   `TryGetComponent` вместо `GetComponent ?? AddComponent` (в редакторе фальшивый null);
   `Start` для ссылок на `GameSession` (порядок `Awake` не гарантирован); UI строится кодом.
5. Флаги мира — по соглашению ГДД: `STORY_*`, `ACTION_*`, `KNOW_*`, `NPC_*`, `SECRET_*`, `TASK_*`.

---

## 3. Что сделано за сессию

### 3.1 Параметры сюжета (влияют на концовку)
- `Scripts/World/StoryStats.cs` — описания параметров: `Crime`, `Cult`, `GirlFriend`
  (от −5 до +5, старт 0) и `CatHunger` (от 0 до 100, старт 0).
- Параметры хранятся в `WorldState` как числовые флаги и прижимаются к границам диапазона.
  Метод `WorldState.Add(name, delta)`.
- `Yarn/Stats.yarn` — объявления. В диалоге: `<<set $Crime = $Crime + 1>>`.
- `RadioVariableStorage` сообщает Yarn уже прижатое к диапазону значение.
- Пока нигде в диалогах не меняются — нужно расставить `<<set>>` по выборам.

### 3.2 HUD: «Это будет иметь последствия» + отладочная сводка
- `Scripts/UI/StoryStatsHud.cs`, префаб `Prefabs/UI/StoryStatsHud.prefab` (в сцене рядом с `TaskHud`).
- **Надпись.** При реальном изменении `Crime`/`Cult`/`GirlFriend` в левом верхнем углу
  на 3 с появляется «Это будет иметь последствия».
- **Сводка по правому Ctrl** (только в редакторе и Development Build): значения параметров
  и `GameTime: ЧЧ:ММ [ночь N, M / 360 мин]`.
- **Ввод времени.** При открытой сводке 4 цифры подряд (верхний ряд или цифровой блок)
  ставят часы смены: `0130` = 01:30. Недобранные цифры сбрасываются через 3 с.
  Вызывается `TimeManager.DebugSetTime`, которая сначала шлёт `DebugJumped`.
- **Если сводку не видно.** Однажды её обрезал Scale 1.5x в окне Game — Scale должен быть 1x.

### 3.3 Мини-игра «Эфир» (`Scripts/Minigames/Wire*`, префаб `Prefabs/UI/WirePuzzle.prefab`)
- **Музыка.** Пока открыто поле мини-игры, играют случайные треки из `Audio/Efir`; трек
  доиграл — следующий. Плейлист `Data/EfirPlaylist.asset` сам пересобирается из папки
  (`Scripts/Audio/MusicPlaylist.cs`, `PlaylistPlayer.cs`, `Scripts/Editor/MusicPlaylistSync.cs`).
- **Два уровня подряд** (`MinigameHost.wireLevels`): `Wire_Night1` (скорость 2), затем
  `Wire_Night1_Level2` (скорость 3). Поле между уровнями не закрывается, музыка не прерывается.
  В строке состояния «Уровень N/M».
- **Маячок** — иконка `Art/ExtraArt/Microphone.png`. На любую клавишу она сжимается
  до 80% и возвращается (`WirePuzzleView.pressScale`, `pressRecoverTime`).
- **Звуки нажатий.** Любая клавиша — `EfirTap1`, засчитанное нажатие — `EfirTap2`.
- **Громкость** музыки и щелчков — 0.5.
- **После победы** флаг `STORY_FIRST_BROADCAST_STARTED` и время 00:30.

### 3.4 Визит бабки Зины
- `Yarn/Night1_Granny.yarn` переписан по `010003Бабка_Зина.md`: банка, три вопроса, ответ.
  Флаги `zina_tattoo_asked`, `zina_promised`. Старые `granny_*` удалены.
- `DoorKnocker` переписан: стучит ровно два раза (при приходе и при первом вставании),
  без бесконечного повтора. Гость ждёт, пока не откроют.

### 3.5 Банка Зины
- Объект `EvenyProps/Banka` выключен в сцене и включается в 01:00 (`Scripts/World/ActivateAtTime.cs`).
- Осмотр один раз, как куртка Серёги: `DialogueInteractable`, узел `Banka_Examine`
  в `Yarn/Apartment.yarn`, доступность `$banka_can_examine`.

### 3.6 Компьютер: почта и письмо из полиции
- `Scripts/UI/ComputerProgram.cs` — базовый класс программы; у значка в `ComputerDesktop`
  появилось поле `program`.
- `Scripts/UI/ComputerMail.cs` — список писем-вкладок, клик открывает письмо, кнопка
  «← Входящие». Письмо появляется по флагу `STORY_POLICE_MAIL_ARRIVED`; прочтение ставит
  `KNOW_POLICE_MISSING_GIRL` и снимает задачу `check_computer`.
- Эмодзи 📩 в шрифте LiberationSans нет, поэтому пометка текстом «НОВОЕ СООБЩЕНИЕ».
- `SitPoint` получил событие `SatDown` и свойство `IsSeated`.
  `Scripts/World/SeatTaskTrigger.cs` срабатывает и если задача появилась, пока игрок уже сидит.

### 3.7 Привязка к часам и отладка
- `GameTimeMark` / `TimedEvent` — общий механизм «отметка + задержка» с отменой при переводе.
- Переведены на часы: `PhoneRinger` (00:00 + 2 с), `DoorKnocker` (00:30), `TaskAtTime`,
  `NightSchedule`/`ScheduleRunner`. Режима «реальные секунды от старта» больше нет;
  записей в `Night1Schedule` пока нет.
- `TaskCatalog` получил поле `time` у задач; `TaskLog.SkipBefore` снимает задачи раньше
  отметки и не даёт им вернуться. Время задач:

  | Задачи | Время |
  |---|---|
  | answer_phone, tape_a, start_broadcast | 00:00 |
  | door_knock | 00:30 |
  | return_to_desk, check_computer | 01:00 |

- `Data/StoryBeats.asset` + `StoryBeatSkipper` на `GameSession`: при переводе часов вперёд
  шаги раньше отметки получают флаги завершения. Шаги на самой отметке не трогаются.

  | Шаг | Время |
  |---|---|
  | boss_call | 00:00 |
  | first_broadcast_efir | 00:00 |
  | zina_visit | 00:30 |
  | police_mail | 01:00 |

- Появление вещей (банка) — это состояние мира: оно применяется и при перепрыгивании.

### 3.8 Прочее
- `Cover2`: материал `Art/Posters/Materials/Untitled.mat` переведён в Alpha Clipping (Cutout)
  как у `Cover` (`M_Cover`); у текстуры включён Alpha Is Transparency.

---

## 4. Инструменты (MCP)

- **Unity:** в Claude Code работает сервер `UnityMCP` (MCP for Unity, CoplayDev, `mcpforunity://`).
  Второй сервер `Unity-MCP` (IvanMurzak) в этой сессии не подключался — ошибка соединения;
  в консоли Unity периодически пишется `[AI] ConnectionManager ... A task was canceled`,
  к игре не относится.
- **Blender 5.2.2 LTS** (Steam): `F:\SteamLibrary\steamapps\common\Blender\blender.exe`.
  `F:\Progs\blender.exe` — старый 5.1.2.
  - **Официальный Blender Lab MCP v1.0.3.** Исходники в `%LOCALAPPDATA%\blender_mcp\src`;
    аддон «MCP» стоит в 5.2 (localhost:9876, нужен Edit → Preferences → System →
    Allow Online Access); сервер `C:\Users\Vector\.local\bin\blender-mcp.exe`.
    Подключён как `blender-lab` в Claude Code (user scope) и в `~/.codex/config.toml`.
  - **`dcc-blender`** (плагин dcc-mcp, `http://127.0.0.1:9765/mcp`) подключён и в Codex,
    и в Claude Code. Это шлюз из 4 мета-инструментов (`search`, `describe`, `load_skill`,
    `call`) над ~247 инструментами Blender.
  - **На новом аккаунте** Claude Code MCP-серверы из user scope (`~/.claude.json`) останутся,
    если это тот же пользователь Windows. Иначе добавить заново:
    ```
    claude mcp add blender-lab --scope user -e "BLENDER_PATH=F:\SteamLibrary\steamapps\common\Blender\blender.exe" -- C:\Users\Vector\.local\bin\blender-mcp.exe
    claude mcp add --transport http dcc-blender --scope user http://127.0.0.1:9765/mcp
    ```

---

## 5. Грабли, на которые наступали

- **Перекомпиляция во время Play Mode** ломает `FirstPersonController` (NullReferenceException
  каждый кадр). Перед правкой кода проверять `EditorApplication.isPlaying` и не компилировать
  в игре; если случилось — перезапустить Play.
- **`manage_editor play` возвращается до перезагрузки сцены.** Первый `execute_code` сразу
  после него может увидеть старое состояние (например, часы не 00:00). Сначала проверить
  `Time.frameCount` и время.
- **Новый `.yarn` файл** иногда не попадает в скомпилированный проект до
  `AssetDatabase.ImportAsset("Assets/_Project/Yarn/Radio.yarnproject", ForceUpdate)`.
- **Имитация клавиш:** `InputSystem.QueueStateEvent` с отпусканием и нажатием в одном вызове;
  каждое нажатие — отдельным вызовом, чтобы попало в свой кадр.
- **`execute_code`** блокирует `AssetDatabase.DeleteAsset` — нужен `safety_checks=false`
  (только для своих временных файлов).

---

## 6. Состояние репозитория

- Ветка `dev-nil`, последний коммит `9257598 feat: случайный провод под уровень сложности`.
- **Всё из этой сессии не закоммичено.** Кроме того, в рабочей копии много изменений, сделанных
  до сессии: модели, префабы мебели, караоке, радио, кассеты, `DialogueSystem.prefab`,
  GDD, `Packages/manifest.json` и др. Перед сменой аккаунта лучше закоммитить логическими
  частями (правила — в `CONTRIBUTING.md`: работа в `dev-nil`, в `main` — через PR).
- Сцена `Assets/_Project/Scenes/Apartment.unity` сохранена со всеми изменениями сессии.
- Консоль Unity чистая: ошибок компиляции нет, предупреждения только старые `CS0618`
  про `FindFirstObjectByType`.

---

## 7. Что дальше (по таблице ночи 1)

- **01:30** — сцена 6 почти готова (письмо). В таблице после письма ещё задача «прочитать
  письмо» — сейчас её роль играет «Проверить компьютер».
- **02:00** — второй эфир: задача выйти в эфир, мини-игра №2.
- **02:30** — главный выбор первого дня: `010004Второй_эфир_объявления.md`. Здесь впервые
  меняются параметры (Барсик: Crime +1; Мария: Crime −1, Cult −1, …). Объявление Барсика
  зависит от `zina_promised`.
- **03:00** — звонок в эфир (`010005Звонок_Саши.md`); **03:30** — выбор песни;
  **04:00** — кошка (`010008Кошка.md`, `CatHunger`, Cult ±1); **05:00** — конец смены,
  автоответчик (`010006`); **05:30** — сон (`010007`).
- Каждую новую сцену: время по таблице, `GameTimeMark` или действие, задачи с `time`
  в `Tasks.asset`, шаг в `StoryBeats.asset`.
- **Открытые вопросы:**
  - реплика Зины «Час ночи!» звучит в 00:30 — оставили по сценарию;
  - текст письма мелкий — можно увеличить окно компьютера;
  - параметры сюжета пока нигде не меняются.
