# «Солнечная долина»

3-в-ряд с восстановлением фермы и хозяйством как в Township (дизайн: `docs/GDD.md`).

- `unity/SunnyValley` — проект Unity 6.3; `Assets/SunnyValley/Core` — ядро на чистом C# (3-в-ряд в `Match3`,
  мета в `Meta`), `Game` — отображение и интерфейс, `Editor` — сборка APK.
- `core-tests` — тесты ядра без Unity: `dotnet test farm/core-tests`.
