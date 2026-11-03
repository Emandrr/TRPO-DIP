# -*- coding: utf-8 -*-
"""
IDEF1X СА-ЭКГ — строго по методологии IDEF1X (FIPS PUB 184)
с оформлением по ЕСКД (ГОСТ 2.104, 2.301) и ЕСПД (ГОСТ 19.101 / 19.105).

Правила:
  - независимая сущность — прямоугольник;
  - зависимая — со скруглёнными углами (только если FK входит в PK);
  - идентифицирующая связь — сплошная, неидентифицирующая — пунктир;
  - ● у дочерней (многие); ромб у родителя — FK допускает NULL;
  - PK над чертой, неключевые под чертой; (FK), (AK), (O);
  - имена сущностей и атрибутов — на русском (логический уровень).
"""
from __future__ import annotations

from pathlib import Path
import xml.etree.ElementTree as ET

OUT = Path(__file__).with_name("SA-EKG_IDEF1X_GOST.drawio")
# ГОСТ 2.304 (на ПК: GOST Common; type A отсутствует)
FONT = "GOST Common"

# --- styles ---
# одна ячейка + HTML-таблица: всегда 2 черты (имя|PK|неключевые), overflow режет выступ
IND = (
    f"rounded=0;whiteSpace=wrap;html=1;overflow=hidden;align=left;verticalAlign=top;"
    f"fillColor=#FFFFFF;strokeColor=#000000;fontFamily={FONT};fontSize=10;"
    f"fontColor=#000000;spacing=0;spacingLeft=0;spacingRight=0;spacingTop=0;spacingBottom=0;"
)
DEP = (
    f"rounded=1;arcSize=8;whiteSpace=wrap;html=1;overflow=hidden;align=left;verticalAlign=top;"
    f"fillColor=#FFFFFF;strokeColor=#000000;fontFamily={FONT};fontSize=10;"
    f"fontColor=#000000;spacing=0;spacingLeft=0;spacingRight=0;spacingTop=0;spacingBottom=0;"
)
# endSize=5 — компактная точка «многие», не перекрывает рамку
REL_ID = (
    f"edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=0;html=1;"
    f"strokeColor=#000000;strokeWidth=1;startArrow=none;endArrow=oval;endFill=1;endSize=5;"
    f"fontFamily={FONT};fontSize=10;fontColor=#000000;"
    f"exitPerimeter=1;entryPerimeter=1;"
)
REL_NI = (
    f"edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=0;html=1;dashed=1;"
    f"strokeColor=#000000;strokeWidth=1;startArrow=none;endArrow=oval;endFill=1;endSize=5;"
    f"fontFamily={FONT};fontSize=10;fontColor=#000000;"
    f"exitPerimeter=1;entryPerimeter=1;"
)
REL_NI_NULL = (
    f"edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=0;html=1;dashed=1;"
    f"strokeColor=#000000;strokeWidth=1;startArrow=diamondThin;startFill=0;startSize=6;"
    f"endArrow=oval;endFill=1;endSize=5;fontFamily={FONT};fontSize=10;fontColor=#000000;"
    f"exitPerimeter=1;entryPerimeter=1;"
)
REL_CAT = (
    f"edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;"
    f"strokeColor=#000000;strokeWidth=1;startArrow=none;endArrow=none;"
    f"fontFamily={FONT};fontSize=10;fontColor=#000000;"
)
DISC = (
    f"ellipse;whiteSpace=wrap;html=1;aspect=fixed;fillColor=#FFFFFF;"
    f"strokeColor=#000000;fontFamily={FONT};fontSize=9;fontColor=#000000;"
)
BAR = "line;strokeWidth=2;html=1;fillColor=none;strokeColor=#000000;"
LBL = (
    f"text;html=1;strokeColor=none;fillColor=none;align=center;verticalAlign=middle;"
    f"fontFamily={FONT};fontSize=10;fontColor=#000000;"
)
TITLE = (
    f"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;"
    f"fontFamily={FONT};fontSize=13;fontStyle=1;fontColor=#000000;"
)
SMALL = (
    f"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;"
    f"fontFamily={FONT};fontSize=10;fontColor=#000000;"
)
FRAME = (
    f"rounded=0;whiteSpace=wrap;html=1;fillColor=none;strokeColor=#000000;"
    f"strokeWidth=2;fontFamily={FONT};"
)
CELL = (
    f"rounded=0;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=#000000;"
    f"fontFamily={FONT};fontSize=10;fontColor=#000000;align=center;verticalAlign=middle;"
)
CELL_L = (
    f"rounded=0;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=#000000;"
    f"fontFamily={FONT};fontSize=10;fontColor=#000000;align=left;verticalAlign=middle;spacingLeft=4;"
)


