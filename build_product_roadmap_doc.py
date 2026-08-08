from pathlib import Path

from docx import Document
from docx.enum.section import WD_SECTION
from docx.enum.table import WD_CELL_VERTICAL_ALIGNMENT, WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Inches, Pt, RGBColor


OUTPUT = Path(r"C:\PROJECT\AI-ассистент для League of Legends — концепция и дорожная карта.docx")

FONT = "Calibri"
NAVY = "0B2545"
BLUE = "2E74B5"
DARK_BLUE = "1F4D78"
MUTED = "5D6B78"
INK = "1F2933"
LIGHT_BLUE = "E8EEF5"
CALLOUT = "F4F6F9"
BORDER = "CCD6E0"
WHITE = "FFFFFF"


def set_run_font(run, size=None, bold=None, italic=None, color=INK, name=FONT):
    run.font.name = name
    run._element.get_or_add_rPr().rFonts.set(qn("w:ascii"), name)
    run._element.get_or_add_rPr().rFonts.set(qn("w:hAnsi"), name)
    run._element.get_or_add_rPr().rFonts.set(qn("w:eastAsia"), name)
    if size is not None:
        run.font.size = Pt(size)
    if bold is not None:
        run.bold = bold
    if italic is not None:
        run.italic = italic
    if color:
        run.font.color.rgb = RGBColor.from_string(color)


def set_cell_shading(cell, fill):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = tc_pr.find(qn("w:shd"))
    if shd is None:
        shd = OxmlElement("w:shd")
        tc_pr.append(shd)
    shd.set(qn("w:fill"), fill)
    shd.set(qn("w:val"), "clear")


def set_cell_margins(cell, top=100, start=140, bottom=100, end=140):
    tc_pr = cell._tc.get_or_add_tcPr()
    tc_mar = tc_pr.first_child_found_in("w:tcMar")
    if tc_mar is None:
        tc_mar = OxmlElement("w:tcMar")
        tc_pr.append(tc_mar)
    for side, value in (("top", top), ("start", start), ("bottom", bottom), ("end", end)):
        tag = qn(f"w:{side}")
        node = tc_mar.find(tag)
        if node is None:
            node = OxmlElement(f"w:{side}")
            tc_mar.append(node)
        node.set(qn("w:w"), str(value))
        node.set(qn("w:type"), "dxa")


def set_cell_borders(cell, color=BORDER, size=6, left_color=None, left_size=None):
    tc_pr = cell._tc.get_or_add_tcPr()
    borders = tc_pr.first_child_found_in("w:tcBorders")
    if borders is None:
        borders = OxmlElement("w:tcBorders")
        tc_pr.append(borders)
    for edge in ("top", "start", "bottom", "end", "insideH", "insideV"):
        node = borders.find(qn(f"w:{edge}"))
        if node is None:
            node = OxmlElement(f"w:{edge}")
            borders.append(node)
        node.set(qn("w:val"), "single")
        node.set(qn("w:sz"), str(left_size if edge == "start" and left_size else size))
        node.set(qn("w:color"), left_color if edge == "start" and left_color else color)


def set_table_geometry(table, widths_dxa, indent_dxa=120):
    table.alignment = WD_TABLE_ALIGNMENT.LEFT
    table.autofit = False
    tbl_pr = table._tbl.tblPr

    tbl_w = tbl_pr.find(qn("w:tblW"))
    if tbl_w is None:
        tbl_w = OxmlElement("w:tblW")
        tbl_pr.append(tbl_w)
    tbl_w.set(qn("w:w"), str(sum(widths_dxa)))
    tbl_w.set(qn("w:type"), "dxa")

    tbl_ind = tbl_pr.find(qn("w:tblInd"))
    if tbl_ind is None:
        tbl_ind = OxmlElement("w:tblInd")
        tbl_pr.append(tbl_ind)
    tbl_ind.set(qn("w:w"), str(indent_dxa))
    tbl_ind.set(qn("w:type"), "dxa")

    layout = tbl_pr.find(qn("w:tblLayout"))
    if layout is None:
        layout = OxmlElement("w:tblLayout")
        tbl_pr.append(layout)
    layout.set(qn("w:type"), "fixed")

    grid = table._tbl.tblGrid
    for child in list(grid):
        grid.remove(child)
    for width in widths_dxa:
        col = OxmlElement("w:gridCol")
        col.set(qn("w:w"), str(width))
        grid.append(col)

    for row in table.rows:
        for idx, cell in enumerate(row.cells):
            tc_pr = cell._tc.get_or_add_tcPr()
            tc_w = tc_pr.find(qn("w:tcW"))
            if tc_w is None:
                tc_w = OxmlElement("w:tcW")
                tc_pr.append(tc_w)
            tc_w.set(qn("w:w"), str(widths_dxa[idx]))
            tc_w.set(qn("w:type"), "dxa")
            cell.width = Inches(widths_dxa[idx] / 1440)


