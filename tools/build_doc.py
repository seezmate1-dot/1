# -*- coding: utf-8 -*-
"""Собирает ТЗ версии 3 из исходного документа версии 2.

1. Правит старые разделы, чтобы они не спорили с разделами 44–52 (список в 44.3).
2. Дописывает разделы 44–52 из content_v3.py в том же оформлении.

Запуск:  python tools/build_doc.py [исходный.docx] [результат.docx]
"""
import copy
import os
import re
import sys

from docx import Document
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.text.paragraph import Paragraph

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC = os.path.join(ROOT, 'docs', 'source', 'TZ_v2_original.docx')
OUT = os.path.join(ROOT, 'docs', 'Dark_Fantasy_Action_RPG_UE5_TZ_v3.docx')

BULLET_ABSTRACT = '0'
DECIMAL_ABSTRACT = '1'
TABLE_FONT = '18'  # половины пункта, как в существующих таблицах


# ---------------------------------------------------------------- низкий уровень

def add_runs(p_el, text, size=None, bold_all=False):
    """Добавляет в абзац прогоны; **...** становится жирным."""
    for i, part in enumerate(re.split(r'\*\*', text)):
        if not part:
            continue
        r = OxmlElement('w:r')
        bold = bold_all or i % 2 == 1
        if bold or size:
            rpr = OxmlElement('w:rPr')
            if bold:
                rpr.append(OxmlElement('w:b'))
            if size:
                for tag in ('w:sz', 'w:szCs'):
                    e = OxmlElement(tag)
                    e.set(qn('w:val'), size)
                    rpr.append(e)
            r.append(rpr)
        t = OxmlElement('w:t')
        t.set('{http://www.w3.org/XML/1998/namespace}space', 'preserve')
        t.text = part
        r.append(t)
        p_el.append(r)


def make_p(text='', style=None, num_id=None):
    p = OxmlElement('w:p')
    ppr = OxmlElement('w:pPr')
    if style:
        ps = OxmlElement('w:pStyle')
        ps.set(qn('w:val'), style)
        ppr.append(ps)
    if num_id:
        numpr = OxmlElement('w:numPr')
        ilvl = OxmlElement('w:ilvl')
        ilvl.set(qn('w:val'), '0')
        nid = OxmlElement('w:numId')
        nid.set(qn('w:val'), str(num_id))
        numpr.append(ilvl)
        numpr.append(nid)
        ppr.append(numpr)
    p.append(ppr)
    if text:
        add_runs(p, text)
    return p


def make_cell(width, text, header=False, bold=False):
    tc = OxmlElement('w:tc')
    tcpr = OxmlElement('w:tcPr')
    w = OxmlElement('w:tcW')
    w.set(qn('w:w'), str(width))
    w.set(qn('w:type'), 'dxa')
    tcpr.append(w)
    if header:
        shd = OxmlElement('w:shd')
        shd.set(qn('w:val'), 'clear')
        shd.set(qn('w:color'), 'auto')
        shd.set(qn('w:fill'), 'F2F2F2')
        tcpr.append(shd)
    tc.append(tcpr)
    p = make_p()
    if text:
        add_runs(p, text, size=TABLE_FONT, bold_all=header or bold)
    tc.append(p)
    return tc


def make_table(widths, header, rows, bold_last=False):
    assert sum(widths) == 9360, widths
    tbl = OxmlElement('w:tbl')
    tblpr = OxmlElement('w:tblPr')
    st = OxmlElement('w:tblStyle')
    st.set(qn('w:val'), 'TableGrid')
    tblpr.append(st)
    tw = OxmlElement('w:tblW')
    tw.set(qn('w:w'), str(sum(widths)))
    tw.set(qn('w:type'), 'dxa')
    tblpr.append(tw)
    lay = OxmlElement('w:tblLayout')
    lay.set(qn('w:type'), 'fixed')
    tblpr.append(lay)
    look = OxmlElement('w:tblLook')
    for k, v in (('val', '04A0'), ('firstRow', '1'), ('lastRow', '0'), ('firstColumn', '0'),
                 ('lastColumn', '0'), ('noHBand', '0'), ('noVBand', '1')):
        look.set(qn('w:' + k), v)
    tblpr.append(look)
    tbl.append(tblpr)
    grid = OxmlElement('w:tblGrid')
    for w in widths:
        gc = OxmlElement('w:gridCol')
        gc.set(qn('w:w'), str(w))
        grid.append(gc)
    tbl.append(grid)

    def row(cells, header=False, bold=False):
        assert len(cells) == len(widths), cells
        tr = OxmlElement('w:tr')
        trpr = OxmlElement('w:trPr')
        trpr.append(OxmlElement('w:tblHeader' if header else 'w:cantSplit'))
        tr.append(trpr)
        for w, c in zip(widths, cells):
            tr.append(make_cell(w, c, header=header, bold=bold))
        return tr

    tbl.append(row(header, header=True))
    for i, r in enumerate(rows):
        tbl.append(row(r, bold=bold_last and i == len(rows) - 1))
    return tbl


