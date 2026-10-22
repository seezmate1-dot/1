# «Солнечная долина»

3-в-ряд с восстановлением фермы и хозяйством как в Township (дизайн: `docs/GDD.md`).

- `unity/SunnyValley` — проект Unity 6.3; `Assets/SunnyValley/Core` — ядро на чистом C# (3-в-ряд в `Match3`,
  мета в `Meta`), `Game` — отображение и интерфейс, `Editor` — сборка APK.
- `core-tests` — тесты ядра без Unity: `dotnet test farm/core-tests`.
- `ui-tests` — вёрстка интерфейса на 11 экранах (безопасная зона, кнопки не пересекаются, 44 pt, текст влезает)
  без Unity: `dotnet test farm/ui-tests`; схемы экранов — в `farm/ui-tests/previews`. Подробнее: `unity/README.md`.
- APK: `.github/workflows/farm-android.yml` (Releases, теги `farm-apk-N`); проверки: `farm-checks.yml`.