def add_custom_numbering(doc, fmt, level_text, left=540, hanging=270):
    numbering = doc.part.numbering_part.element
    abstract_ids = [int(x.get(qn("w:abstractNumId"))) for x in numbering.findall(qn("w:abstractNum"))]
    num_ids = [int(x.get(qn("w:numId"))) for x in numbering.findall(qn("w:num"))]
    abstract_id = max(abstract_ids, default=0) + 1
    num_id = max(num_ids, default=0) + 1

    abstract = OxmlElement("w:abstractNum")
    abstract.set(qn("w:abstractNumId"), str(abstract_id))
    multi = OxmlElement("w:multiLevelType")
    multi.set(qn("w:val"), "singleLevel")
    abstract.append(multi)

    lvl = OxmlElement("w:lvl")
    lvl.set(qn("w:ilvl"), "0")
    start = OxmlElement("w:start")
    start.set(qn("w:val"), "1")
    lvl.append(start)
    num_fmt = OxmlElement("w:numFmt")
    num_fmt.set(qn("w:val"), fmt)
    lvl.append(num_fmt)
    text = OxmlElement("w:lvlText")
    text.set(qn("w:val"), level_text)
    lvl.append(text)
    suff = OxmlElement("w:suff")
    suff.set(qn("w:val"), "tab")
    lvl.append(suff)
    jc = OxmlElement("w:lvlJc")
    jc.set(qn("w:val"), "left")
    lvl.append(jc)

    p_pr = OxmlElement("w:pPr")
    tabs = OxmlElement("w:tabs")
    tab = OxmlElement("w:tab")
    tab.set(qn("w:val"), "num")
    tab.set(qn("w:pos"), str(left))
    tabs.append(tab)
    p_pr.append(tabs)
    ind = OxmlElement("w:ind")
    ind.set(qn("w:left"), str(left))
    ind.set(qn("w:hanging"), str(hanging))
    p_pr.append(ind)
    spacing = OxmlElement("w:spacing")
    spacing.set(qn("w:after"), "80")
    spacing.set(qn("w:line"), "300")
    spacing.set(qn("w:lineRule"), "auto")
    p_pr.append(spacing)
    lvl.append(p_pr)

    r_pr = OxmlElement("w:rPr")
    fonts = OxmlElement("w:rFonts")
    fonts.set(qn("w:ascii"), FONT)
    fonts.set(qn("w:hAnsi"), FONT)
    r_pr.append(fonts)
    lvl.append(r_pr)
    abstract.append(lvl)
    numbering.append(abstract)

    num = OxmlElement("w:num")
    num.set(qn("w:numId"), str(num_id))
    abstract_ref = OxmlElement("w:abstractNumId")
    abstract_ref.set(qn("w:val"), str(abstract_id))
    num.append(abstract_ref)
    numbering.append(num)
    return num_id


def apply_numbering(paragraph, num_id):
    p_pr = paragraph._p.get_or_add_pPr()
    num_pr = p_pr.find(qn("w:numPr"))
    if num_pr is None:
        num_pr = OxmlElement("w:numPr")
        p_pr.insert(0, num_pr)
    ilvl = OxmlElement("w:ilvl")
    ilvl.set(qn("w:val"), "0")
    num_pr.append(ilvl)
    nid = OxmlElement("w:numId")
    nid.set(qn("w:val"), str(num_id))
    num_pr.append(nid)