def nl(s: str) -> str:
    return s.replace("\n", "&#xa;")


def _text_px(s: str) -> int:
    """Ширина строки в px (GOST Common, 10 pt)."""
    return sum(7 if ord(c) > 127 else 6 for c in s)


# высота строки и отступы (IDEF1X: две горизонтальные черты — под именем и под PK)
_ROW = 18
_PAD_Y = 3
_PAD_X = 5


def _clean_attrs(attrs: list[str] | None) -> list[str]:
    """Убрать пустые заглушки «—» / '-' из неключевой области."""
    if not attrs:
        return []
    skip = {"—", "-", "–", "−", ""}
    return [a for a in attrs if a.strip() not in skip]


def ent_size(name: str, pk: list[str], attrs: list[str]) -> tuple[int, int]:
    name_lines = name.split("\n")
    pk_lines = list(pk) if pk else ["?"]
    at_lines = _clean_attrs(attrs)
    lines = name_lines + pk_lines + at_lines
    w = max(_text_px(s) for s in lines) + 2 * _PAD_X + 8
    w = max(100, min(w, 240))
    h = (_ROW * len(name_lines) + 2 * _PAD_Y) + (_ROW * len(pk_lines) + 2 * _PAD_Y) + 2
    if at_lines:
        h += _ROW * len(at_lines) + 2 * _PAD_Y + 2  # + черта под PK
    return w, h


def ent_html(name: str, pk: list[str], attrs: list[str]) -> str:
    """IDEF1X: имя | PK; секция неключевых — только если атрибуты есть."""
    pk_lines = list(pk) if pk else ["?"]
    at_lines = _clean_attrs(attrs)
    name_html = "<br/>".join(name.split("\n"))
    pk_html = "<br/>".join(pk_lines)
    td = (
        f"padding:{_PAD_Y}px {_PAD_X}px;line-height:{_ROW}px;"
        "vertical-align:middle;font-size:10px;"
    )
    # если нет неключевых — второй черты и пустого блока нет
    pk_border = "border-bottom:1px solid #000;" if at_lines else ""
    rows = (
        f"<tr><td style='text-align:center;font-weight:bold;border-bottom:1px solid #000;{td}'>"
        f"{name_html}</td></tr>"
        f"<tr><td style='text-align:left;{pk_border}{td}'>{pk_html}</td></tr>"
    )
    if at_lines:
        at_html = "<br/>".join(at_lines)
        rows += f"<tr><td style='text-align:left;{td}'>{at_html}</td></tr>"
    return (
        "<div style='padding:0 2px;box-sizing:border-box;'>"
        "<table style='width:100%;border-collapse:collapse;margin:0;table-layout:fixed;'>"
        f"{rows}</table></div>"
    )


