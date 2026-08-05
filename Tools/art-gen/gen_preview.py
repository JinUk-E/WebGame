# -*- coding: utf-8 -*-
"""전 스프라이트 검수용 몽타주 → Tools/art-gen/preview.png"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from artgen_common import ART_ROOT, FONT_PATH, NIGHT_DARK, mix, rgba

OUT = Path(__file__).resolve().parent / "preview.png"
PAGE_W = 1900
PAD = 26
LABEL_H = 22
MAX_W, MAX_H = 430, 330  # 셀 내 표시 최대 크기 (넘으면 축소)

SECTIONS = (
    ("Room — 바닥·벽·창문·문", "Room"),
    ("Props — 소품", "Props"),
    ("UI — 프레임·버튼·부적", "UI"),
    ("Portraits — 초상 4종", "Portraits"),
)


def load_fonts():
    try:
        return (ImageFont.truetype(str(FONT_PATH), 30), ImageFont.truetype(str(FONT_PATH), 15))
    except OSError:
        f = ImageFont.load_default()
        return (f, f)


def run():
    font_h, font_s = load_fonts()
    cells = []  # (section_index, name, img, scale)
    for si, (_, folder) in enumerate(SECTIONS):
        for p in sorted((ART_ROOT / folder).glob("*.png")):
            if p.name == "heart128.png":  # 기존 임시 에셋 제외
                continue
            im = Image.open(p).convert("RGBA")
            sc = min(1.0, MAX_W / im.width, MAX_H / im.height)
            disp = im.resize((max(1, int(im.width * sc)), max(1, int(im.height * sc))),
                             Image.LANCZOS) if sc < 1.0 else im
            label = f"{p.name}  {im.width}x{im.height}" + (f"  (x{sc:.2f})" if sc < 1.0 else "")
            cells.append((si, label, disp))

    # 섹션별 플로우 레이아웃
    bg_a = NIGHT_DARK
    bg_b = mix(NIGHT_DARK, (0, 0, 0), 0.4)
    rows = []  # (y, height) 계산용 — 먼저 배치 시뮬레이션
    layout = []
    y = PAD
    for si, (title, _) in enumerate(SECTIONS):
        layout.append(("title", title, PAD, y))
        y += 46
        x = PAD
        row_h = 0
        for ci, (csi, label, disp) in enumerate(cells):
            if csi != si:
                continue
            cw = max(disp.width, 150) + PAD
            if x + cw > PAGE_W - PAD:
                x = PAD
                y += row_h + LABEL_H + PAD
                row_h = 0
            layout.append(("cell", (label, disp), x, y))
            x += cw
            row_h = max(row_h, disp.height)
        y += row_h + LABEL_H + PAD + 16
    page_h = y + PAD

    page = Image.new("RGBA", (PAGE_W, page_h), rgba(bg_b))
    d = ImageDraw.Draw(page)
    # 체커보드 (투명 확인용 은은한 격자)
    for gy in range(0, page_h, 24):
        for gx in range(0, PAGE_W, 24):
            if (gx // 24 + gy // 24) % 2 == 0:
                d.rectangle([gx, gy, gx + 24, gy + 24], fill=rgba(bg_a))

    for kind, data, x, yy in layout:
        if kind == "title":
            d.text((x, yy), data, font=font_h, fill=(232, 220, 192, 255))
            d.line([x, yy + 40, PAGE_W - PAD, yy + 40], fill=(107, 81, 56, 255), width=2)
        else:
            label, disp = data
            page.alpha_composite(disp, (x, yy))
            d.text((x, yy + disp.height + 4), label, font=font_s, fill=(184, 174, 149, 255))

    page.convert("RGB").save(OUT, optimize=True)
    print(f"preview → {OUT}  ({PAGE_W}x{page_h})")


if __name__ == "__main__":
    run()