def add_bullet(doc, text, bullet_id, bold_lead=None):
    p = doc.add_paragraph(style="List Paragraph")
    apply_numbering(p, bullet_id)
    p.paragraph_format.space_before = Pt(0)
    p.paragraph_format.space_after = Pt(4)
    p.paragraph_format.line_spacing = 1.25
    if bold_lead and text.startswith(bold_lead):
        lead = p.add_run(bold_lead)
        set_run_font(lead, bold=True)
        rest = p.add_run(text[len(bold_lead):])
        set_run_font(rest)
    else:
        run = p.add_run(text)
        set_run_font(run)
    return p


def add_numbered(doc, text, number_id, bold_lead=None):
    p = doc.add_paragraph(style="List Paragraph")
    apply_numbering(p, number_id)
    p.paragraph_format.space_before = Pt(0)
    p.paragraph_format.space_after = Pt(4)
    p.paragraph_format.line_spacing = 1.25
    if bold_lead and text.startswith(bold_lead):
        lead = p.add_run(bold_lead)
        set_run_font(lead, bold=True)
        rest = p.add_run(text[len(bold_lead):])
        set_run_font(rest)
    else:
        run = p.add_run(text)
        set_run_font(run)
    return p


def add_body(doc, text, bold_lead=None):
    p = doc.add_paragraph()
    if bold_lead and text.startswith(bold_lead):
        lead = p.add_run(bold_lead)
        set_run_font(lead, bold=True)
        rest = p.add_run(text[len(bold_lead):])
        set_run_font(rest)
    else:
        run = p.add_run(text)
        set_run_font(run)
    return p


def add_callout(doc, label, text, fill=CALLOUT, accent=BLUE):
    table = doc.add_table(rows=1, cols=1)
    set_table_geometry(table, [9360], indent_dxa=180)
    cell = table.cell(0, 0)
    set_cell_shading(cell, fill)
    set_cell_margins(cell, top=150, start=180, bottom=150, end=180)
    set_cell_borders(cell, color=BORDER, size=5, left_color=accent, left_size=18)
    cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
    p = cell.paragraphs[0]
    p.paragraph_format.space_before = Pt(0)
    p.paragraph_format.space_after = Pt(0)
    p.paragraph_format.line_spacing = 1.2
    r1 = p.add_run(label + " ")
    set_run_font(r1, size=10.5, bold=True, color=DARK_BLUE)
    r2 = p.add_run(text)
    set_run_font(r2, size=10.5, color=INK)
    spacer = doc.add_paragraph()
    spacer.paragraph_format.space_after = Pt(0)
    spacer.paragraph_format.space_before = Pt(0)
    spacer.paragraph_format.line_spacing = 0.35
    spacer.add_run("")
    return table


def add_page_number(paragraph):
    run = paragraph.add_run()
    begin = OxmlElement("w:fldChar")
    begin.set(qn("w:fldCharType"), "begin")
    instr = OxmlElement("w:instrText")
    instr.set(qn("xml:space"), "preserve")
    instr.text = " PAGE "
    separate = OxmlElement("w:fldChar")
    separate.set(qn("w:fldCharType"), "separate")
    text = OxmlElement("w:t")
    text.text = "1"
    end = OxmlElement("w:fldChar")
    end.set(qn("w:fldCharType"), "end")
    run._r.extend([begin, instr, separate, text, end])
    set_run_font(run, size=8.5, color=MUTED)