class D:
    def __init__(self, name: str, w: int, h: int):
        self.name, self.w, self.h = name, w, h
        self.root = ET.Element("root")
        ET.SubElement(self.root, "mxCell", id="0")
        ET.SubElement(self.root, "mxCell", id="1", parent="0")
        self.n = 0

    def id(self) -> str:
        self.n += 1
        return f"x{self.n}"

    def box(self, value: str, style: str, x, y, w, h, parent="1") -> str:
        i = self.id()
        c = ET.SubElement(
            self.root, "mxCell", id=i, value=nl(value), style=style, vertex="1", parent=parent
        )
        ET.SubElement(
            c, "mxGeometry", x=str(x), y=str(y), width=str(w), height=str(h), **{"as": "geometry"}
        )
        return i

    def label(self, v, x, y, w=110, h=16):
        return self.box(v, LBL, x, y, w, h)

    def entity(self, name, pk, attrs, x, y, w=None, h=None, dependent=False):
        """Сущность IDEF1X (FIPS 184): всегда имя / PK / неключевые."""
        aw, ah = ent_size(name, pk, attrs)
        return self.box(
            ent_html(name, pk, attrs),
            DEP if dependent else IND,
            x, y, aw, ah,
        )

    def rel(
        self,
        src,
        tgt,
        *,
        identifying=True,
        nullable=False,
        points=None,
        ex=1,
        ey=0.5,
        ix=0,
        iy=0.5,
    ):
        i = self.id()
        if identifying:
            base = REL_ID
        elif nullable:
            base = REL_NI_NULL
        else:
            base = REL_NI
        style = (
            f"{base}exitX={ex};exitY={ey};exitDx=0;exitDy=0;"
            f"entryX={ix};entryY={iy};entryDx=0;entryDy=0;"
        )
        c = ET.SubElement(
            self.root, "mxCell", id=i, style=style, edge="1", parent="1", source=src, target=tgt
        )
        geo = ET.SubElement(c, "mxGeometry", relative="1", **{"as": "geometry"})
        if points:
            arr = ET.SubElement(geo, "Array", **{"as": "points"})
            for px, py in points:
                ET.SubElement(arr, "mxPoint", x=str(px), y=str(py))
        return i

    def cat(self, src, tgt, points=None, ex=0.5, ey=1, ix=0.5, iy=0):
        i = self.id()
        style = (
            f"{REL_CAT}exitX={ex};exitY={ey};exitDx=0;exitDy=0;"
            f"entryX={ix};entryY={iy};entryDx=0;entryDy=0;"
        )
        c = ET.SubElement(
            self.root, "mxCell", id=i, style=style, edge="1", parent="1", source=src, target=tgt
        )
        geo = ET.SubElement(c, "mxGeometry", relative="1", **{"as": "geometry"})
        if points:
            arr = ET.SubElement(geo, "Array", **{"as": "points"})
            for px, py in points:
                ET.SubElement(arr, "mxPoint", x=str(px), y=str(py))
        return i

    def frame_eskd(self, designation: str, title: str, sheet: int, sheets: int):
        """Рамка + основная надпись (упрощённая форма 1 ГОСТ 2.104) для формата листа."""
        m = 40  # поле рамки
        self.box("", FRAME, m, m, self.w - 2 * m, self.h - 2 * m)
        # основная надпись справа снизу
        bx, by, bw, bh = self.w - m - 720, self.h - m - 160, 720, 160
        self.box("", FRAME, bx, by, bw, bh)
        # сетка штампа
        rows = [
            (0, 0, 120, 40, "Разраб."),
            (0, 40, 120, 40, "Пров."),
            (0, 80, 120, 40, "Н. контр."),
            (0, 120, 120, 40, "Утв."),
            (120, 0, 200, 40, ""),
            (120, 40, 200, 40, ""),
            (120, 80, 200, 40, ""),
            (120, 120, 200, 40, ""),
            (320, 0, 280, 80, designation),
            (320, 80, 280, 80, title),
            (600, 0, 60, 40, "Лит."),
            (660, 0, 60, 40, ""),
            (600, 40, 60, 40, "Масса"),
            (660, 40, 60, 40, ""),
            (600, 80, 60, 40, "Масштаб"),
            (660, 80, 60, 40, "—"),
            (600, 120, 60, 40, f"Лист {sheet}"),
            (660, 120, 60, 40, f"Листов {sheets}"),
        ]
        for x, y, w, h, val in rows:
            st = CELL if len(val) < 20 else CELL_L
            self.box(val, st, bx + x, by + y, w, h)

    def element(self):
        diagram = ET.Element("diagram", id=self.name.replace(" ", "-"), name=self.name)
        model = ET.SubElement(
            diagram,
            "mxGraphModel",
            dx="1600",
            dy="1000",
            grid="1",
            gridSize="10",
            guides="1",
            tooltips="1",
            connect="1",
            arrows="1",
            fold="1",
            page="1",
            pageScale="1",
            pageWidth=str(self.w),
            pageHeight=str(self.h),
            background="#FFFFFF",
            math="0",
            shadow="0",
        )
        model.append(self.root)
        return diagram