class Numbering:
    """Каждому новому списку свой numId, нумерация начинается с 1."""

    def __init__(self, doc):
        self.root = doc.part.numbering_part.element
        ids = [int(n.get(qn('w:numId'))) for n in self.root.findall(qn('w:num'))]
        self.next_id = max(ids) + 1

    def new(self, abstract):
        num = OxmlElement('w:num')
        num.set(qn('w:numId'), str(self.next_id))
        a = OxmlElement('w:abstractNumId')
        a.set(qn('w:val'), abstract)
        num.append(a)
        ov = OxmlElement('w:lvlOverride')
        ov.set(qn('w:ilvl'), '0')
        so = OxmlElement('w:startOverride')
        so.set(qn('w:val'), '1')
        ov.append(so)
        num.append(ov)
        self.root.append(num)
        self.next_id += 1
        return self.next_id - 1


# ---------------------------------------------------------------- правки старых разделов

class Patcher:
    def __init__(self, doc):
        self.doc = doc
        self.done = []

    def find(self, start):
        hits = [p for p in self.doc.paragraphs if p.text.startswith(start)]
        if len(hits) != 1:
            raise SystemExit('Абзац «%s»: найдено %d, нужно ровно 1' % (start, len(hits)))
        return hits[0]

    def set_text(self, start, text):
        """Заменяет текст абзаца целиком, оформление абзаца (стиль, список) остаётся."""
        p = self.find(start)
        for r in list(p._p):
            if r.tag != qn('w:pPr'):
                p._p.remove(r)
        add_runs(p._p, text)
        self.done.append(start)
        return p

    def replace_in(self, start, old, new):
        """Замена куска текста внутри абзаца без пересборки прогонов."""
        p = self.find(start)
        for t in p._p.iter(qn('w:t')):
            if old in t.text:
                t.text = t.text.replace(old, new)
                self.done.append(start)
                return p
        raise SystemExit('В абзаце «%s» нет текста «%s»' % (start, old))

    def insert_after(self, start, text, same_list=True):
        """Новый абзац сразу после найденного, с тем же стилем и списком."""
        p = self.find(start)
        new = make_p()
        if same_list and p._p.pPr is not None:
            new.remove(new.find(qn('w:pPr')))
            new.insert(0, copy.deepcopy(p._p.pPr))
        add_runs(new, text)
        p._p.addnext(new)
        self.done.append('+' + start)
        return new

    def table_after(self, heading):
        h = self.find(heading)
        el = h._p.getnext()
        while el is not None and el.tag != qn('w:tbl'):
            el = el.getnext()
        if el is None:
            raise SystemExit('Нет таблицы после «%s»' % heading)
        return el

    @staticmethod
    def cell_text(tc):
        return ''.join(t.text for t in tc.iter(qn('w:t')))

    def set_cell(self, tbl, row_start, col, text):
        for tr in tbl.findall(qn('w:tr')):
            tcs = tr.findall(qn('w:tc'))
            if self.cell_text(tcs[0]).startswith(row_start):
                tc = tcs[col]
                ts = list(tc.iter(qn('w:t')))
                ts[0].text = text
                for t in ts[1:]:
                    t.text = ''
                self.done.append('cell ' + row_start)
                return
        raise SystemExit('Нет строки «%s»' % row_start)

    def add_row(self, tbl, cells, before_last=False):
        rows = tbl.findall(qn('w:tr'))
        tmpl = rows[1]
        new = copy.deepcopy(tmpl)
        for tc, text in zip(new.findall(qn('w:tc')), cells):
            p = tc.find(qn('w:p'))
            for r in p.findall(qn('w:r')):
                p.remove(r)
            if text:
                add_runs(p, text, size=TABLE_FONT)
        if before_last:
            rows[-1].addprevious(new)
        else:
            rows[-1].addnext(new)
        self.done.append('row ' + cells[0])


