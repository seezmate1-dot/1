# Dark Fantasy Action RPG на Unreal Engine 5

Локальная Action RPG для ПК в тёмном фэнтези, с архитектурой, готовой к будущему MMO. Пока в репозитории только ТЗ (мастер-промпт и дизайн-документ) и скрипты, которые его собирают. Проект UE5 добавится на этапе 0 (раздел 12 ТЗ).

## Что читать

- [`docs/TZ_v3.md`](docs/TZ_v3.md): ТЗ версии 3 целиком, разделы 1–52. Удобно читать на GitHub.
- [`docs/Dark_Fantasy_Action_RPG_UE5_TZ_v3.docx`](docs/Dark_Fantasy_Action_RPG_UE5_TZ_v3.docx): то же самое в Word.
- [`REVIEW.md`](REVIEW.md): что проверено и исправлено, что ещё требует внимания и решений.

## Как устроено

| Путь | Назначение |
|---|---|
| `docs/source/TZ_v2_original.docx` | ТЗ версии 2 (разделы 1–43), исходник для сборки |
| `tools/content_v3.py` | Текст разделов 44–52 |
| `tools/build_doc.py` | Правит старые разделы и дописывает 44–52 |
| `tools/check_numbers.py` | Проверяет, что числа сходятся |

```
pip install python-docx
python tools/build_doc.py
python tools/check_numbers.py
```

Когда здесь появится проект UE5, сначала нужно включить Git LFS для бинарных ассетов (`.uasset`, `.umap`, текстуры, звук), как требует раздел 11.