def configure_styles(doc):
    styles = doc.styles
    normal = styles["Normal"]
    normal.font.name = FONT
    normal._element.rPr.rFonts.set(qn("w:ascii"), FONT)
    normal._element.rPr.rFonts.set(qn("w:hAnsi"), FONT)
    normal.font.size = Pt(11)
    normal.font.color.rgb = RGBColor.from_string(INK)
    normal.paragraph_format.space_before = Pt(0)
    normal.paragraph_format.space_after = Pt(6)
    normal.paragraph_format.line_spacing = 1.25
    normal.paragraph_format.widow_control = True

    title = styles["Title"]
    title.font.name = FONT
    title._element.rPr.rFonts.set(qn("w:ascii"), FONT)
    title._element.rPr.rFonts.set(qn("w:hAnsi"), FONT)
    title.font.size = Pt(28)
    title.font.bold = True
    title.font.color.rgb = RGBColor.from_string(NAVY)
    title.paragraph_format.space_before = Pt(0)
    title.paragraph_format.space_after = Pt(7)
    title.paragraph_format.keep_with_next = True

    subtitle = styles["Subtitle"]
    subtitle.font.name = FONT
    subtitle._element.rPr.rFonts.set(qn("w:ascii"), FONT)
    subtitle._element.rPr.rFonts.set(qn("w:hAnsi"), FONT)
    subtitle.font.size = Pt(13)
    subtitle.font.color.rgb = RGBColor.from_string(MUTED)
    subtitle.paragraph_format.space_before = Pt(0)
    subtitle.paragraph_format.space_after = Pt(18)
    subtitle.paragraph_format.keep_with_next = True

    for style_name, size, color, before, after in (
        ("Heading 1", 16, BLUE, 18, 10),
        ("Heading 2", 13, BLUE, 14, 7),
        ("Heading 3", 12, DARK_BLUE, 10, 5),
    ):
        style = styles[style_name]
        style.font.name = FONT
        style._element.rPr.rFonts.set(qn("w:ascii"), FONT)
        style._element.rPr.rFonts.set(qn("w:hAnsi"), FONT)
        style.font.size = Pt(size)
        style.font.bold = True
        style.font.color.rgb = RGBColor.from_string(color)
        style.paragraph_format.space_before = Pt(before)
        style.paragraph_format.space_after = Pt(after)
        style.paragraph_format.keep_with_next = True
        style.paragraph_format.keep_together = True

    list_style = styles["List Paragraph"]
    list_style.font.name = FONT
    list_style._element.rPr.rFonts.set(qn("w:ascii"), FONT)
    list_style._element.rPr.rFonts.set(qn("w:hAnsi"), FONT)
    list_style.font.size = Pt(11)
    list_style.paragraph_format.space_after = Pt(4)
    list_style.paragraph_format.line_spacing = 1.25
    list_style.paragraph_format.widow_control = True


def add_roadmap_table(doc):
    rows = [
        ("Основа", "Каркас Windows-приложения, настройки, оверлей, голос и журнал событий."),
        ("Наблюдатель", "Распознавание миникарты, игрового времени, уровней, предметов и ключевых появлений противников."),
        ("Shadow Mode", "Тихое наблюдение без советов: проверка точности и сбор ошибочных срабатываний."),
        ("Live Alpha", "Первые приоритетные предупреждения о леснике, исчезнувшем мидере и неизвестной позиции противников."),
        ("Matchup Coach", "Фазовые советы для Urgot против ограниченного набора соперников на TOP."),
        ("Post-game Coach", "Временная шкала матча, повторяющиеся ошибки и цель на следующую игру."),
        ("Coach Packs", "Редактор пользовательских правил, версии патчей, авторство и безопасный локальный API."),
        ("Закрытая альфа", "Тестирование на друзьях и проверка того, становятся ли решения игроков лучше."),
        ("Beta", "Расширение TOP, добавление ADC и подготовка публичного live-режима после проверки Riot."),
    ]

    table = doc.add_table(rows=1, cols=2)
    set_table_geometry(table, [2100, 7260], indent_dxa=140)
    table.rows[0]._tr.get_or_add_trPr().append(OxmlElement("w:tblHeader"))
    headers = ("Этап", "Результат")
    for idx, text in enumerate(headers):
        cell = table.rows[0].cells[idx]
        set_cell_shading(cell, LIGHT_BLUE)
        set_cell_margins(cell, top=120, start=140, bottom=120, end=140)
        set_cell_borders(cell)
        cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
        p = cell.paragraphs[0]
        p.paragraph_format.space_after = Pt(0)
        p.paragraph_format.line_spacing = 1.1
        run = p.add_run(text)
        set_run_font(run, size=10, bold=True, color=NAVY)

    for stage, result in rows:
        cells = table.add_row().cells
        for cell in cells:
            set_cell_margins(cell, top=105, start=140, bottom=105, end=140)
            set_cell_borders(cell)
            cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
        p0 = cells[0].paragraphs[0]
        p0.paragraph_format.space_after = Pt(0)
        p0.paragraph_format.line_spacing = 1.12
        r0 = p0.add_run(stage)
        set_run_font(r0, size=9.7, bold=True, color=DARK_BLUE)
        p1 = cells[1].paragraphs[0]
        p1.paragraph_format.space_after = Pt(0)
        p1.paragraph_format.line_spacing = 1.12
        r1 = p1.add_run(result)
        set_run_font(r1, size=9.7, color=INK)

    return table