def patch_old_sections(doc):
    P = Patcher(doc)

    # 2. Мастер-промпт, правило 8
    P.replace_in('Ты senior-разработчик', 'из разделов 21–29 и 36–43 документа',
                 'из разделов 21–29 и 36–52 документа')

    # 12. Этапы и промпты
    P.insert_after('Этап 8. Полировка и сборка.',
                   '**Этапы 2б, 4б, 5б (версия 3).** Статусы и оглушение, масла и улучшения, охоты и малые '
                   'системы. Условия готовности и промпты в разделе 52.2, привязка к этапам A0–A4 в 52.1. '
                   'Локальный лог смертей (50.4) делается раньше остального, на этапе A0.')

    # 13. Чего нет в первой версии
    P.set_text('Мультиплеер, обмен и аукцион',
               'Мультиплеер, обмен и аукцион между игроками, смена дня и ночи, редактор лица, ездовые животные, '
               'жильё игрока. Простой крафт и улучшения оружия в первую версию входят (раздел 28). Сознательно не '
               'делаем и не планируем (44.2): прочность оружия и брони, голод и жажду, перегруз по весу, '
               'ежедневные награды и таймеры, бесконечный реролл свойств.')

    # 21.1 Вес
    P.set_text('Вес: лимит переноски',
               '**Вес:** лимит переноски = 100 + 3 × Сила (130 на старте). Вес есть только у оружия, щитов и '
               'брони. Перегруза и штрафов нет: если места не хватает, предмет не подбирается (раздел 50.3).')

    # 22.1 Расчёт опыта Акта 1
    P.set_text('Расчёт Акта 1: примерно',
               '**Расчёт Акта 1:** полное прохождение с побочными квестами и охотами даёт около 7 840 опыта, '
               'уровень 10 (разделы 41 и 51.1), к Матери Чащи герой приходит на уровне 9. Только основная линия: '
               'к Матери Чащи уровень 7, после неё 8.')

    # 22.5 Рецепт настойки больше не падает с босса
    P.set_text('С шансом: 30% Венец',
               'С шансом: 30% Венец Эльзы (эпический шлем, броня 12, +15% сопротивление магии). На сложности '
               '«Пепельный» шанс ×1,25. Рецепт настойки против спор с босса больше не падает: это гарантированная '
               'награда квеста «Рецепт Эльзы» (40.1).')

    # 23.2 Мать Чащи
    P.set_text('Параметры (сложность «Страж»)',
               '**Параметры (сложность «Страж»).** Здоровье 2400, броня 40, запас оглушения 150 (чем он '
               'заполняется, раздел 46). Огонь ×1,5, лёд (стужа) ×0,8. Яд и увядание не действуют, кровотечение '
               'сопротивляется, пороги статусов ×3 (раздел 45.3).')
    P.set_text('Ожидаемый бой. 4–6',
               '**Ожидаемый бой.** 4–6 минут, с огненным маслом 3–4,5 минуты (47.3). Герой уровня 8 получает '
               '8–12 попаданий при нормальной игре, 3 фляги это покрывают.')

    # 26.1, 26.3, 26.4 Цены и рецепты
    P.set_text('Материалы и расходники: волчья',
               '**Материалы и расходники:** волчья шкура 8, клык 25, ржавое железо 12, сухой корень 10, трава 6, '
               'холодное железо 60, смола 10, споровый мешочек 12, иней 15, зелье исцеления 40, зелье '
               'выносливости 40, противоядие 30, склянка 5, стрела 2. Смолу, мешочки и иней торговцы не продают, '
               'только покупают. Масла не продаются и не покупаются (47.2).')
    P.set_text('Мара (травница)',
               '**Мара (травница):** зелья, склянки, травы. Рецепты не продаёт, а учит (50.1). Покупает травы, '
               'зелья и материалы. Запас 300, +80 за отдых, максимум 500.')
    P.set_text('При VillageTrust от 70',
               'При VillageTrust от 70 Мара бесплатно учит редким рецептам: за золото рецепты не продаются нигде '
               '(50.1). При 20 и ниже Торгрим не продаёт эпические предметы, а Брик берёт на 10% больше.')

    # 27.1 Ремень
    P.set_text('8 волчьих шкур',
               '8 волчьих шкур → ремень «Стойкость», необычный, +20 к лимиту веса (Торгрим);')

    # 28.1–28.3 Крафт и улучшения
    P.set_text('Наковальня у Торгрима',
               'Наковальня у Торгрима (оружие и броня, огненное масло, перековка и разборка) и стол Мары (зелья, '
               'ядовитое и ледяное масла). Крафт без таймеров: мгновенно, с анимацией 2–3 секунды. Рецепты '
               'открываются только в мире: у NPC, из записок и книг, в наградах квестов, охот и боссов. За золото '
               'рецепты не продаются (раздел 50.1).')
    P.insert_after('Склянка покупается у Мары',
                   'Масла на оружие (огненное, ядовитое, ледяное): рецепты, материалы и где их учат, в разделе 47.2.')
    P.set_text('Оружие до +5.',
               '**Оружие** до +5. На каждом шаге игрок выбирает ветку: урон +8%, скорость ударов +6% или '
               'устойчивость к оглушению +10, в одной ветке не больше трёх шагов (раздел 48.1). Цена: +1 это 100 '
               'монет и 1 ржавое железо, +2: 200 и 2, +3: 300 и 3, +4: 400, 4 железа и 1 холодное железо, +5: '
               '500, 5 железа и Сердце Чащи.')
    P.set_text('Источники железа',
               '**Источники железа:** около 4 штук с охотников, до 12 с элит, около 12 из залежей в шахте (8 узлов '
               'по 1–2 штуки), до 12 из разборки (не больше 2 за главу, 48.3). Хватает на одно оружие до +5 и '
               'одну-две части брони, на всё сразу не хватит (раздел 40).')

    # 29 Подсказки
    P.set_text('Подсказки. Короткие',
               '**Подсказки.** Короткие подсказки управления в прологе (парирование, красные телеграфы). '
               'Отключаются в настройках. Под каждым активным квестом в журнале строка с ближайшим шагом, она '
               'отключается отдельно (раздел 50.2).')

    # 29.1 Достижения
    P.set_text('Бой (8):', '**Бой (10):**')
    P.set_text('«Охотник на волков»', '«Охотник»: выполнить все 6 именных охот (раздел 49).')
    P.insert_after('«Пепельный»: пройти Акт 1', '«Сокрушитель»: оглушить шкалой элиту или мини-босса (раздел 46).')
    P.insert_after('«Пепельный»: пройти Акт 1', '«Горячий приём»: победить Мать Чащи с огненным маслом на оружии.')
    P.set_text('Исследование (5):', '**Исследование (6):**')
    P.insert_after('«Путник»: 5 раз', '«Тайна Дуба»: найти поляну Хранителя Старого Дуба (42.2).')
    P.set_text('Экономика и развитие (6):', '**Экономика и развитие (7):**')
    P.insert_after('«Выбор пути»', '«Свой клинок»: довести оружие до +5, выбрав все три ветки (48.1).')

    # 34 Сводка
    P.set_text('Версия 2: «первая версия»',
               'Версия 2: «первая версия» теперь означает полный Акт 1 на 6–8 часов (разделы 36–43). Срез на 60–90 '
               'минут остаётся промежуточным этапом A0. При расхождениях разделы 36–43 главнее разделов 14, 22.3, '
               '22.4 и 28.3. Версия 3 (разделы 44–52): статусы, оглушение, масла, ветки улучшений, охоты и малые '
               'системы. По своим темам разделы 44–52 главнее всех предыдущих.')
    P.set_text('Системы: бой и сложности',
               '**Системы:** бой и сложности (21), лут и фарм (22), боссы (23), диалоги (24), уровни и перки (25), '
               'рынок и торговцы (26), бартер и обмен (27), крафт и улучшения (28), смерть, перемещение и '
               'достижения (29), очаги и воспоминания (15.3), статусы и оглушение (45–46), масла (47), ветки, '
               'перековка и разборка (48), именные охоты (49), рецепты, подсказки, сундук и лог смертей (50).')
    P.set_text('Что может измениться после',
               '**Что может измениться после плейтеста:** все числа в разделах 21–29 и 36–52, шансы дропа, цены и '
               'стоимость улучшений.')

    # 35.5 Журнал авторства
    log = P.table_after('35.5 Журнал авторства')
    P.add_row(log, ['Механики версии 3: статусы, оглушение, масла, улучшения', '44–48, 50', 'ИИ-черновик', '', ''])
    P.add_row(log, ['Именные охоты: имена, слухи, истории целей', '49', 'ИИ-черновик', '', ''])

    # 37.1 Уровень в конце главы 3
    t371 = P.table_after('37.1 Сколько это занимает')
    P.set_cell(t371, 'Глава 3', 3, '7')

    # 39.3 Модификаторы
    P.set_text('Чтобы знакомые враги',
               'Чтобы знакомые враги не наскучили, у некоторых есть метка. Ставит её дизайнер вручную, случайных '
               'меток нет, в одной группе не больше одной. Исключение: именная цель охоты может нести две метки '
               '(49.1).')
    P.set_text('Закалённый. Здоровья',
               '**Закалённый.** Здоровья на 30% больше и запас оглушения на 30% больше (46.1), видны корни на '
               'броне.')

    # 40.1 Правила против фарма
    P.set_text('Материалы конечны.',
               '**Материалы конечны.** Железо, сухие корни, смола, иней и Сердце Чащи лежат в мире и не '
               'возрождаются. Железа в Акте 1 около 30 единиц и до 12 из разборки с лимитом 2 за главу (48.3): на '
               'оружие до +5 уходит около 20 (с холодным железом), на одну броню до +3 около 6. Всё сразу не '
               'улучшить, игрок выбирает, что усиливать. Трава для зелий и масел растёт на мельнице и обновляется '
               'раз за главу, а не за отдых.')
    P.set_text('Награда за новое.',
               '**Награда за новое.** Первое убийство каждого вида даёт утроенный опыт. Каждому мини-боссу +30 '
               'опыта за первую победу. В бестиарии появляется запись со значками уязвимостей (45.4), а после того '
               'как игрок парировал все атаки вида, ещё и знание врага: +10% урона по этому виду. Знание врага '
               'работает вместе с уязвимостями, множители перемножаются.')

    # 41.1 и 41.2 Опыт
    P.insert_after('Из второй строки',
                   'Версия 3: именные охоты добавляют 460 опыта, итог Акта 1 7 840 (таблица в разделе 51.1).',
                   same_list=False)
    t412 = P.table_after('41.2 Уровень по главам')
    for row, chapter, total, level in (('Глава 1', '830', '890', '3'), ('Глава 2', '1 210', '2 100', '5'),
                                       ('Глава 3', '1 160', '3 260', '7'), ('Глава 4', '1 580', '4 840', '8'),
                                       ('Глава 5', '1 350', '6 190', '9'), ('Глава 6', '1 650', '7 840', '10')):
        P.set_cell(t412, row, 1, chapter)
        P.set_cell(t412, row, 2, total)
        P.set_cell(t412, row, 3, level)
    P.set_text('Таблица для игрока, который',
               'Таблица для игрока, который делает всё, включая именные охоты (раздел 49, по одной на главу). '
               'Основная линия без побочных даёт к Матери Чащи около 3 500 опыта (уровень 7) и около 4 300 после '
               'неё (уровень 8), это совпадает с разделом 22.1. Максимальный уровень первой версии остаётся 15, '
               'Акт 1 заканчивается на 10, поэтому в разделе 30.2 диапазон Акта 1 теперь 1–10.')
    return P.done


