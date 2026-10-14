# Арт «Последнего очага»: откуда он и как его пересобрать

Стиль — игрушечный, как в Hustle Castle: мультяшные персонажи с лицами и снаряжением, крепость в разрезе, тёплый свет очага против холодной скалы. Всё рисуется 3D-рендером в Blender (Cycles) из бесплатных моделей **KayKit** (Kay Lousberg, лицензия CC0) и простых форм. Музыка написана программно, короткие звуки — из библиотек **Kenney** (CC0). Ничего платного, никаких ограничений на коммерческое использование.

## Что где лежит

| Файлы в `Resources/Art` | Что это | Чем сделано |
|---|---|---|
| `Rooms/<Зал>.png` (17 залов), `Rooms/Hearth.png` | залы разреза 768×480, очаг на две ячейки | `tools/art/rooms.py` — стены, пол, балки и мебель из KayKit Dungeon/Furniture/Restaurant/Halloween |
| `Rooms/Empty.png`, `Rock.png`, `Frost.png` | выкопанная пустая ячейка, скала, иней поверх промёрзшего зала | `rooms.py`, `world.py` |
| `HearthFire.png` | 8 кадров живого огня очага, рисуются поверх камина | `rooms.render_fire` |
| `Mountain.png` | снежные пики и замок Стражей над разрезом | `world.py` |
| `Map.png` | карта кампании: остров из шестиугольников, 10 мест по актам | `world.py`, KayKit Medieval Hexagon |
| `Sheets/*.png`, `Portraits/*.png` | анимации и портреты: 10 жителей, 6 героев, 4 семьи тварей Тумана | `tools/art/characters.py`, KayKit Adventurers и Skeletons (76 анимаций на общем скелете) |
| `Decor/*.png` | 18 украшений: 10 обычных и 8 премиальных | `tools/art/decor.py` |
| `Art.json` | описание листов анимаций, места огня, точки карты, размеры украшений | `tools/art/render_all.py` |

Жители собраны из частей героев KayKit: бородатые и женские головы, капюшоны, разная одежда, перекрашенная в средневековые тона, кружки, топоры, книги в руках. Твари Тумана — скелеты KayKit, тонированные по семьям: Твари — бледно-голубые, Одержимые — лиловые, Рыцари Двора — стальные, Корневые — болотные.

## Как пересобрать

1. Python-модуль Blender (`pip install bpy`, Blender 4.2+), Pillow, numpy, ffmpeg.
2. Наборы KayKit (git clone с github.com/KayKit-Game-Assets) в `/home/user/kaykit-game-assets` или в папку из переменной `KAYKIT`: `kaykit-character-pack-adventures-1.0`, `kaykit-character-pack-skeletons-1.0`, `kaykit-dungeon-remastered-1.0`, `kaykit-furniture-bits-1.0`, `kaykit-restaurant-bits-1.0`, `kaykit-halloween-bits-1.0`, `kaykit-medieval-hexagon-pack-1.0`.
3. `python hearth/tools/art/render_all.py` — всё сразу (около часа на 4 ядрах без видеокарты), или по частям: `characters`, `rooms`, `fire`, `world`, `decor`.
4. `python hearth/tools/audio/music.py` — музыка и треск огня.

Художник может заменить любую картинку своей того же размера и с тем же именем — код менять не нужно.

## Звук

| Файл | Что | Откуда |
|---|---|---|
| `Hearth.ogg` | спокойная тема крепости: арфа, мягкая подкладка, флейта, ре дорийский | синтез, `tools/audio/music.py` |
| `Battle.ogg` | тема боя и Наплыва: быстрее, с барабанами | синтез |
| `Fire.ogg` | треск огня очага, слышен на вкладке «Крепость» | синтез |
| `Click`, `Coins`, `Build`, `Done`, `Hit`, `Clang`, `Victory`, `Defeat`, `Page`, `Door`, `Blade` | короткие звуки | Kenney, CC0 |

Музыка тихая по умолчанию; кнопки «Музыка» и «Звуки» вверху экрана выключают их, выбор запоминается.
