# «Солнечная долина»

3-в-ряд с восстановлением фермы и хозяйством как в Township (дизайн: `docs/GDD.md`).

- `unity/SunnyValley` — проект Unity 6.3; `Assets/SunnyValley/Core` — ядро на чистом C# (3-в-ряд в `Match3`,
  мета в `Meta`), `Game` — отображение и интерфейс, `Editor` — сборка APK.
- Мета (`Core/Meta`, вход — `MetaGame`): валюты, жизни, награды за уровни 3-в-ряд, глава 1 «Восстановление»
  (`Resources/Meta/chapter1.json` — зоны, задания, сюжет), хозяйство, заказы, городок, ежедневные задания,
  сохранения с миграциями. Экономика — модель `tools/economy.py`: генерирует `Resources/Meta/balance.json` и
  `docs/ECONOMY.md` (темп, потоки монет); `python3 farm/tools/economy.py --check` — проверка согласованности (CI
  `farm-meta.yml`), бот-игрок `MetaBotTests` сверяет настоящие правила с моделью на 7 и 30 днях.
- `core-tests` — тесты ядра без Unity: `dotnet test farm/core-tests`.
- `ui-tests` — вёрстка интерфейса на 11 экранах (безопасная зона, кнопки не пересекаются, 44 pt, текст влезает)
  без Unity: `dotnet test farm/ui-tests`; схемы экранов — в `farm/ui-tests/previews`. Подробнее: `unity/README.md`.
- APK: `.github/workflows/farm-android.yml` (Releases, теги `farm-apk-N`); проверки: `farm-checks.yml`.
