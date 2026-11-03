# -*- coding: utf-8 -*-
"""Rebuild SA-EKG DFD with clear corridors — no edges through blocks."""
from __future__ import annotations

from pathlib import Path
import xml.etree.ElementTree as ET

OUT = Path(__file__).with_name("SA-EKG_DFD_GOST.drawio")

EXT = (
    "rounded=0;whiteSpace=wrap;html=1;shadow=1;fillColor=#FFFFFF;"
    "strokeColor=#000000;fontFamily=GOST Common;fontSize=12;fontColor=#000000;"
    "align=center;verticalAlign=middle;"
)
PROC = (
    "rounded=1;arcSize=14;whiteSpace=wrap;html=1;fillColor=#FFFFFF;"
    "strokeColor=#000000;fontFamily=GOST Common;fontSize=12;fontColor=#000000;"
    "align=center;verticalAlign=middle;"
)
STORE = (
    "shape=partialRectangle;whiteSpace=wrap;html=1;left=0;right=0;"
    "fillColor=#FFFFFF;strokeColor=#000000;fontFamily=GOST Common;"
    "fontSize=12;fontColor=#000000;align=center;verticalAlign=middle;"
)
TITLE = (
    "text;html=1;strokeColor=none;fillColor=none;align=left;"
    "verticalAlign=middle;fontFamily=GOST Common;fontSize=14;"
    "fontStyle=1;fontColor=#000000;"
)
BAND = (
    "text;html=1;strokeColor=none;fillColor=none;align=left;"
    "fontFamily=GOST Common;fontSize=11;fontStyle=2;fontColor=#666666;"
)
LBL = (
    "text;html=1;strokeColor=none;fillColor=none;align=center;"
    "verticalAlign=middle;fontFamily=GOST Common;fontSize=10;fontColor=#000000;"
)
LEG = (
    "text;html=1;strokeColor=#000000;fillColor=#FFFFFF;align=left;"
    "verticalAlign=middle;fontFamily=GOST Common;fontSize=11;"
    "fontColor=#000000;spacingLeft=8;"
)
EDGE = (
    "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;html=1;"
    "strokeColor=#000000;strokeWidth=1;endArrow=block;endFill=1;"
    "fontFamily=GOST Common;fontSize=10;fontColor=#000000;"
)


def nl(s: str) -> str:
    return s.replace("\n", "&#xa;")


class Diagram:
    def __init__(self, name: str, w: int, h: int):
        self.name = name
        self.w = w
        self.h = h
        self.root = ET.Element("root")
        ET.SubElement(self.root, "mxCell", id="0")
        ET.SubElement(self.root, "mxCell", id="1", parent="0")
        self.n = 0

    def _id(self) -> str:
        self.n += 1
        return f"c{self.n}"

    def box(self, value: str, style: str, x, y, w, h) -> str:
        i = self._id()
        cell = ET.SubElement(
            self.root, "mxCell", id=i, value=nl(value), style=style, vertex="1", parent="1"
        )
        ET.SubElement(
            cell, "mxGeometry", x=str(x), y=str(y), width=str(w), height=str(h), **{"as": "geometry"}
        )
        return i

    def label(self, value: str, x, y, w=130, h=20) -> str:
        return self.box(value, LBL, x, y, w, h)

    def edge(self, src, tgt, points=None, ex=1, ey=0.5, ix=0, iy=0.5) -> str:
        i = self._id()
        style = (
            f"{EDGE}exitX={ex};exitY={ey};exitDx=0;exitDy=0;"
            f"entryX={ix};entryY={iy};entryDx=0;entryDy=0;"
        )
        cell = ET.SubElement(
            self.root, "mxCell", id=i, style=style, edge="1", parent="1", source=src, target=tgt
        )
        geo = ET.SubElement(cell, "mxGeometry", relative="1", **{"as": "geometry"})
        if points:
            arr = ET.SubElement(geo, "Array", **{"as": "points"})
            for x, y in points:
                ET.SubElement(arr, "mxPoint", x=str(x), y=str(y))
        return i

    def element(self) -> ET.Element:
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


