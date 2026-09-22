# Radio

**Игровой джем** · Дедлайн: 27 сентября 2026, 17:00

## Концепция

_Здесь описание игры — заполнить по GDD._

## Команда

| Участник | Ветка | Роль |
|---|---|---|
| Nil | `dev-nil` | Ведущий разработчик, владелец репозитория |
| Nikolass | `dev-nikolas` | Разработчик |
| Danya | `dev-danya` | Разработчик |

---

## Быстрый старт

### Что нужно установить

| Инструмент | Версия | Зачем |
|---|---|---|
| **Unity** | **6000.4.1f1** | Основной движок |
| **Git LFS** | последняя | Хранение бинарных файлов (арт, звук) |
| **Python** | 3.10+ | Нужен для MCP сервера |
| **uv / uvx** | последняя | Запуск MCP сервера (`pip install uv`) |

Рендер-пайплайн — **URP (Universal Render Pipeline)**, 3D.

### Клонирование и начало работы

```powershell
# 1. Клонировать репозиторий
git clone <URL репозитория>
cd Radio

# 2. Инициализировать Git LFS (один раз на машине)
git lfs install

# 3. Перейти на свою ветку
git checkout dev-nikolas

# 4. Открыть проект в Unity 6000.4.1f1:
#    Unity Hub -> Add -> Add project from disk -> папка Radio
```

> Есть ещё один обязательный шаг — подключение Unity-мерджера сцен.
> Он описан в [CONTRIBUTING.md](CONTRIBUTING.md). Без него конфликт в сцене ломает её насовсем.

---

## Правила командной работы

Полная инструкция — **[CONTRIBUTING.md](CONTRIBUTING.md)**. Прочитай её перед первым коммитом.

Коротко самое важное:

- **`main` — всегда рабочая сборка.** Напрямую в неё пушит только Nil.
- Остальные вливают свои ветки в `main` **через Pull Request**, который одобряет Nil.
- Каждое утро подтягивай `main` к себе в ветку. Свою ветку пушь часто — хоть каждый час.
- **Перед правкой любого `.unity`-файла предупреди команду в чате.** Одновременная правка сцены двумя людьми = конфликт, который нормально не разрешается.
- `Asset Serialization Mode = Force Text` — уже включено в репозитории, приедет само.
- Формат коммита: `feat: добавил систему управления` / `fix: починил прыжок`.

---

## Версии пакетов — не понижать

В `Packages/manifest.json` версии подняты относительно того, что кладёт шаблон Unity Hub. Шаблон «Universal 3D» собран под Unity 6.0 и на 6000.4.1f1 **не компилируется**:

```
InputSystemPluginControl.cs(47,25): error CS0117:
'BuildTarget' does not contain a definition for 'ReservedCFE'
```

Актуальные версии — Input System 1.19.0, URP 17.4.0, Timeline 1.8.11. Если Package Manager предложит «откатить к версии из шаблона» — не соглашайся.

---

## MCP for Unity (CoplayDev)

Пакет `com.coplaydev.unity-mcp` добавлен в `Packages/manifest.json`.

Для работы MCP-сервера нужен **Python + uv**:

```powershell
pip install uv
uvx --from mcpforunityserver==9.7.3 mcp-for-unity
```

В Unity: `Window → MCP For Unity → Server Window`.

---

## Структура проекта

```
Assets/
├── _Project/        — всё наше
│   ├── Scripts/     — C# код
│   ├── Scenes/      — сцены
│   ├── Prefabs/     — префабы
│   ├── Art/         — спрайты, текстуры, модели
│   ├── Audio/       — звуки и музыка
│   └── UI/          — UI-элементы
└── Settings/        — URP-ассеты, не трогаем без нужды
```

---

## Git LFS

В LFS автоматически уходят: `*.png *.jpg *.jpeg *.psd *.fbx *.wav *.mp3 *.ogg *.exr *.tga *.mp4` (см. `.gitattributes`).