def build_kb() -> ET.Element:
    """Лист 1 — ключевая модель IDEF1X (KB), раскладка без пересечений связей."""
    d = D("1 IDEF1X ключевая (KB)", 1600, 1500)
    d.frame_eskd(
        "САЭКГ.ИМ01.01",
        "СА-ЭКГ. Логическая модель данных IDEF1X.\nКлючевая модель (KB)",
        1,
        2,
    )
    d.box(
        "Рисунок 1 — Логическая модель данных IDEF1X СА-ЭКГ (ключевая модель)",
        TITLE, 60, 55, 900, 28,
    )
    d.box(
        "Метод: IDEF1X (FIPS PUB 184). Оформление: ЕСКД ГОСТ 2.104, ЕСПД ГОСТ 19.105. "
        "Связи без пересечений; копии сущностей — для обратных потоков.",
        SMALL, 60, 85, 1000, 28,
    )

    # Колонки: A=80 (пациентская цепочка), B=320 (врач), C=560 (устройство/аномалии)
    # Коридоры: слева x=50 (пациент→ЭКГ/диагноз), справа x=720 (врач→диагноз)
    user = d.entity("ПОЛЬЗОВАТЕЛЬ", ["КодПользователя"], ["АдресЭлПочты (AK1)", "Роль"], 80, 130)
    tok = d.entity("ТОКЕН ОБНОВЛЕНИЯ", ["КодТокена"], ["КодПользователя (FK)"], 320, 130)
    d.rel(user, tok, identifying=False, ex=1, ey=0.5, ix=0, iy=0.5)
    d.label("имеет", 250, 140, 60, 14)

    disc = d.box("Роль", DISC, 130, 250, 40, 40)
    d.box("", BAR, 122, 290, 56, 8)
    d.cat(user, disc)
    d.label("неполная\nкатегория", 180, 250, 80, 28)

    pat = d.entity("ПАЦИЕНТ", ["КодПользователя (FK)"], [], 80, 330, dependent=True)
    doc = d.entity("ВРАЧ", ["КодПользователя (FK)"], [], 320, 330, dependent=True)
    d.cat(disc, pat, ex=0.25, ey=1, ix=0.5, iy=0)
    d.cat(disc, doc, ex=0.75, ey=1, ix=0.5, iy=0)

    card = d.entity("МЕДКАРТА", ["КодКарты"], ["КодПациента (FK) (AK1)"], 80, 430)
    d.rel(pat, card, identifying=False, ex=0.5, ey=1, ix=0.5, iy=0)
    d.label("имеет", 130, 405, 50, 14)

    link = d.entity(
        "НАЗНАЧЕНИЕ\nВРАЧА",
        ["КодВрача (FK)", "КодПациента (FK)"],
        ["ДатаНазначения"],
        320, 430,
        dependent=True,
    )
    d.rel(doc, link, identifying=True, ex=0.5, ey=1, ix=0.5, iy=0)
    # в зазоре между колонками A и B, без пересечения вертикали врач→назначение
    d.rel(
        pat, link, identifying=True,
        points=[(250, 370), (250, 460)],
        ex=1, ey=0.55, ix=0, iy=0.35,
    )
    d.label("назначается", 255, 400, 90, 14)

    dev = d.entity("УСТРОЙСТВО", ["КодУстройства"], ["ЗаводскойНомер (AK1)"], 560, 330)
    ecg = d.entity(
        "ЗАПИСЬ ЭКГ",
        ["КодЗаписи"],
        ["КодПациента (FK)", "КодУстройства (FK)"],
        80, 560,
    )
    # пациент → ЭКГ: левый коридор (мимо медкарт)
    d.rel(
        pat, ecg, identifying=False,
        points=[(50, 380), (50, 590)],
        ex=0, ey=0.7, ix=0, iy=0.4,
    )
    d.label("сдаёт", 10, 480, 50, 14)
    # устройство → ЭКГ: вниз, затем влево под назначением
    d.rel(
        dev, ecg, identifying=False,
        points=[(620, 420), (620, 590), (220, 590)],
        ex=0.5, ey=1, ix=1, iy=0.45,
    )
    d.label("фиксирует", 640, 500, 80, 14)

    rep = d.entity("ОТЧЁТ", ["КодОтчёта"], ["КодЗаписи (FK) (AK1)"], 80, 680)
    d.rel(ecg, rep, identifying=False, ex=0.5, ey=1, ix=0.5, iy=0)
    d.label("порождает", 130, 655, 70, 14)

    anom = d.entity("АНОМАЛИЯ", ["КодАномалии"], ["КодОтчёта (FK)"], 400, 680)
    d.rel(rep, anom, identifying=False, ex=1, ey=0.5, ix=0, iy=0.5)
    d.label("содержит", 280, 665, 70, 14)

    diag = d.entity(
        "ДИАГНОЗ",
        ["КодДиагноза"],
        ["КодПациента (FK)", "КодВрача (FK)", "КодОтчёта (FK) (O)"],
        80, 820,
    )
    d.rel(rep, diag, identifying=False, nullable=True, ex=0.5, ey=1, ix=0.5, iy=0)
    d.label("основан", 130, 795, 60, 14)

    # Копия ВРАЧ справа от диагноза (не на вертикали аномалий)
    doc_c = d.entity("ВРАЧ", ["КодПользователя (FK)"], [], 260, 820, dependent=True)
    d.rel(doc_c, diag, identifying=False, ex=0, ey=0.5, ix=1, iy=0.45)
    d.label("ставит", 210, 805, 50, 14)

    da = d.entity(
        "АНОМАЛИЯ\nВ ДИАГНОЗЕ",
        ["КодДиагноза (FK)", "КодАномалии (FK)"],
        [],
        400, 900,
        dependent=True,
    )
    d.rel(anom, da, identifying=True, ex=0.5, ey=1, ix=0.5, iy=0)
    # горизонталь под копией врача
    d.rel(
        diag, da, identifying=True,
        points=[(220, 930), (400, 930)],
        ex=1, ey=0.9, ix=0, iy=0.35,
    )
    d.label("включает", 280, 910, 70, 14)

    pat_c = d.entity("ПАЦИЕНТ", ["КодПользователя (FK)"], [], 80, 1000, dependent=True)
    d.rel(pat_c, diag, identifying=False, ex=0.5, ey=0, ix=0.5, iy=1)
    d.label("получает", 130, 975, 70, 14)

    rec = d.entity("РЕКОМЕНДАЦИЯ", ["КодРекомендации"], ["КодДиагноза (FK)"], 80, 1140)
    prs = d.entity("НАЗНАЧЕНИЕ ЛС", ["КодНазначения"], ["КодДиагноза (FK)"], 400, 1140)
    # левый коридор — только к рекомендации (мимо копии пациента)
    d.rel(
        diag, rec, identifying=False,
        points=[(40, 900), (40, 1165)],
        ex=0, ey=0.55, ix=0, iy=0.45,
    )
    d.label("содержит", 5, 1050, 50, 14)
    # правый обход НИЖЕ «АНОМАЛИЯ В ДИАГНОЗЕ» (блок ~y=900…1000) — не сквозь него
    d.rel(
        diag, prs, identifying=False,
        points=[(200, 1030), (560, 1030), (560, 1165)],
        ex=1, ey=0.65, ix=1, iy=0.45,
    )
    d.label("содержит", 570, 1060, 60, 14)

    d.box(
        "Условные обозначения: □ независимая сущность; ⌒ зависимая; ——— идентифицирующая связь; "
        "- - - неидентифицирующая; ● многие; ◇ — допустим NULL у родителя; "
        "(FK)/(AK)/(O) — внешний / альтернативный ключ / необязательный. "
        "Повтор сущности — копия для связи без пересечения линий.",
        SMALL, 60, 1220, 1100, 40,
    )
    return d.element()