def build():
    doc = Document()
    configure_styles(doc)

    section = doc.sections[0]
    section.page_width = Inches(8.5)
    section.page_height = Inches(11)
    section.top_margin = Inches(1)
    section.right_margin = Inches(1)
    section.bottom_margin = Inches(1)
    section.left_margin = Inches(1)
    section.header_distance = Inches(0.492)
    section.footer_distance = Inches(0.492)

    header = section.header
    hp = header.paragraphs[0]
    hp.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    hp.paragraph_format.space_after = Pt(0)
    hr = hp.add_run("КОНЦЕПЦИЯ ПРОДУКТА")
    set_run_font(hr, size=8, bold=True, color=MUTED)

    footer = section.footer
    fp = footer.paragraphs[0]
    fp.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    fp.paragraph_format.space_after = Pt(0)
    fr = fp.add_run("AI-ассистент для League of Legends  ·  ")
    set_run_font(fr, size=8.5, color=MUTED)
    add_page_number(fp)

    bullet_id = add_custom_numbering(doc, "bullet", "•")
    number_id = add_custom_numbering(doc, "decimal", "%1.")

    kicker = doc.add_paragraph()
    kicker.paragraph_format.space_before = Pt(5)
    kicker.paragraph_format.space_after = Pt(8)
    kr = kicker.add_run("РАБОЧАЯ КОНЦЕПЦИЯ")
    set_run_font(kr, size=9, bold=True, color=BLUE)

    title = doc.add_paragraph(style="Title")
    title.add_run("AI-ассистент для League of Legends")
    subtitle = doc.add_paragraph(style="Subtitle")
    subtitle.add_run("Концепция продукта и дорожная карта функциональности")

    add_callout(
        doc,
        "Главная задача:",
        "повысить игровую осознанность — помочь игроку раньше замечать угрозы, понимать силу своего матчапа и учиться на собственных решениях.",
    )

    doc.add_heading("Идея продукта", level=1)
    add_body(
        doc,
        "Приложение работает как игровой коуч: наблюдает за доступной игроку информацией, выделяет важные события и выдаёт короткие объяснимые советы. Оно не играет вместо пользователя, а помогает быстрее замечать то, что игрок мог пропустить, и постепенно развивать самостоятельное понимание игры.",
    )

    add_bullet(doc, "Ситуационная осведомлённость. Предупреждения о перемещениях и возможных угрозах на карте.", bullet_id, "Ситуационная осведомлённость.")
    add_bullet(doc, "Матчап-коучинг. Советы о силе чемпионов на разных уровнях, предметах и стадиях линии.", bullet_id, "Матчап-коучинг.")
    add_bullet(doc, "Обучение после игры. Разбор событий, повторяющихся ошибок и целей на следующую игру.", bullet_id, "Обучение после игры.")

    doc.add_heading("Основной цикл", level=1)
    add_numbered(doc, "Наблюдение. Ассистент считывает необходимые элементы интерфейса и миникарты.", number_id, "Наблюдение.")
    add_numbered(doc, "Оценка. Система отделяет подтверждённые события от предположений и определяет их важность.", number_id, "Оценка.")
    add_numbered(doc, "Предупреждение. Игрок получает короткое голосовое сообщение или карточку оверлея.", number_id, "Предупреждение.")
    add_numbered(doc, "Решение. Игрок самостоятельно выбирает действие с учётом полученной информации.", number_id, "Решение.")
    add_numbered(doc, "Разбор. После матча система связывает предупреждения с результатами и показывает повторяющиеся ошибки.", number_id, "Разбор.")

    doc.add_heading("Основной функционал", level=1)
    doc.add_heading("Наблюдение за картой", level=2)
    add_body(doc, "Ассистент следит за видимой информацией на миникарте и формирует историю последних появлений противников.")
    add_bullet(doc, "замечает появление вражеского лесника и сохраняет место последнего обнаружения;", bullet_id)
    add_bullet(doc, "сообщает об исчезновении мидера или другого лайнера;", bullet_id)
    add_bullet(doc, "оценивает возможное направление движения, не выдавая предположение за факт;", bullet_id)
    add_bullet(doc, "предупреждает о глубокой позиции, когда местонахождение опасных противников неизвестно.", bullet_id)

    add_callout(
        doc,
        "Пример сообщения:",
        "лесник был замечен в нижней части карты. Он может продолжить зачистку в сторону топа, поэтому сейчас опасно выходить слишком глубоко без обзора.",
        fill="EEF4FA",
    )

    doc.add_heading("Matchup Coach", level=2)
    add_body(
        doc,
        "Для каждой пары чемпионов создаётся Matchup Timeline — дорожная карта, показывающая, как меняется соотношение сил по мере получения уровней, покупки предметов и перехода между стадиями матча.",
    )
    add_body(doc, "Urgot против Sett — начало линии. Sett обычно сильнее в прямом затяжном сражении. Основная цель Urgot — сохранить здоровье и безопасно провести первые волны.", "Urgot против Sett — начало линии.")
    add_body(doc, "Urgot достиг 9-го уровня и собрал Black Cleaver. Матчап становится заметно лучше: полноценный бой уже можно рассматривать, но его результат по-прежнему зависит от здоровья, волны, позиции и преимущества соперника.", "Urgot достиг 9-го уровня и собрал Black Cleaver.")
    add_body(doc, "Каждый совет хранит условия активации, объяснение, ограничения, автора, источник и версию патча. Искусственный интеллект не придумывает механику самостоятельно — он использует проверенную базу знаний.")

    doc.add_heading("Контекст матча", level=2)
    add_body(doc, "Чтобы выбирать подходящий совет, приложение учитывает игровое время, уровень, предметы, здоровье, стадию матча, соперника на линии и доступную информацию о карте.")

    doc.add_page_break()

    doc.add_heading("Как ассистент общается с игроком", level=1)
    add_body(doc, "Главная задача интерфейса — сообщать только действительно важное и не перегружать игрока.")
    add_bullet(doc, "угроза ганга имеет более высокий приоритет, чем общий совет по матчапу;", bullet_id)
    add_bullet(doc, "одинаковые сообщения не повторяются без новой информации;", bullet_id)
    add_bullet(doc, "устаревшие предположения отменяются;", bullet_id)
    add_bullet(doc, "в активной драке второстепенные советы откладываются;", bullet_id)
    add_bullet(doc, "частоту и типы сообщений можно настроить или полностью отключить.", bullet_id)

    add_callout(
        doc,
        "Принцип:",
        "сначала факт, затем предположение, после этого короткий совет. Игрок всегда понимает, на чём основано предупреждение.",
    )

    doc.add_heading("Разбор после матча", level=1)
    add_body(doc, "После игры ассистент превращает журнал событий в короткий обучающий разбор.")
    add_bullet(doc, "показывает временную шкалу предупреждений и ключевых решений;", bullet_id)
    add_bullet(doc, "находит смерти и ошибки, произошедшие после проигнорированной информации;", bullet_id)
    add_bullet(doc, "выделяет повторяющиеся проблемы;", bullet_id)
    add_bullet(doc, "предлагает одну или две цели на следующую игру;", bullet_id)
    add_bullet(doc, "отмечает неверные срабатывания, чтобы улучшать качество системы.", bullet_id)

    add_callout(
        doc,
        "Пример вывода:",
        "мидер исчез с линии, но игрок продолжил находиться возле вражеской башни и вскоре погиб от ганга. Цель следующей игры — после сигнала об исчезновении противника сокращать глубину позиции до появления новой информации.",
        fill="EEF4FA",
    )

    doc.add_heading("Community Coach Packs", level=1)
    add_body(
        doc,
        "Опытные игроки смогут добавлять собственные matchup-гайды и игровые правила через безопасный редактор. Это позволит развивать базу знаний силами сообщества, не меняя код приложения.",
    )
    add_bullet(doc, "правила привязываются к роли, чемпиону, противнику, уровню, предметам и стадии игры;", bullet_id)
    add_bullet(doc, "указываются автор, источник и актуальная версия патча;", bullet_id)
    add_bullet(doc, "публикации могут проходить проверку, получать рейтинг и отзывы;", bullet_id)
    add_bullet(doc, "пользовательские дополнения не получают доступ к управлению персонажем и не исполняют произвольный код.", bullet_id)

    doc.add_heading("Первая полезная версия — MVP", level=1)
    add_body(doc, "MVP считается готовым, когда приложение формирует законченный цикл от наблюдения до разбора.")
    add_bullet(doc, "определяет начало матча и игровой таймер;", bullet_id)
    add_bullet(doc, "наблюдает за миникартой;", bullet_id)
    add_bullet(doc, "замечает лесника и исчезновение мидера;", bullet_id)
    add_bullet(doc, "отделяет подтверждённые события от предположений;", bullet_id)
    add_bullet(doc, "выдаёт редкие приоритетные предупреждения;", bullet_id)
    add_bullet(doc, "активирует несколько matchup-правил для Urgot на TOP;", bullet_id)
    add_bullet(doc, "сохраняет журнал и формирует простой разбор после матча;", bullet_id)
    add_bullet(doc, "работает локально и не мешает производительности игры.", bullet_id)

    doc.add_page_break()

    doc.add_heading("Дорожная карта", level=1)
    add_body(doc, "Каждый этап создаёт самостоятельный проверяемый результат. Переход дальше происходит только после того, как текущая часть работает стабильно и приносит понятную пользу.")
    add_roadmap_table(doc)

    doc.add_heading("Что появится в будущем", level=1)
    add_bullet(doc, "расширенная библиотека матчапов для TOP;", bullet_id)
    add_bullet(doc, "коучинг для ADC с учётом двух чемпионов на каждой стороне линии;", bullet_id)
    add_bullet(doc, "анализ волн, возвратов на базу и подготовки к объектам;", bullet_id)
    add_bullet(doc, "персонализация частоты и сложности советов;", bullet_id)
    add_bullet(doc, "редактор Coach Packs без необходимости работать с файлами правил;", bullet_id)
    add_bullet(doc, "профили авторов, рейтинги, модерация и библиотека проверенного контента;", bullet_id)
    add_bullet(doc, "поддержка MID, Jungle и Support после проверки базовой модели на TOP и ADC.", bullet_id)

    doc.add_heading("Что не входит в первый релиз", level=1)
    add_bullet(doc, "поддержка всех чемпионов и ролей;", bullet_id)
    add_bullet(doc, "команды вида «нажми способность сейчас»;", bullet_id)
    add_bullet(doc, "автоматическое управление клавиатурой, мышью или персонажем;", bullet_id)
    add_bullet(doc, "использование скрытой от игрока информации;", bullet_id)
    add_bullet(doc, "обязательная отправка записей матча в облако;", bullet_id)
    add_bullet(doc, "публичное включение спорных live-функций до проверки требований Riot.", bullet_id)

    doc.add_heading("Как понять, что продукт полезен", level=1)
    add_body(doc, "Главный результат — не количество распознанных событий, а изменение поведения игрока.")
    add_bullet(doc, "снижается количество смертей от предсказуемых гангов;", bullet_id)
    add_bullet(doc, "игрок быстрее реагирует на исчезновение противников;", bullet_id)
    add_bullet(doc, "лучше понимает сильные и слабые стадии матчапа;", bullet_id)
    add_bullet(doc, "реже повторяет одну и ту же ошибку;", bullet_id)
    add_bullet(doc, "считает советы полезными и хочет оставить ассистент включённым.", bullet_id)

    add_callout(
        doc,
        "Итог:",
        "приложение должно пройти полный цикл: заметить событие, объяснить его значение, оставить решение игроку и помочь извлечь урок после матча.",
        fill=LIGHT_BLUE,
        accent=DARK_BLUE,
    )

    # Prevent headings and short callouts from being stranded where possible.
    for paragraph in doc.paragraphs:
        paragraph.paragraph_format.widow_control = True

    props = doc.core_properties
    props.title = "AI-ассистент для League of Legends — концепция и дорожная карта"
    props.subject = "Концепция продукта, MVP и дорожная карта функциональности"
    props.author = ""
    props.keywords = "League of Legends, AI-ассистент, коучинг, roadmap"
    props.comments = ""

    doc.save(OUTPUT)
    print(OUTPUT)


if __name__ == "__main__":
    build()