# ---------------------------------------------------------------- новые разделы

def load_content():
    ns = {}
    with open(os.path.join(HERE, 'content_v3.py'), encoding='utf8') as f:
        exec(f.read(), ns)
    return ns['C']


def append_content(doc, content):
    body = doc.element.body
    sect = body.find(qn('w:sectPr'))
    nums = Numbering(doc)

    def put(el):
        sect.addprevious(el)

    for item in content:
        kind = item[0]
        if kind == 'h2':
            put(make_p(item[1], 'Heading2'))
        elif kind == 'h3':
            put(make_p(item[1], 'Heading3'))
        elif kind == 'p':
            put(make_p(item[1]))
        elif kind in ('b', 'n'):
            num_id = nums.new(BULLET_ABSTRACT if kind == 'b' else DECIMAL_ABSTRACT)
            for text in item[1]:
                put(make_p(text, 'ListParagraph', num_id))
        elif kind == 't':
            _, widths, header, rows, bold_last = item
            put(make_table(widths, header, rows, bold_last))
            put(make_p())
        else:
            raise SystemExit('Неизвестный элемент %r' % (kind,))


def main():
    src = sys.argv[1] if len(sys.argv) > 1 else SRC
    out = sys.argv[2] if len(sys.argv) > 2 else OUT
    doc = Document(src)
    if any(p.text.startswith('44. ') for p in doc.paragraphs):
        raise SystemExit('В исходнике уже есть раздел 44, нужен документ версии 2')
    done = patch_old_sections(doc)
    append_content(doc, load_content())
    doc.core_properties.title = 'Dark Fantasy Action RPG на UE5: мастер-промпт и ТЗ, версия 3'
    doc.save(out)
    print('Правок старых разделов: %d' % len(done))
    print('Готово: %s' % out)


if __name__ == '__main__':
    main()
