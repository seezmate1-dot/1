# Dark Fantasy Action RPG на Unreal Engine 5

Локальная Action RPG для ПК в тёмном фэнтези, с архитектурой, готовой к будущему MMO. Сейчас идёт **этап 0** (раздел 12 ТЗ): каркас проекта UE5 готов, дальше его нужно установить и собрать по [`docs/SETUP.md`](docs/SETUP.md).

## Что читать

- [`docs/TZ_v3.md`](docs/TZ_v3.md): ТЗ версии 3 целиком, разделы 1–52. Удобно читать на GitHub.
- [`docs/Dark_Fantasy_Action_RPG_UE5_TZ_v3.docx`](docs/Dark_Fantasy_Action_RPG_UE5_TZ_v3.docx): то же самое в Word.
- [`REVIEW.md`](REVIEW.md): что проверено и исправлено, что ещё требует внимания и решений.
- [`docs/SETUP.md`](docs/SETUP.md): установка движка и первый запуск проекта.
- [`docs/ASSETS.csv`](docs/ASSETS.csv): реестр ассетов и лицензий (раздел 4).
- [`docs/PERF_LOG.md`](docs/PERF_LOG.md): журнал оптимизации и замеров (раздел 53).
- Правила оптимизации: раздел 53 в [`docs/TZ_v3.md`](docs/TZ_v3.md).

## Как устроено

| Путь | Назначение |
|---|---|
| `AshRPG/` | Проект Unreal Engine 5 (C++): `AshRPG.uproject`, модули в `Source/`, настройки в `Config/` |
| `docs/source/TZ_v2_original.docx` | ТЗ версии 2 (разделы 1–43), исходник для сборки |
| `tools/content_v3.py` | Текст разделов 44–52 |
| `tools/build_doc.py` | Правит старые разделы и дописывает 44–52 |
| `tools/check_numbers.py` | Проверяет, что числа сходятся |

```
pip install python-docx
python tools/build_doc.py
python tools/check_numbers.py
```

## Проект UE5

`AshRPG` это техническое имя проекта. Название игры (раздел 19) задаётся отдельно, и переименовывать проект для этого не нужно.

Модули из раздела 10 ТЗ (имена с префиксом `Ash`, потому что модули `Core` и `UI` уже есть в самом движке):

| Модуль | Что в нём |
|---|---|
| `AshCore` | Общие типы, Gameplay Tags, логи, сохранения, интерфейсы сервисов |
| `AshCombat` | Бой на Gameplay Ability System: атрибуты, способности, статусы, оглушение |
| `AshInventory` | Предметы, инвентарь, экипировка, торговля, крафт |
| `AshQuests` | Квесты, журнал, диалоги, охоты |
| `AshUI` | HUD и меню на Common UI |
| `AshRPG` | Главный модуль: режим игры, персонаж, контроллер |

Бинарные файлы Unreal (`.uasset`, `.umap`, текстуры, звук) идут через Git LFS (`.gitattributes`). Большие сторонние паки в Git не хранятся, см. `docs/SETUP.md`.