def build_context() -> ET.Element:
    d = Diagram("DFD контекст", 1600, 1100)
    d.box(
        "Рисунок — Контекстная диаграмма потоков данных DFD СА-ЭКГ\n(нотация Гейна–Сарсона)",
        TITLE, 40, 20, 1100, 40,
    )
    admin = d.box("Администратор", EXT, 640, 80, 160, 56)
    admin_r = d.box("Администратор", EXT, 1000, 80, 160, 56)
    patient = d.box("Пациент", EXT, 60, 360, 150, 56)
    doctor = d.box("Врач", EXT, 1240, 360, 150, 56)
    device = d.box("Регистратор ЭКГ\n(источник файла)", EXT, 640, 740, 170, 56)
    p0 = d.box("<b>0</b>\nОбработать кардиологическое\nисследование", PROC, 560, 340, 320, 100)

    d.edge(admin, p0, ex=0.5, ey=1, ix=0.5, iy=0)
    d.label("Команды управления\nучётными записями", 730, 190, 150, 36)
    d.edge(p0, admin_r, [(880, 300), (1080, 300), (1080, 136)], ex=0.85, ey=0, ix=0.5, iy=1)
    d.label("Сведения об учётных\nзаписях и журналы", 880, 250, 160, 36)
    d.edge(patient, p0, ex=1, ey=0.3, ix=0, iy=0.3)
    d.label("Файл исследования ЭКГ,\nзапросы сведений", 280, 320, 160, 36)
    d.edge(p0, patient, ex=0, ey=0.75, ix=1, iy=0.75)
    d.label("Принятый отчёт,\nдиагноз, назначения", 280, 430, 160, 36)
    d.edge(doctor, p0, ex=0, ey=0.3, ix=1, iy=0.3)
    d.label("Решение по отчёту,\nдиагноз, назначения", 980, 320, 170, 36)
    d.edge(p0, doctor, ex=1, ey=0.75, ix=0, iy=0.75)
    d.label("Отчёт анализа, сигнал,\nмедицинская карта", 980, 430, 170, 36)
    d.edge(device, p0, ex=0.5, ey=0, ix=0.5, iy=1)
    d.label("Данные файла ЭКГ", 730, 580, 120, 20)
    d.box(
        "Обозначения: прямоугольник с тенью — внешняя сущность; скруглённый прямоугольник — процесс; "
        "стрелка — поток данных. Копии сущностей — чтобы обратные потоки не пересекали прямые.",
        LEG, 40, 920, 1400, 40,
    )
    return d.element()