def build_fa() -> ET.Element:
    """Лист 2 — FA: та же столбцовая раскладка без пересечений + полные атрибуты."""
    d = D("2 IDEF1X полная атрибутивная (FA)", 2000, 2300)
    d.frame_eskd(
        "САЭКГ.ИМ01.01",
        "СА-ЭКГ. Логическая модель данных IDEF1X.\nПолная атрибутивная модель (FA)",
        2,
        2,
    )
    d.box(
        "Рисунок 2 — Логическая модель данных IDEF1X СА-ЭКГ (полная атрибутивная модель)",
        TITLE, 60, 55, 1100, 28,
    )
    d.box(
        "IDEF1X (FIPS PUB 184). ЕСКД ГОСТ 2.104 / ЕСПД ГОСТ 19.105. "
        "Связи без пересечений; копии сущностей — для связей без пересечения линий.",
        SMALL, 60, 85, 1200, 28,
    )

    # Журналы и токен — верхний ряд вправо от пользователя (без пересечений)
    user = d.entity(
        "ПОЛЬЗОВАТЕЛЬ",
        ["КодПользователя"],
        [
            "АдресЭлПочты (AK1)", "ХэшПароля", "Роль",
            "ПризнакБлокировки", "ПризнакСменыПароля",
            "СчётчикНеудачныхВходов", "БлокировкаДо (O)",
            "ДатаСоздания / ДатаИзменения",
        ],
        80, 130,
    )
    tok = d.entity(
        "ТОКЕН ОБНОВЛЕНИЯ",
        ["КодТокена"],
        ["КодПользователя (FK)", "ХэшТокена", "СрокДействия", "ДатаОтзыва (O)", "ЗаменёнНа (O)"],
        360, 130,
    )
    slog = d.entity(
        "ЖУРНАЛ СИСТЕМЫ",
        ["КодЗаписиЖурнала"],
        [
            "МеткаВремени / Уровень / Модуль / Код", "Сообщение",
            "КодПользователя (FK) (O)", "ИдЗапроса (O)", "Детали (O)",
        ],
        640, 130,
    )
    audit = d.entity(
        "ЖУРНАЛ ДОСТУПА",
        ["КодАудита"],
        ["МеткаВремени / Действие / Сущность", "КодПользователя (FK)", "КодОбъекта"],
        920, 130,
    )
    d.rel(user, tok, identifying=False, ex=1, ey=0.5, ix=0, iy=0.5)
    d.label("имеет", 300, 145, 50, 14)
    # копии пользователя у журналов — без веера пересекающихся линий
    user_s = d.entity(
        "ПОЛЬЗОВАТЕЛЬ", ["КодПользователя"], ["АдресЭлПочты (AK1)", "Роль"], 640, 320,
    )
    user_a = d.entity(
        "ПОЛЬЗОВАТЕЛЬ", ["КодПользователя"], ["АдресЭлПочты (AK1)", "Роль"], 920, 320,
    )
    d.rel(user_s, slog, identifying=False, nullable=True, ex=0.5, ey=0, ix=0.5, iy=1)
    d.label("фиксируется", 700, 295, 80, 14)
    d.rel(user_a, audit, identifying=False, ex=0.5, ey=0, ix=0.5, iy=1)
    d.label("порождает", 980, 295, 70, 14)

    disc = d.box("Роль", DISC, 145, 340, 40, 40)
    d.box("", BAR, 137, 380, 56, 8)
    d.cat(user, disc)
    d.label("неполная\nкатегория", 195, 340, 80, 28)

    pat = d.entity(
        "ПАЦИЕНТ", ["КодПользователя (FK)"],
        ["СогласиеНаПДн", "ДатаСогласия (O)"],
        80, 420, dependent=True,
    )
    doc = d.entity("ВРАЧ", ["КодПользователя (FK)"], [], 360, 420, dependent=True)
    d.cat(disc, pat, ex=0.25, ey=1, ix=0.5, iy=0)
    d.cat(disc, doc, ex=0.75, ey=1, ix=0.5, iy=0)

    card = d.entity(
        "МЕДИЦИНСКАЯ КАРТА", ["КодКарты"],
        [
            "КодПациента (FK) (AK1)", "Фамилия / Имя / Отчество (O)",
            "ДатаРождения", "Пол", "ПризнакУдаления / ДатаУдаления (O)",
        ],
        80, 560,
    )
    d.rel(pat, card, identifying=False, ex=0.5, ey=1, ix=0.5, iy=0)
    d.label("имеет", 140, 530, 50, 14)

    link = d.entity(
        "НАЗНАЧЕНИЕ ВРАЧА",
        ["КодВрача (FK)", "КодПациента (FK)"],
        ["ДатаНазначения", "Назначил (O)"],
        360, 560, dependent=True,
    )
    d.rel(doc, link, identifying=True, ex=0.5, ey=1, ix=0.5, iy=0)
    d.rel(pat, link, identifying=True, points=[(300, 480), (300, 590)], ex=1, ey=0.5, ix=0, iy=0.35)
    d.label("назначается", 305, 520, 90, 14)

    dev = d.entity(
        "УСТРОЙСТВО ЭКГ", ["КодУстройства"],
        [
            "Наименование", "ЗаводскойНомер (AK1)",
            "Модель (O) / Описание (O)", "ПризнакУдаления / ДатаУдаления (O)",
        ],
        640, 420,
    )
    ecg = d.entity(
        "ЗАПИСЬ ИССЛЕДОВАНИЯ ЭКГ", ["КодЗаписи"],
        [
            "КодПациента (FK)", "КодУстройства (FK)",
            "ДатаЗагрузки / ИмяФайла", "ЧислоОтсчётов / Начало / Конец",
            "Статус", "КодОшибки (O) / ДоляНепригодного (O)",
            "СрокСырых / ПризнакОчисткиСырых", "СрокФильтр. / ПризнакОчисткиФильтр.",
        ],
        80, 780,
    )
    d.rel(pat, ecg, identifying=False, points=[(50, 500), (50, 850)], ex=0, ey=0.7, ix=0, iy=0.3)
    d.label("сдаёт", 10, 650, 45, 14)
    d.rel(dev, ecg, identifying=False, points=[(720, 580), (720, 850), (340, 850)], ex=0.5, ey=1, ix=1, iy=0.35)
    d.label("регистрирует", 740, 700, 90, 14)

    gaps = d.entity(
        "ПРОПУСК СИГНАЛА", ["КодПропуска"],
        ["КодЗаписи (FK)", "СмещениеНачалаМс / СмещениеКонцаМс", "Причина"],
        640, 620,
    )
    d.rel(ecg, gaps, identifying=False, points=[(400, 820), (640, 820)], ex=1, ey=0.25, ix=0, iy=0.5)
    d.label("содержит", 480, 800, 70, 14)

    rep = d.entity(
        "ОТЧЁТ АНАЛИЗА", ["КодОтчёта"],
        [
            "КодЗаписи (FK) (AK1)", "ДатаСоздания", "Статус",
            "КодВрачаРешения (FK) (O)", "ДатаРешения (O) / Комментарий (O)", "Сводка (O)",
        ],
        80, 1020,
    )
    d.rel(ecg, rep, identifying=False, ex=0.5, ey=1, ix=0.5, iy=0)
    d.label("порождает", 140, 995, 70, 14)

    # копия врача у отчёта — решение без длинной линии через модель
    doc_r = d.entity("ВРАЧ", ["КодПользователя (FK)"], [], 360, 1020, dependent=True)
    d.rel(doc_r, rep, identifying=False, nullable=True, ex=0, ey=0.5, ix=1, iy=0.5)
    d.label("принимает\nрешение", 280, 1000, 80, 28)

    anom = d.entity(
        "АНОМАЛИЯ", ["КодАномалии"],
        [
            "КодОтчёта (FK)", "КодПоказателя", "Направление",
            "СмещениеНачалаМс / СмещениеКонцаМс",
            "ЗначениеМин (O) / ЗначениеМакс (O) / ЕдИзм (O)",
            "ТекстНормы (O) / ЧислоЦиклов (O)",
        ],
        640, 1020,
    )
    d.rel(rep, anom, identifying=False, points=[(340, 1100), (640, 1100)], ex=1, ey=0.7, ix=0, iy=0.5)
    d.label("включает", 480, 1075, 70, 14)

    jobs = d.entity(
        "ОЧЕРЕДЬ ЗАДАНИЙ", ["КодЗадания"],
        [
            "Тип / Параметры", "Статус / ЧислоПопыток",
            "ВыполнитьПосле / Блокировка (O)", "ПоследняяОшибка (O)",
        ],
        920, 620,
    )
    d.label("(логич. связь по параметрам)", 920, 780, 180, 20)

    diag = d.entity(
        "ДИАГНОЗ", ["КодДиагноза"],
        [
            "КодПациента (FK)", "КодВрача (FK)", "КодЗаписи (FK) (O)", "КодОтчёта (FK) (O)",
            "Наименование / Описание", "ДатаДиагноза", "ПризнакУдаления / Аудит (O)",
        ],
        80, 1260,
    )
    d.rel(rep, diag, identifying=False, nullable=True, ex=0.5, ey=1, ix=0.5, iy=0)
    d.label("основан", 140, 1235, 60, 14)

    # копия ЭКГ у диагноза (опц. связь)
    ecg_c = d.entity(
        "ЗАПИСЬ ИССЛЕДОВАНИЯ ЭКГ", ["КодЗаписи"],
        ["КодПациента (FK)", "КодУстройства (FK)"],
        360, 1260,
    )
    d.rel(ecg_c, diag, identifying=False, nullable=True, ex=0, ey=0.5, ix=1, iy=0.35)
    d.label("связан с", 280, 1245, 70, 14)

    doc_d = d.entity("ВРАЧ", ["КодПользователя (FK)"], [], 360, 1400, dependent=True)
    d.rel(doc_d, diag, identifying=False, ex=0.3, ey=0, ix=0.7, iy=1)
    d.label("ставит", 300, 1375, 50, 14)

    pat_c = d.entity(
        "ПАЦИЕНТ", ["КодПользователя (FK)"],
        ["СогласиеНаПДн", "ДатаСогласия (O)"],
        80, 1520, dependent=True,
    )
    d.rel(pat_c, diag, identifying=False, ex=0.5, ey=0, ix=0.5, iy=1)
    d.label("получает", 140, 1495, 70, 14)

    da = d.entity(
        "АНОМАЛИЯ В ДИАГНОЗЕ",
        ["КодДиагноза (FK)", "КодАномалии (FK)"],
        [],
        640, 1260, dependent=True,
    )
    d.rel(anom, da, identifying=True, ex=0.5, ey=1, ix=0.5, iy=0)
    d.rel(diag, da, identifying=True, points=[(340, 1360), (640, 1360)], ex=1, ey=0.7, ix=0, iy=0.5)
    d.label("включает", 480, 1335, 70, 14)

    recom = d.entity(
        "РЕКОМЕНДАЦИЯ", ["КодРекомендации"],
        ["КодДиагноза (FK)", "Текст", "Комментарий (O)", "ПризнакУдаления / Аудит (O)"],
        80, 1680,
    )
    presc = d.entity(
        "НАЗНАЧЕНИЕ ЛС", ["КодНазначения"],
        [
            "КодДиагноза (FK)", "НаименованиеПрепарата", "ДатаНачала / ДатаОкончания",
            "Комментарий (O)", "ПризнакУдаления / Аудит (O)",
        ],
        360, 1680,
    )
    d.rel(diag, recom, identifying=False, points=[(40, 1420), (40, 1720)], ex=0, ey=0.55, ix=0, iy=0.4)
    d.label("содержит", 5, 1580, 45, 14)
    # обход справа от копии врача — не сквозь блоки
    d.rel(
        diag, presc, identifying=False,
        points=[(220, 1480), (520, 1480), (520, 1720), (480, 1720)],
        ex=1, ey=0.8, ix=1, iy=0.4,
    )
    d.label("содержит", 530, 1580, 60, 14)

    d.box("Сигнальные данные DuckDB (идентифицирующая связь по КодЗаписи)", SMALL, 640, 1480, 400, 20)
    raw = d.entity(
        "СЫРОЙ ОТСЧЁТ", ["КодЗаписи (FK)", "СмещениеМс"],
        ["МеткаВремени", "Значение"],
        640, 1520, dependent=True,
    )
    filt = d.entity(
        "ФИЛЬТР. ОТСЧЁТ", ["КодЗаписи (FK)", "СмещениеМс"], ["Значение"],
        640, 1660, dependent=True,
    )
    fid = d.entity(
        "ОПОРНАЯ ТОЧКА",
        ["КодЗаписи (FK)", "НомерЦикла", "НомерСегмента"],
        ["R / Qон / Sофф (мс)", "Pон / Pофф / Tофф / J (O)", "БазоваяЛиния (O) / АмплитудаR (O)"],
        640, 1780, dependent=True,
    )
    # копия записи ЭКГ над DuckDB — короткие вертикали
    ecg_d = d.entity(
        "ЗАПИСЬ ИССЛЕДОВАНИЯ ЭКГ", ["КодЗаписи"],
        ["КодПациента (FK)", "КодУстройства (FK)"],
        900, 1520,
    )
    d.rel(ecg_d, raw, identifying=True, ex=0, ey=0.3, ix=1, iy=0.4)
    d.rel(ecg_d, filt, identifying=True, ex=0, ey=0.55, ix=1, iy=0.4)
    d.rel(ecg_d, fid, identifying=True, ex=0, ey=0.8, ix=1, iy=0.3)
    d.label("хранит", 860, 1600, 50, 14)

    d.box(
        "Условные обозначения IDEF1X: □ независимая; ⌒ зависимая; ——— идентифицирующая; "
        "- - - неидентифицирующая; ● многие; ◇ — NULL у родителя; (FK)/(AK)/(O). "
        "Повтор блока — копия сущности. Физ. имена: users, medical_cards, ecg_records (см. ERD).",
        SMALL, 60, 1980, 1100, 36,
    )
    return d.element()


def main() -> None:
    mx = ET.Element("mxfile", host="app.diagrams.net", agent="Cursor", version="22.1.0")
    mx.append(build_kb())
    mx.append(build_fa())
    text = '<?xml version="1.0" encoding="UTF-8"?>\n' + ET.tostring(mx, encoding="unicode") + "\n"
    ET.fromstring(text)
    OUT.write_text(text, encoding="utf-8")
    print("OK", OUT.stat().st_size, "->", OUT.name)


if __name__ == "__main__":
    main()
