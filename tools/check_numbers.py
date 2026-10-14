# -*- coding: utf-8 -*-
"""Сверяет числа разделов 44–52 между собой и со старыми разделами.

Запуск:  python tools/check_numbers.py
Если всё сходится, печатает OK и выходит с кодом 0.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ns = {}
with open(os.path.join(HERE, 'content_v3.py'), encoding='utf8') as f:
    exec(f.read(), ns)
C = ns['C']
errors = []


def check(cond, msg):
    if not cond:
        errors.append(msg)


def table(*header):
    """Первая таблица, чьи заголовки начинаются с header."""
    for item in C:
        if item[0] == 't' and tuple(item[2][:len(header)]) == header:
            return item[3]
    raise SystemExit('Нет таблицы с колонками %s' % (header,))


def num(s):
    return int(s.replace(' ', ''))


# Охоты (49.2): опыт и золото
hunts = table('Охота')
xp = sum(int(r[4].split('/')[0]) for r in hunts)
gold = sum(int(r[4].split('/')[1]) for r in hunts)
check(len(hunts) == 6, 'охот должно быть 6')
check(xp == 460, 'опыт охот %d, а в тексте 460' % xp)
check(gold == 430, 'золото охот %d, а в тексте 430' % gold)

# Опыт Акта 1 (51.1)
xp_rows = table('Источник')
check(sum(num(r[2]) for r in xp_rows[:-1]) == num(xp_rows[-1][2]), 'сумма опыта «Стало» не сходится')
check(sum(num(r[1]) for r in xp_rows[:-1]) == num(xp_rows[-1][1]), 'сумма опыта «Было» не сходится')
check(num(xp_rows[-1][2]) == 7380 + xp, 'итог опыта не равен 7 380 + охоты')
check(sum(int(r[3].rstrip('%')) for r in xp_rows[:-1]) == 100, 'доли опыта не дают 100%')
for r in xp_rows[:-1]:
    share = round(100 * num(r[2]) / num(xp_rows[-1][2]))
    check(share == int(r[3].rstrip('%')), 'доля «%s» должна быть %d%%' % (r[0], share))

# Уровни по главам (41.2 + охоты), порог уровня L: 75 × L × (L − 1) (25.1)
def level(total):
    lv = 1
    while 75 * (lv + 1) * lv <= total:
        lv += 1
    return lv

chapter_xp_v2 = [60, 800, 1150, 1100, 1500, 1250, 1520]
hunt_xp = [0] + [int(r[4].split('/')[0]) for r in hunts]
expected_levels = [1, 3, 5, 7, 8, 9, 10]
total = 0
for i, (a, b) in enumerate(zip(chapter_xp_v2, hunt_xp)):
    total += a + b
    check(level(total) == expected_levels[i], 'глава %d: опыт %d даёт уровень %d' % (i, total, level(total)))
check(total == 7840, 'опыт по главам %d, а не 7 840' % total)

# Золото (51.2)
check(5200 + gold == 5630, 'доход должен быть 5 630')
check(6200 - (5200 + gold) == 570, 'нехватка должна быть 570')

# Уязвимости (45.3): у обычного врага от одной до трёх
for r in table('Враг', 'Огонь'):
    n = sum(1 for v in r[1:6] if v == 'У')
    check(1 <= n <= 3, '%s: уязвимостей %d' % (r[0], n))

# Оглушение (46): примеры из 46.5
stagger = {r[0]: int(r[1]) for r in table('Враг', 'Запас оглушения')}
check(50 * 2 >= stagger['Капитан охотников'], 'два парирования не оглушают капитана')
check(60 + 30 >= stagger['Корневая тварь'], '«Сокрушение» + тяжёлый не оглушают корневую тварь')
axe_series = 5 + 5 + 12 + 5
check(axe_series == 27, 'серия топора не 27')
for name in ('Заражённый селянин', 'Увядший волк', 'Споровик'):
    check(axe_series >= stagger[name], 'серия топора не оглушает: %s' % name)
check(stagger['Мать Чащи'] == 150, 'запас Матери Чащи должен совпадать с 23.2 (150)')
check(round(stagger['Щитоносец'] * 1.3) == 91, '«Железный лоб»: 70 × 1,3 = 91')

# Масла (47.2): материалы и количество
resin = 3 + 3 + 2 + 2 + 2 + 2
check(resin == 14, 'смолы не 14')
fire, poison, ice = resin // 2, 5 + 3, 6
check(fire + poison + ice == 21, 'масел не 21')
check(abs((fire + poison + ice) * 8 / 60 - 3) < 0.3, 'суммарное время масел далеко от трёх часов')

# Ветки улучшений (48.1)
check(abs(1.24 * 1.12 - 1.39) < 0.01, '3 урона + 2 скорости не дают около +39%')
check(1.24 * 1.12 > 1.16 * 1.18, 'лучшая сборка не «3 урона + 2 скорости»')

# Перековка (48.2): половина базовой цены из 26.1
for base, price in ((60, 30), (200, 100), (700, 350)):
    check(base // 2 == price, 'перековка: %d / 2 != %d' % (base, price))

# Вес (50.3)
weights = 4 + 4 + 4 + 4 + 9 + 6 + 5 + 5 + 6 + 14 + 28
check(80 <= weights <= 100, 'снаряжение Акта 1 весит %d, а не около 90' % weights)
check(100 + 3 * 10 == 130, 'лимит веса на старте не 130')

# Железо (48.3)
check(30 + 2 * 6 == 42, 'железа в Акте 1 не 42')

if errors:
    print('Найдены расхождения:')
    for e in errors:
        print(' -', e)
    sys.exit(1)
print('OK: числа разделов 44–52 сходятся')