def build_logical() -> ET.Element:
    d = Diagram("DFD логический", 1900, 2900)
    d.box(
        "Рисунок — Логическая диаграмма потоков данных DFD СА-ЭКГ (уровень 1)\n"
        "Ряды автономны; обратные потоки — к копиям справа; стрелки не проходят сквозь блоки",
        TITLE, 40, 12, 1600, 44,
    )

    # --- 1. Accounts: stores ONLY to the right of P1, medcards BELOW P1 alone ---
    y = 90
    d.box("1. Учётные записи", BAND, 40, y - 28, 200, 18)
    a = d.box("Администратор", EXT, 40, y, 140, 50)
    p1 = d.box("<b>1</b>\nУправлять учётными\nзаписями", PROC, 280, y - 10, 180, 70)
    users = d.box("Пользователи", STORE, 620, y - 40, 180, 44)
    logs = d.box("Журнал аудита", STORE, 620, y + 50, 180, 44)
    ar = d.box("Администратор", EXT, 920, y + 50, 140, 50)
    cards_w = d.box("Медицинские карты", STORE, 280, y + 120, 180, 44)

    d.edge(a, p1)
    d.label("Команды учёток", 180, y - 30, 100, 18)
    # users above-right corridor
    d.edge(p1, users, [(520, y - 50), (520, y - 18)], ex=0.5, ey=0, ix=0, iy=0.5)
    d.label("Данные учётки", 470, y - 70, 100, 18)
    d.edge(users, p1, [(520, y + 5), (520, y + 40)], ex=0, ey=0.7, ix=1, iy=0.25)
    d.label("Сведения об учётке", 470, y + 10, 120, 18)
    # journals: from P1 right side, horizontal at mid, then into logs — logs are RIGHT of P1, cards are BELOW
    d.edge(p1, logs, ex=1, ey=0.75, ix=0, iy=0.5)
    d.label("Запись аудита", 500, y + 55, 100, 18)
    d.edge(logs, ar)
    d.label("Сведения журнала", 810, y + 30, 110, 18)
    # consent straight down — nothing else in this vertical lane
    d.edge(p1, cards_w, ex=0.5, ey=1, ix=0.5, iy=0)
    d.label("Отметка согласия\nна обработку ПД", 100, y + 100, 140, 32)

    # --- 2. Auth ---
    y = 300
    d.box("2. Аутентификация", BAND, 40, y - 28, 180, 18)
    pat = d.box("Пациент", EXT, 40, y, 140, 50)
    p2 = d.box("<b>2</b>\nАутентифицировать\nпользователя", PROC, 280, y - 10, 180, 70)
    users2 = d.box("Пользователи", STORE, 620, y, 180, 50)
    patr = d.box("Пациент", EXT, 960, y, 140, 50)
    d.edge(pat, p2, ex=1, ey=0.3, ix=0, iy=0.3)
    d.label("Учётные данные", 180, y - 32, 100, 18)
    d.edge(p2, users2, ex=1, ey=0.35, ix=0, iy=0.35)
    d.label("Данные для проверки", 500, y - 32, 120, 18)
    d.edge(users2, p2, ex=0, ey=0.7, ix=1, iy=0.7)
    d.label("Результат проверки", 500, y + 55, 120, 18)
    d.edge(p2, patr, [(520, y + 100), (1030, y + 100), (1030, y + 25)], ex=0.5, ey=1, ix=0.5, iy=1)
    d.label("Сеанс доступа", 720, y + 105, 100, 18)

    # --- 3. Medical card ---
    y = 480
    d.box("3. Медицинская карта", BAND, 40, y - 28, 200, 18)
    pat3 = d.box("Пациент", EXT, 40, y, 140, 50)
    p3 = d.box("<b>3</b>\nВести медицинскую\nкарту", PROC, 280, y - 10, 180, 70)
    cards = d.box("Медицинские карты", STORE, 620, y, 200, 50)
    pat3r = d.box("Пациент", EXT, 980, y, 140, 50)
    d.edge(pat3, p3, ex=1, ey=0.3, ix=0, iy=0.3)
    d.label("Данные медкарты", 180, y - 32, 100, 18)
    d.edge(p3, cards, ex=1, ey=0.35, ix=0, iy=0.35)
    d.label("Карта пациента", 500, y - 32, 100, 18)
    d.edge(cards, p3, ex=0, ey=0.7, ix=1, iy=0.7)
    d.label("Сведения карты", 500, y + 55, 100, 18)
    d.edge(p3, pat3r, [(520, y + 100), (1050, y + 100), (1050, y + 25)], ex=0.5, ey=1, ix=0.5, iy=1)
    d.label("Сведения карты", 720, y + 105, 100, 18)

    # --- 4. ECG intake ---
    y = 660
    d.box("4. Приём исследования ЭКГ", BAND, 40, y - 28, 240, 18)
    cards4 = d.box("Медицинские карты", STORE, 340, y, 190, 40)
    p4 = d.box("<b>4</b>\nПринять и проверить\nфайл исследования ЭКГ", PROC, 340, y + 90, 200, 80)
    devices = d.box("Устройства ЭКГ", STORE, 340, y + 220, 190, 40)
    pat4 = d.box("Пациент", EXT, 40, y + 105, 140, 50)
    adm4 = d.box("Администратор", EXT, 40, y + 220, 140, 40)
    recs = d.box("Записи исследований", STORE, 700, y + 50, 200, 50)
    sigs = d.box("Сигналы ЭКГ", STORE, 700, y + 170, 200, 50)
    pat4r = d.box("Пациент", EXT, 1040, y + 105, 140, 50)

    d.edge(pat4, p4)
    d.label("Файл исследования ЭКГ,\nсведения о приборе", 175, y + 75, 140, 32)
    d.edge(p4, pat4r)
    d.label("Результат приёма", 920, y + 85, 100, 18)
    d.edge(cards4, p4, ex=0.5, ey=1, ix=0.5, iy=0)
    d.label("Пол пациента", 550, y + 50, 90, 18)
    d.edge(devices, p4, ex=0.5, ey=0, ix=0.5, iy=1)
    d.label("Сведения о приборе", 550, y + 200, 110, 18)
    d.edge(adm4, devices)
    d.label("Справочник приборов", 175, y + 200, 130, 18)
    d.edge(p4, recs, ex=1, ey=0.25, ix=0, iy=0.5)
    d.label("Метаданные\nзаписи", 580, y + 55, 90, 32)
    d.edge(p4, sigs, ex=1, ey=0.75, ix=0, iy=0.5)
    d.label("Отсчёты\nсигнала", 580, y + 175, 80, 32)

    # --- 5. Analysis ---
    y = 980
    d.box("5. Анализ и отчёт", BAND, 40, y - 28, 180, 18)
    recs5 = d.box("Записи исследований", STORE, 40, y, 200, 44)
    sigs5 = d.box("Сигналы ЭКГ", STORE, 40, y + 160, 200, 44)
    p5 = d.box("<b>5</b>\nПроанализировать\nсигнал ЭКГ", PROC, 460, y + 60, 190, 80)
    p6 = d.box("<b>6</b>\nСформировать\nотчёт анализа", PROC, 820, y + 60, 190, 80)
    reports = d.box("Отчёты анализа", STORE, 1180, y + 20, 190, 50)
    anom = d.box("Аномалии", STORE, 1180, y + 160, 190, 50)

    d.edge(recs5, p5, [(320, y + 22), (320, y + 80)], ex=1, ey=0.5, ix=0, iy=0.25)
    d.label("Запись к анализу", 230, y - 8, 110, 18)
    d.edge(p5, recs5, [(380, y + 120), (380, y + 50), (240, y + 50)], ex=0, ey=0.45, ix=1, iy=0.7)
    d.label("Статус записи", 250, y + 70, 90, 18)
    d.edge(sigs5, p5, [(320, y + 182), (320, y + 120)], ex=1, ey=0.5, ix=0, iy=0.75)
    d.label("Отсчёты сигнала", 230, y + 150, 110, 18)
    d.edge(p5, sigs5, [(400, y + 240), (400, y + 200), (240, y + 200)], ex=0, ey=0.9, ix=1, iy=0.75)
    d.label("Опорные точки и\nотфильтрованный сигнал", 220, y + 230, 160, 32)
    d.edge(p5, p6)
    d.label("Результаты измерений", 660, y + 40, 130, 18)
    d.edge(p6, reports, ex=1, ey=0.3, ix=0, iy=0.5)
    d.label("Отчёт анализа", 1030, y + 30, 100, 18)
    d.edge(p6, anom, ex=1, ey=0.75, ix=0, iy=0.5)
    d.label("Выявленные аномалии", 1020, y + 170, 130, 18)

    # --- 6. Decision ---
    y = 1320
    d.box("6. Решение по отчёту", BAND, 40, y - 28, 200, 18)
    reports6 = d.box("Отчёты анализа", STORE, 300, y, 180, 40)
    cards6 = d.box("Медицинские карты", STORE, 300, y + 220, 180, 40)
    p7 = d.box("<b>7</b>\nПринять решение\nпо отчёту", PROC, 580, y + 80, 180, 70)
    doc = d.box("Врач", EXT, 40, y + 90, 140, 50)
    docr = d.box("Врач", EXT, 900, y + 40, 140, 50)
    patr6 = d.box("Пациент", EXT, 900, y + 150, 140, 50)
    d.edge(doc, p7)
    d.label("Решение по отчёту", 300, y + 65, 120, 18)
    d.edge(reports6, p7, ex=0.5, ey=1, ix=0.5, iy=0)
    d.label("Отчёт на рассмотрение", 420, y + 30, 130, 18)
    d.edge(p7, reports6, [(670, y + 55), (390, y + 55)], ex=0.5, ey=0, ix=0.5, iy=0)
    d.label("Статус решения", 500, y + 10, 100, 18)
    d.edge(cards6, p7, ex=0.5, ey=0, ix=0.5, iy=1)
    d.label("Медкарта пациента", 420, y + 195, 120, 18)
    d.edge(p7, docr, ex=1, ey=0.3, ix=0, iy=0.5)
    d.label("Принятый отчёт", 780, y + 45, 100, 18)
    d.edge(p7, patr6, ex=1, ey=0.75, ix=0, iy=0.5)
    d.label("Принятый отчёт", 780, y + 155, 100, 18)

    # --- 7. Diagnosis ---
    # Широкий зазор P8 → хранилища; подписи СБОКУ от стрелок; прямые горизонтали.
    y = 1680
    d.box("7. Диагноз и назначения", BAND, 40, y - 28, 220, 18)
    anom7 = d.box("Аномалии", STORE, 360, y, 200, 44)
    cards7 = d.box("Медицинские карты", STORE, 360, y + 320, 200, 44)
    doc7 = d.box("Врач", EXT, 40, y + 140, 140, 50)
    p8 = d.box("<b>8</b>\nОформить диагноз,\nрекомендации и назначения", PROC, 340, y + 120, 240, 90)
    diag = d.box("Диагнозы", STORE, 860, y + 80, 200, 50)
    presc = d.box("Рекомендации и назначения", STORE, 860, y + 230, 240, 50)
    pat7 = d.box("Пациент", EXT, 1260, y + 80, 150, 50)
    doc7r = d.box("Врач", EXT, 1260, y + 230, 150, 50)

    d.edge(doc7, p8, ex=1, ey=0.5, ix=0, iy=0.5)
    d.label("Текст диагноза\nи назначений", 180, y + 115, 120, 32)

    d.edge(anom7, p8, ex=0.5, ey=1, ix=0.5, iy=0)
    d.label("Аномалии для\nвключения", 580, y + 40, 120, 32)

    d.edge(cards7, p8, ex=0.5, ey=0, ix=0.5, iy=1)
    d.label("Медкарта\nпациента", 580, y + 280, 110, 32)

    d.edge(p8, diag, [(720, y + 105)], ex=1, ey=0.25, ix=0, iy=0.5)
    d.label("Диагноз", 640, y + 60, 80, 18)
    d.edge(diag, pat7, ex=1, ey=0.5, ix=0, iy=0.5)
    d.label("Диагноз и\nназначения", 1090, y + 50, 120, 32)

    d.edge(p8, presc, [(720, y + 255)], ex=1, ey=0.85, ix=0, iy=0.5)
    d.label("Рекомендации\nи назначения", 620, y + 270, 140, 32)
    d.edge(presc, doc7r, ex=1, ey=0.5, ix=0, iy=0.5)
    d.label("Сведения о\nдиагнозе", 1120, y + 255, 110, 32)

    d.box(
        "Обозначения: скруглённый блок — процесс; прямоугольник с тенью — внешняя сущность; "
        "открытый прямоугольник — хранилище. Копии справа — для обратных потоков. "
        "Подписи без фона. У каждого хранилища есть вход и выход.",
        LEG, 40, 2150, 1700, 48,
    )
    return d.element()


