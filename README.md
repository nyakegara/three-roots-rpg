# Три корня

Курсовой проект: пошаговая RPG на C# и MonoGame, без игрового движка. Игрок исследует отдельные карты-кампании, принимает решения в заданиях и сражается на гексагональном поле.

## Требования

- .NET 10 SDK
- Windows, Linux или macOS для запуска MonoGame DesktopGL

## Сборка и запуск

Из папки репозитория:

```powershell
dotnet restore ThreeRoots.sln
dotnet build ThreeRoots.sln
dotnet run --project src/Game.MonoGame/Game.MonoGame.csproj
dotnet test ThreeRoots.sln
```

## Структура

- `src/Game.Core` — правила и модели игры; проект не зависит от графической библиотеки.
- `src/Game.MonoGame` — окно, ввод и отрисовка.
- `tests/Game.Tests` — автоматические проверки правил.
- `content` — кампании, карты и изображения.
- `docs/superpowers` — согласованный дизайн и план реализации курсовой.

## Git для этого проекта

`git status` показывает, какие файлы изменились; `git add <файл>` выбирает изменения для коммита; `git commit -m "описание"` сохраняет их в локальной истории; `git push` отправляет коммиты на GitHub. Перед каждым коммитом запускай `dotnet test ThreeRoots.sln` и проверяй `git status`.

Пока проект находится на этапе каркаса. Игровые экраны и механики будут добавляться небольшими проверяемыми частями.
