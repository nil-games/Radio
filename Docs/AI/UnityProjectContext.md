# Unity project context

Проверено 2026-09-22, исходный commit `6002e42`, рабочая ветка `dev-nil`.

- Проект `F:/UnityProjects/Radio`, Unity 6000.4.1f1, URP 17.4.0. Активный pipeline в Editor: `PC_RPAsset`.
- Input System 1.19.0, `activeInputHandler: 1`; Test Framework 1.6.0.
- Собственные ассеты находятся в `Assets/_Project`: Scenes, Scripts, Prefabs, Materials,
  Art, Audio, UI, Animations, Data. До блокинга first-party C# и asmdef здесь не обнаружены.
- Список сцен в EditorBuildSettings: SampleScene (первая), Apartment (вторая), обе включены.
  Игровой стартовый поток ещё не подтверждён; список сборки не менялся.
- Дизайн и требуемые будущие системы описаны в README и Docs/GDD.md; это не доказательство
  наличия реализованных gameplay-систем. Текущая задача добавляет только статическое окружение.
- Coplay MCP проверен живым запросом к Apartment; доступны чтение сцены/Console,
  создание объектов, C# в Editor и захват изображений. IvanMurzak установлен, но в работе не используется.
- Ошибок Console перед изменением не было. Первый широкий поиск шейдеров MCP выдал
  предупреждение о нормализации пути Packages; URP Lit отдельно подтверждён через Shader.Find.
- Force Text включён. Метафайлы создаёт Unity. Новая автоматизация лежит в Scripts/Editor
  и не входит в runtime-сборку. Наборы собственных тестов не обнаружены.
- По прямому указанию владельца: только dev-nil, подтягивать main, коммитить/пушить
  логические изменения, main — через PR и ручной merge владельцем. Не применять
  разрешение прямого push владельца из CONTRIBUTING к Codex.
- Команда предупреждена пользователем перед изменением Apartment.

Источники: CONTRIBUTING.md, README.md, Docs/GDD.md, Packages/manifest.json,
ProjectSettings/ProjectVersion.txt, EditorBuildSettings.asset, EditorSettings.asset,
ProjectSettings.asset, список Assets/_Project и живое состояние Unity MCP.
Размеры и допущения блокинга: `Docs/ApartmentBlockout.md`.