def build_physical() -> ET.Element:
    d = Diagram("DFD физический", 2000, 3400)
    d.box(
        "Рисунок — Физическая (TO-BE) диаграмма потоков данных DFD СА-ЭКГ\n"
        "Стрелки не проходят сквозь блоки; хранилища разнесены по свободным коридорам",
        TITLE, 40, 12, 1600, 44,
    )

    # ========== ROW 1 — FIXED LAYOUT ==========
    # [Admin]→[P1]——————→[P9]→[PDF]
    #            ↑ [Users] ↑          (Users выше коридора P1–P9)
    #            |    [Logs]→[AdminR] (Logs в зазоре под P1–P9, правее карт)
    #         [Cards]                 (только под P1)
    y = 110
    d.box("1. Администрирование", BAND, 40, y - 55, 200, 18)

    a = d.box("Администратор", EXT, 40, y + 40, 140, 50)
    p1 = d.box("<b>1</b>\nУправлять учётными\nзаписями", PROC, 260, y + 30, 180, 70)
    p9 = d.box("<b>9</b>\nСформировать\nдокумент с паролем", PROC, 720, y + 30, 190, 70)
    pdf = d.box("Документ с\nвременным паролем", EXT, 1060, y + 35, 160, 56)

    users = d.box("Пользователи\n(users)", STORE, 480, y - 50, 170, 48)
    logs = d.box("Журналы\n(system_logs, audit)", STORE, 520, y + 140, 190, 48)
    ar = d.box("Администратор", EXT, 860, y + 140, 140, 50)
    cards = d.box("Медицинские карты\n(medical_cards)", STORE, 260, y + 210, 180, 48)

    d.edge(a, p1)
    d.label("Команды учёток", 165, y + 15, 100, 18)

    # P1 → P9: прямой коридор, Users выше линии
    d.edge(p1, p9, ex=1, ey=0.5, ix=0, iy=0.5)
    d.label("Данные временного\nпароля", 540, y + 72, 120, 32)

    d.edge(p9, pdf)
    d.label("Документ с паролем", 940, y + 15, 120, 18)

    # P1 ↔ Users: только вверх от P1, без пересечения P1–P9
    d.edge(p1, users, ex=0.55, ey=0, ix=0.4, iy=1)
    d.label("Данные учётки", 360, y - 45, 100, 18)
    d.edge(users, p1, ex=0.7, ey=1, ix=0.75, iy=0)
    d.label("Сведения об учётке", 620, y - 45, 120, 18)

    # P1 → Logs: из правого нижнего угла P1 вниз в зазор (x=470), затем в Logs
    # Карты только под P1 (x=260…440) — стрелка их не пересекает
    d.edge(p1, logs, [(470, y + 110), (470, y + 164)], ex=1, ey=0.9, ix=0, iy=0.5)
    d.label("Запись журнала", 380, y + 125, 100, 18)

    d.edge(logs, ar)
    d.label("Сведения журнала", 730, y + 120, 110, 18)

    # Согласие: строго вниз под P1
    d.edge(p1, cards, ex=0.4, ey=1, ix=0.4, iy=0)
    d.label("Отметка согласия\nна обработку ПД", 80, y + 185, 140, 32)

    # ========== ROW 2 auth ==========
    y = 420
    d.box("2. Аутентификация", BAND, 40, y - 28, 180, 18)
    pat = d.box("Пациент", EXT, 40, y, 140, 50)
    p2 = d.box("<b>2</b>\nАутентифицировать\nпользователя", PROC, 280, y - 10, 180, 70)
    users2 = d.box("Пользователи\n(users, refresh_tokens)", STORE, 620, y, 210, 50)
    patr = d.box("Пациент", EXT, 980, y, 140, 50)
    d.edge(pat, p2, ex=1, ey=0.3, ix=0, iy=0.3)
    d.label("Учётные данные", 180, y - 32, 100, 18)
    d.edge(p2, users2, ex=1, ey=0.35, ix=0, iy=0.35)
    d.label("Данные для проверки", 500, y - 32, 120, 18)
    d.edge(users2, p2, ex=0, ey=0.7, ix=1, iy=0.7)
    d.label("Результат проверки", 500, y + 55, 120, 18)
    d.edge(p2, patr, [(520, y + 100), (1050, y + 100), (1050, y + 25)], ex=0.5, ey=1, ix=0.5, iy=1)
    d.label("Сеанс доступа", 720, y + 105, 100, 18)

    # ========== ROW 3 ==========
    y = 600
    d.box("3. Медицинская карта", BAND, 40, y - 28, 200, 18)
    pat3 = d.box("Пациент", EXT, 40, y, 140, 50)
    p3 = d.box("<b>3</b>\nВести медицинскую\nкарту", PROC, 280, y - 10, 180, 70)
    cards3 = d.box("Медицинские карты\n(medical_cards)", STORE, 620, y, 210, 50)
    pat3r = d.box("Пациент", EXT, 980, y, 140, 50)
    d.edge(pat3, p3, ex=1, ey=0.3, ix=0, iy=0.3)
    d.label("Данные медкарты", 180, y - 32, 100, 18)
    d.edge(p3, cards3, ex=1, ey=0.35, ix=0, iy=0.35)
    d.label("Карта пациента", 500, y - 32, 100, 18)
    d.edge(cards3, p3, ex=0, ey=0.7, ix=1, iy=0.7)
    d.label("Сведения карты", 500, y + 55, 100, 18)
    d.edge(p3, pat3r, [(520, y + 100), (1050, y + 100), (1050, y + 25)], ex=0.5, ey=1, ix=0.5, iy=1)
    d.label("Сведения карты", 720, y + 105, 100, 18)

    # ========== ROW 4 upload ==========
    # Устройства и временный файл РЯДОМ под P4 — без стрелок сквозь блоки.
    # Подписи только в свободных полях рядом со стрелками.
    y = 780
    d.box("4. Приём файла исследования", BAND, 40, y - 28, 240, 18)
    csv = d.box("Файл исследования\nЭКГ", EXT, 40, y + 20, 160, 50)
    pat4 = d.box("Пациент", EXT, 40, y + 140, 160, 50)
    adm4 = d.box("Администратор", EXT, 40, y + 300, 160, 50)

    cards4 = d.box("Медицинские карты", STORE, 400, y, 240, 44)
    p4 = d.box("<b>4</b>\nПринять и проверить\nфайл исследования ЭКГ", PROC, 400, y + 120, 240, 90)

    # Рядом: devices слева, temp справа — у каждого свой вертикальный коридор в P4
    devices = d.box("Устройства\n(devices)", STORE, 280, y + 320, 200, 50)
    temp = d.box("Временный файл\nпроверки", STORE, 560, y + 320, 200, 50)

    recs = d.box("Записи исследований\n(ecg_records)", STORE, 960, y + 40, 220, 50)
    sigs = d.box("Сигналы ЭКГ\n(DuckDB)", STORE, 960, y + 150, 220, 50)
    jobs = d.box("Очередь заданий\n(background_jobs)", STORE, 960, y + 260, 220, 50)
    pat4r = d.box("Пациент", EXT, 1320, y + 140, 150, 50)

    d.edge(csv, p4, ex=1, ey=0.5, ix=0, iy=0.22)
    d.label("Содержимое\nфайла ЭКГ", 230, y + 20, 100, 32)
    d.edge(pat4, p4, ex=1, ey=0.5, ix=0, iy=0.55)
    d.label("Запрос приёма,\nсведения о приборе", 210, y + 150, 130, 32)

    d.edge(cards4, p4, ex=0.5, ey=1, ix=0.5, iy=0)
    d.label("Пол пациента", 660, y + 55, 100, 18)

    # devices → P4: левая половина низа P4
    d.edge(devices, p4, ex=0.5, ey=0, ix=0.25, iy=1)
    d.label("Сведения\nо приборе", 200, y + 255, 100, 32)
    d.edge(adm4, devices, ex=1, ey=0.5, ix=0, iy=0.5)
    d.label("Справочник\nприборов", 50, y + 255, 100, 32)

    # P4 → temp: вертикаль над temp (x≈592), не через devices
    d.edge(p4, temp, [(592, y + 250)], ex=0.8, ey=1, ix=0.2, iy=0)
    d.label("Временные\nданные файла", 470, y + 255, 100, 32)
    # temp → P4: справа от temp вверх, затем в правый низ P4
    d.edge(
        temp, p4,
        [(780, y + 300), (780, y + 230)],
        ex=1, ey=0.3, ix=0.95, iy=1,
    )
    d.label("Данные для\nпроверки", 800, y + 340, 110, 32)

    # К пациенту — сверху над хранилищами, без сквозного прохода
    d.edge(
        p4, pat4r,
        [(800, y - 5), (1395, y - 5), (1395, y + 140)],
        ex=0.75, ey=0, ix=0.5, iy=0,
    )
    d.label("Результат приёма", 1100, y - 30, 110, 18)

    d.edge(p4, recs, ex=1, ey=0.2, ix=0, iy=0.5)
    d.label("Метаданные\nзаписи", 820, y + 40, 100, 32)
    d.edge(p4, sigs, ex=1, ey=0.45, ix=0, iy=0.5)
    d.label("Отсчёты\nсигнала", 820, y + 145, 90, 32)
    d.edge(p4, jobs, ex=1, ey=0.7, ix=0, iy=0.5)
    d.label("Задание\nна анализ", 820, y + 220, 90, 32)

    # ========== ROW 5 analysis ==========
    y = 1280
    d.box("5. Анализ сигнала и формирование отчёта", BAND, 40, y - 28, 320, 18)
    jobs5 = d.box("Очередь заданий", STORE, 40, y, 200, 40)
    recs5 = d.box("Записи исследований", STORE, 40, y + 130, 200, 40)
    sigs5 = d.box("Сигналы ЭКГ (DuckDB)", STORE, 40, y + 280, 200, 40)
    cards5 = d.box("Медицинские карты", STORE, 40, y + 430, 200, 40)
    p5 = d.box("<b>5</b>\nПроанализировать\nсигнал ЭКГ", PROC, 500, y + 180, 190, 80)
    p6 = d.box("<b>6</b>\nСформировать\nотчёт анализа", PROC, 880, y + 180, 190, 80)
    reports = d.box("Отчёты анализа\n(analysis_reports)", STORE, 1240, y + 120, 210, 50)
    anom = d.box("Аномалии\n(anomalies)", STORE, 1240, y + 260, 210, 50)

    d.edge(jobs5, p5, [(340, y + 20), (340, y + 200)], ex=1, ey=0.5, ix=0, iy=0.25)
    d.label("Задание на анализ", 240, y - 5, 120, 18)
    d.edge(recs5, p5, [(340, y + 150), (340, y + 210)], ex=1, ey=0.5, ix=0, iy=0.4)
    d.label("Запись к анализу", 240, y + 125, 110, 18)
    d.edge(p5, recs5, [(420, y + 240), (420, y + 165), (240, y + 165)], ex=0, ey=0.55, ix=1, iy=0.7)
    d.label("Статус записи", 260, y + 185, 90, 18)
    d.edge(sigs5, p5, [(340, y + 300), (340, y + 240)], ex=1, ey=0.5, ix=0, iy=0.75)
    d.label("Отсчёты сигнала", 240, y + 275, 110, 18)
    d.edge(p5, sigs5, [(460, y + 360), (460, y + 320), (240, y + 320)], ex=0, ey=0.9, ix=1, iy=0.75)
    d.label("Опорные точки и\nотфильтрованный сигнал", 240, y + 345, 160, 32)
    d.edge(cards5, p5, [(340, y + 450), (340, y + 270), (500, y + 270)], ex=1, ey=0.5, ix=0.3, iy=1)
    d.label("Пол пациента", 240, y + 430, 90, 18)
    d.edge(p5, p6)
    d.label("Результаты измерений", 710, y + 160, 130, 18)
    d.edge(p6, reports, ex=1, ey=0.3, ix=0, iy=0.5)
    d.label("Отчёт анализа", 1100, y + 130, 100, 18)
    d.edge(p6, anom, ex=1, ey=0.75, ix=0, iy=0.5)
    d.label("Выявленные аномалии", 1090, y + 270, 130, 18)
    d.edge(sigs5, p6, [(300, y + 90), (975, y + 90)], ex=0.75, ey=0, ix=0.5, iy=0)
    d.label("Отсчёты для графика отчёта", 560, y + 65, 160, 18)

    # ========== ROW 6 purge ==========
    y = 1900
    d.box("6. Очистка устаревших сигналов", BAND, 40, y - 28, 280, 18)
    recs6 = d.box("Записи исследований", STORE, 40, y, 200, 44)
    p10 = d.box("<b>10</b>\nУдалить устаревшие\nотсчёты сигнала", PROC, 380, y - 10, 210, 70)
    sigs6 = d.box("Сигналы ЭКГ (DuckDB)", STORE, 740, y, 200, 44)
    d.edge(recs6, p10, ex=1, ey=0.35, ix=0, iy=0.35)
    d.label("Сроки хранения записи", 250, y - 28, 130, 18)
    d.edge(p10, recs6, ex=0, ey=0.7, ix=1, iy=0.7)
    d.label("Отметка удаления", 250, y + 50, 110, 18)
    d.edge(p10, sigs6, ex=1, ey=0.35, ix=0, iy=0.35)
    d.label("Команда удаления отсчётов", 600, y - 28, 150, 18)
    d.edge(sigs6, p10, ex=0, ey=0.7, ix=1, iy=0.7)
    d.label("Подтверждение удаления", 600, y + 50, 140, 18)

    # ========== ROW 7 decision ==========
    y = 2080
    d.box("7. Решение врача по отчёту", BAND, 40, y - 28, 240, 18)
    reports7 = d.box("Отчёты анализа", STORE, 300, y, 180, 40)
    p7 = d.box("<b>7</b>\nПринять решение\nпо отчёту", PROC, 580, y + 80, 180, 70)
    doc = d.box("Врач", EXT, 40, y + 90, 140, 50)
    docr = d.box("Врач", EXT, 920, y + 40, 140, 50)
    patr7 = d.box("Пациент", EXT, 920, y + 150, 140, 50)
    d.edge(doc, p7)
    d.label("Решение по отчёту", 300, y + 65, 120, 18)
    d.edge(reports7, p7, ex=0.5, ey=1, ix=0.5, iy=0)
    d.label("Отчёт на рассмотрение", 420, y + 30, 130, 18)
    d.edge(p7, reports7, [(670, y + 55), (390, y + 55)], ex=0.5, ey=0, ix=0.5, iy=0)
    d.label("Статус решения", 500, y + 10, 100, 18)
    d.edge(p7, docr, ex=1, ey=0.3, ix=0, iy=0.5)
    d.label("Принятый отчёт", 800, y + 45, 100, 18)
    d.edge(p7, patr7, ex=1, ey=0.75, ix=0, iy=0.5)
    d.label("Принятый отчёт", 800, y + 155, 100, 18)

    # ========== ROW 8 diagnosis ==========
    # Широкий зазор; прямые стрелки; подписи рядом, не на линиях.
    y = 2360
    d.box("8. Диагноз и назначения", BAND, 40, y - 28, 220, 18)
    anom8 = d.box("Аномалии", STORE, 380, y, 200, 44)
    cards8 = d.box("Медицинские карты", STORE, 380, y + 330, 200, 44)
    doc8 = d.box("Врач", EXT, 40, y + 150, 150, 50)
    p8 = d.box("<b>8</b>\nОформить диагноз,\nрекомендации и назначения", PROC, 360, y + 130, 250, 90)
    # Хранилища разнесены по вертикали; подписи НАД/ПОД стрелками в широком зазоре
    diag = d.box("Диагнозы\n(diagnoses)", STORE, 900, y + 90, 210, 50)
    presc = d.box("Рекомендации и назначения", STORE, 900, y + 240, 240, 50)
    pat8 = d.box("Пациент", EXT, 1300, y + 90, 150, 50)
    doc8r = d.box("Врач", EXT, 1300, y + 240, 150, 50)

    d.edge(doc8, p8, ex=1, ey=0.5, ix=0, iy=0.5)
    d.label("Текст диагноза\nи назначений", 190, y + 125, 120, 32)

    d.edge(anom8, p8, ex=0.5, ey=1, ix=0.5, iy=0)
    d.label("Аномалии для\nвключения", 620, y + 45, 120, 32)

    d.edge(cards8, p8, ex=0.5, ey=0, ix=0.5, iy=1)
    d.label("Медкарта\nпациента", 620, y + 290, 110, 32)

    d.edge(p8, diag, [(780, y + 115)], ex=1, ey=0.25, ix=0, iy=0.5)
    d.label("Диагноз", 700, y + 70, 80, 18)
    d.edge(diag, pat8, ex=1, ey=0.5, ix=0, iy=0.5)
    d.label("Диагноз и\nназначения", 1130, y + 60, 120, 32)

    d.edge(p8, presc, [(780, y + 265)], ex=1, ey=0.85, ix=0, iy=0.5)
    d.label("Рекомендации\nи назначения", 660, y + 280, 140, 32)
    d.edge(presc, doc8r, ex=1, ey=0.5, ix=0, iy=0.5)
    d.label("Сведения о\nдиагнозе", 1160, y + 265, 110, 32)

    d.box(
        "Обозначения: процесс — скруглённый блок; внешняя сущность — прямоугольник с тенью; "
        "хранилище — открытый прямоугольник. Подписи без фона. Стрелки не проходят сквозь блоки.",
        LEG, 40, 2850, 1800, 40,
    )
    return d.element()


def main() -> None:
    mxfile = ET.Element("mxfile", host="app.diagrams.net", agent="Cursor", version="22.1.0")
    for b in (build_context, build_logical, build_physical):
        mxfile.append(b())
    rough = ET.tostring(mxfile, encoding="unicode").replace("&amp;#xa;", "&#xa;")
    text = '<?xml version="1.0" encoding="UTF-8"?>\n' + rough + "\n"
    ET.fromstring(text)
    OUT.write_text(text, encoding="utf-8")
    print("OK", OUT.stat().st_size)


if __name__ == "__main__":
    main()
