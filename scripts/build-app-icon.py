"""Собирает AppIcon.ico из геометрии знака T-Cue 2e, вариант B — со своей плитой.

Геометрия задана в координатах канвы 64x64: так её отдал дизайн. Каждый размер
рисуется своей геометрией, а не масштабированием одной картинки — на 16 px
решает каждый пиксель.

Плита нужна потому, что иконка в файле одна, а панель задач бывает и светлой,
и тёмной. Без плиты фирменный синий проваливался на светлой панели (2,37:1).
"""
import struct
from io import BytesIO
from pathlib import Path
from PIL import Image, ImageDraw

CANVAS = 64
SUPERSAMPLE = 16  # рисуем крупно и уменьшаем: у ImageDraw нет сглаживания

# Палитра значка совпадает с палитрой приложения: приглушённая шкала и
# акцент на отметке. Плита на тёмной панели задач пропадает (1,04:1), но знак
# не пропадает вместе с ней — обе части читаются прямо по фону панели, 4,5:1 и
# 7,3:1. На светлой панели плита даёт силуэт, 16,5:1.
PLATE = (0x10, 0x18, 0x27, 0xFF)   # Bg.Panel приложения
BAR = (0x6E, 0x7F, 0x96, 0xFF)     # 4,35:1 к плите, 4,52:1 к тёмной панели
MARK = (0x4F, 0xAA, 0xE8, 0xFF)    # 6,98:1 к плите, 7,26:1 к тёмной панели

# размер -> (толщина, перекладина x0, x1, y, низ стойки, радиус узла, радиус плиты)
GEOMETRY = {
    16: (8.5, 17, 47, 24, 45, 9.5, 12),
    20: (8.0, 16, 48, 23, 46, 9.0, 13),
    24: (7.5, 15, 49, 23, 47, 8.5, 13),
    32: (7.0, 14, 50, 22, 48, 8.0, 14),
    40: (6.75, 14, 50, 22, 48, 8.0, 14),
    48: (6.5, 14, 50, 22, 48, 8.0, 14),
}
MASTER = GEOMETRY[48]
SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]


def round_capped_line(draw, x0, y0, x1, y1, width, colour):
    """Линия с круглыми концами: у ImageDraw нет stroke-linecap."""
    radius = width / 2
    draw.line((x0, y0, x1, y1), fill=colour, width=int(round(width)))
    for x, y in ((x0, y0), (x1, y1)):
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=colour)


def render(size):
    stroke, bar_x0, bar_x1, bar_y, stem_y1, node_r, plate_r = GEOMETRY.get(size, MASTER)
    scale = size * SUPERSAMPLE / CANVAS
    side = size * SUPERSAMPLE
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)

    def s(value):
        return value * scale

    draw.rounded_rectangle((0, 0, side - 1, side - 1), radius=s(plate_r), fill=PLATE)
    round_capped_line(draw, s(bar_x0), s(bar_y), s(bar_x1), s(bar_y), s(stroke), BAR)
    round_capped_line(draw, s(CANVAS / 2), s(bar_y), s(CANVAS / 2), s(stem_y1), s(stroke), MARK)
    draw.ellipse(
        (s(CANVAS / 2 - node_r), s(bar_y - node_r), s(CANVAS / 2 + node_r), s(bar_y + node_r)),
        fill=MARK)
    return canvas.resize((size, size), Image.LANCZOS)


def write_ico(images, path):
    """ICO с PNG-записями — так же устроен нынешний AppIcon.ico."""
    blobs = []
    for image in images:
        buffer = BytesIO()
        image.save(buffer, format="PNG", optimize=True)
        blobs.append(buffer.getvalue())

    header = struct.pack("<HHH", 0, 1, len(blobs))
    offset = len(header) + 16 * len(blobs)
    entries, payload = b"", b""
    for image, blob in zip(images, blobs):
        side = 0 if image.width >= 256 else image.width
        entries += struct.pack("<BBBBHHII", side, side, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
        payload += blob
    path.write_bytes(header + entries + payload)


if __name__ == "__main__":
    out = Path(__file__).resolve().parent.parent / "src" / "TempoCue.App" / "Assets" / "Brand"
    images = [render(size) for size in SIZES]
    write_ico(images, out / "AppIcon.ico")
    print("собрано:", ", ".join(str(s) for s in SIZES))
