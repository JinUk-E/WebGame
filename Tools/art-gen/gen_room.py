# -*- coding: utf-8 -*-
"""Room — 바닥(장판)·벽 테두리·창문·문. 탑뷰, PPU 100.

방 외벽 포함 14x9유닛(1400x900px), 벽 두께 0.6유닛(60px) → 내부 바닥 1280x780px.
"""
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

from artgen_common import (
    HANJI_BRIGHT, HANJI_DIM, WOOD_DARK, WOOD_LIGHT, INK,
    apply_grain, apply_streaks, add_stains, canvas, merge, mix, mul,
    overlay, radial_mask, rgba, save, vgrad, downscale,
)

# 상수 (재현 파라미터)
FLOOR_W, FLOOR_H = 1280, 780
WALL_W, WALL_H = 1400, 900
WALL_T = 60                      # 벽 두께 px
JANGPAN = (118, 96, 62)          # 장판 기본색 (나무 갈색 계열 중간 밝기)
JANGPAN_EDGE = (96, 78, 50)
SEAM_SPACING = 160               # 장판 이음매 간격
PILLAR = mul(WOOD_DARK, 0.9)


# ---------------------------------------------------------------- 바닥
def gen_floor():
    rng = random.Random(41001)
    img = vgrad(FLOOR_W, FLOOR_H, mix(JANGPAN, (140, 116, 76), 0.25), JANGPAN).convert("RGBA")

    # 장판 이음매 (세로) — 미세한 명암 밴드 + 이음선 (반투명 → 오버레이 레이어)
    seams, d = overlay(img)
    for x in range(SEAM_SPACING, FLOOR_W + SEAM_SPACING, SEAM_SPACING):
        shade = rng.choice((-1, 1))
        d.rectangle([x - SEAM_SPACING, 0, min(x, FLOOR_W), FLOOR_H],
                    fill=rgba((0, 0, 0), 10) if shade < 0 else rgba((255, 245, 220), 8))
        if x <= FLOOR_W - 2:
            d.line([x, 0, x, FLOOR_H], fill=rgba(mul(JANGPAN, 0.72), 130), width=2)
            d.line([x + 2, 0, x + 2, FLOOR_H], fill=rgba((240, 226, 190), 26), width=1)
    img = merge(img, seams)

    # 나뭇결 (세로로 늘린 노이즈)
    img = apply_streaks(img, opacity=0.10, axis="v", cell=18, blur=1)
    # 생활 얼룩
    img = add_stains(img, rng, 9, mul(JANGPAN, 0.55), alpha=(8, 20), radius=(40, 110))
    img = add_stains(img, rng, 4, (210, 196, 156), alpha=(6, 12), radius=(30, 70))
    # 가장자리 어둡게 (비네트)
    vig = radial_mask(FLOOR_W, FLOOR_H).point(lambda v: int(v * 0.30))
    dark = Image.new("RGBA", img.size, rgba(mul(JANGPAN_EDGE, 0.6), 255))
    img = Image.composite(dark, img, vig).convert("RGBA")
    # 미세 노이즈
    img = apply_grain(img, opacity=0.06, sigma=26)
    save(img, "Room/room_floor.png")


# ---------------------------------------------------------------- 벽 테두리
def gen_wall_frame():
    rng = random.Random(41002)
    img = canvas(WALL_W, WALL_H)
    d = ImageDraw.Draw(img, "RGBA")

    # 한지벽 기본 밴드 (어두운 씬 톤)
    wall_base = mul(HANJI_DIM, 0.72)  # (132, 125, 107)
    d.rectangle([0, 0, WALL_W, WALL_H], fill=rgba(wall_base))
    # 종이 섬유질감은 마지막에 grain으로

    # 벽지 패널 나눔선 (은은한 세로 줄 — 반투명이므로 오버레이)
    lines, ld = overlay(img)
    for x in range(0, WALL_W, 175):
        ld.line([x, 0, x, WALL_H], fill=rgba(mul(wall_base, 0.88), 90), width=2)
    img = merge(img, lines)
    d = ImageDraw.Draw(img, "RGBA")

    corner = 46
    # 모서리 기둥 (사각 블록)
    for cx, cy in ((0, 0), (WALL_W, 0), (0, WALL_H), (WALL_W, WALL_H)):
        d.rectangle([cx - corner, cy - corner, cx + corner, cy + corner], fill=rgba(mul(WOOD_DARK, 0.8)))
    # 상하 벽의 중간 기둥
    for fx in (0.25, 0.5, 0.75):
        x = int(WALL_W * fx)
        d.rectangle([x - 20, 0, x + 20, WALL_T], fill=rgba(PILLAR))
        d.rectangle([x - 20, WALL_H - WALL_T, x + 20, WALL_H], fill=rgba(PILLAR))
        d.line([x - 20, 0, x - 20, WALL_T], fill=rgba(mul(PILLAR, 0.6)), width=2)
        d.line([x - 20, WALL_H - WALL_T, x - 20, WALL_H], fill=rgba(mul(PILLAR, 0.6)), width=2)
    # 좌우 벽의 중간 기둥
    for fy in (0.33, 0.66):
        y = int(WALL_H * fy)
        d.rectangle([0, y - 20, WALL_T, y + 20], fill=rgba(PILLAR))
        d.rectangle([WALL_W - WALL_T, y - 20, WALL_W, y + 20], fill=rgba(PILLAR))

    # 질감
    img = apply_grain(img, opacity=0.09, sigma=30, blur=1)
    img = add_stains(img, rng, 10, mul(wall_base, 0.5), alpha=(8, 16), radius=(20, 60))

    d = ImageDraw.Draw(img, "RGBA")
    hole = [WALL_T, WALL_T, WALL_W - WALL_T, WALL_H - WALL_T]
    # 안쪽 테두리 — 걸레받이(어두운 나무 선)
    d.rectangle([hole[0], hole[1], hole[2] - 1, hole[3] - 1],
                outline=rgba(mul(WOOD_DARK, 0.55)), width=6)
    hl, hld = overlay(img)
    hld.rectangle([hole[0] + 6, hole[1] + 6, hole[2] - 7, hole[3] - 7],
                  outline=rgba(mul(WOOD_LIGHT, 1.1), 120), width=2)
    img = merge(img, hl)
    d = ImageDraw.Draw(img, "RGBA")
    # 바깥 테두리 — 어둠으로 잠기는 외곽
    d.rectangle([0, 0, WALL_W - 1, WALL_H - 1], outline=rgba((10, 10, 14)), width=4)

    # 중앙 구멍 뚫기
    mask = Image.new("L", (WALL_W, WALL_H), 255)
    md = ImageDraw.Draw(mask)
    md.rectangle([hole[0] + 6, hole[1] + 6, hole[2] - 7, hole[3] - 7], fill=0)
    img.putalpha(ImageChops.multiply(img.getchannel("A"), mask))
    save(img, "Room/room_wall_frame.png")


# ---------------------------------------------------------------- 창문 (여명의 핵심 — 존재감 있게)
def gen_window():
    rng = random.Random(41003)
    S = 4
    W, H = 180, 80
    img = canvas(W * S, H * S)
    d = ImageDraw.Draw(img, "RGBA")
    w, h = W * S, H * S

    # 바깥 창틀 (어두운 나무, 라운드 약간)
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=3 * S, fill=rgba(mul(WOOD_DARK, 0.85)))
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=3 * S, outline=rgba(mul(WOOD_DARK, 0.5)), width=S)
    hl, hld = overlay(img)
    hld.line([2 * S, 2 * S, w - 2 * S, 2 * S], fill=rgba(mul(WOOD_LIGHT, 1.2), 150), width=S)
    img = merge(img, hl)

    # 창호지 영역 — 밝은 아이보리 (빛 투과 수용부)
    pad = 8 * S
    paper_box = [pad, pad, w - pad, h - pad]
    paper = vgrad(w, h, mix(HANJI_BRIGHT, (255, 248, 226), 0.45), HANJI_BRIGHT).convert("RGBA")
    # 중앙이 살짝 더 밝게 (투과광 느낌)
    glow_m = radial_mask(w, h, inner=255, outer=0).point(lambda v: int(v * 0.35))
    glow = Image.new("RGBA", (w, h), rgba((255, 252, 238)))
    paper = Image.composite(glow, paper, glow_m).convert("RGBA")
    paper = apply_grain(paper, opacity=0.07, sigma=24, blur=1)
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle(paper_box, radius=2 * S, fill=255)
    img.paste(paper, (0, 0), mask)

    d = ImageDraw.Draw(img, "RGBA")
    # 살 격자 — 세로살 촘촘 + 가로살 1개 (전통 세살창)
    bar = mul(WOOD_DARK, 0.75)
    n_v = 8
    hl, hld = overlay(img)
    for i in range(1, n_v):
        x = pad + (w - pad * 2) * i / n_v
        d.rectangle([x - 1.5 * S, pad, x + 1.5 * S, h - pad], fill=rgba(bar))
        hld.line([x + 1.5 * S, pad, x + 1.5 * S, h - pad], fill=rgba(mul(bar, 1.4), 120), width=S // 2)
    ym = h / 2
    d.rectangle([pad, ym - 1.5 * S, w - pad, ym + 1.5 * S], fill=rgba(bar))
    img = merge(img, hl)
    d = ImageDraw.Draw(img, "RGBA")
    # 창틀 안쪽 선
    d.rounded_rectangle(paper_box, radius=2 * S, outline=rgba(mul(WOOD_DARK, 0.6)), width=S)

    img = downscale(img, S)
    img = apply_grain(img, opacity=0.04, sigma=20)
    save(img, "Room/room_window.png")


# ---------------------------------------------------------------- 문 (장지문 + 걸쇠)
def gen_door():
    rng = random.Random(41004)
    S = 4
    W, H = 160, 90
    w, h = W * S, H * S
    img = canvas(w, h)
    d = ImageDraw.Draw(img, "RGBA")

    # 문틀
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=2 * S, fill=rgba(mul(WOOD_DARK, 0.8)))
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=2 * S, outline=rgba(mul(WOOD_DARK, 0.45)), width=S)
    hl, hld = overlay(img)
    hld.line([2 * S, 2 * S, w - 2 * S, 2 * S], fill=rgba(mul(WOOD_LIGHT, 1.15), 140), width=S)
    img = merge(img, hl)
    d = ImageDraw.Draw(img, "RGBA")

    pad = 7 * S
    panel_gap = 3 * S  # 중앙 미닫이 맞물림
    paper_col = mul(HANJI_DIM, 0.92)  # 창보다 어두운 장지
    for side in (0, 1):
        x0 = pad if side == 0 else w / 2 + panel_gap / 2
        x1 = w / 2 - panel_gap / 2 if side == 0 else w - pad
        box = [x0, pad, x1, h - pad]
        paper = vgrad(int(x1 - x0), h - pad * 2, mix(paper_col, HANJI_BRIGHT, 0.12), mul(paper_col, 0.88)).convert("RGBA")
        paper = apply_grain(paper, opacity=0.08, sigma=26, blur=1)
        img.paste(paper, (int(x0), pad))
        dd = ImageDraw.Draw(img, "RGBA")
        # 살 — 성근 격자 (세로 3, 가로 2)
        bar = mul(WOOD_DARK, 0.7)
        for i in range(1, 4):
            x = x0 + (x1 - x0) * i / 4
            dd.rectangle([x - 1.2 * S, pad, x + 1.2 * S, h - pad], fill=rgba(bar))
        for j in range(1, 3):
            y = pad + (h - pad * 2) * j / 3
            dd.rectangle([x0, y - 1.2 * S, x1, y + 1.2 * S], fill=rgba(bar))
        dd.rectangle(box, outline=rgba(mul(WOOD_DARK, 0.6)), width=S)

    d = ImageDraw.Draw(img, "RGBA")
    # 중앙 문설주 (미닫이 겹침부)
    d.rectangle([w / 2 - panel_gap, pad - S, w / 2 + panel_gap, h - pad + S], fill=rgba(mul(WOOD_DARK, 0.65)))

    # 걸쇠 (금속 디테일 — 중앙 하단)
    lx, ly = w / 2, h - pad - 8 * S
    metal = (118, 120, 126)
    d.rounded_rectangle([lx - 9 * S, ly - 2 * S, lx + 9 * S, ly + 2 * S], radius=S, fill=rgba(metal))
    d.ellipse([lx - 3 * S, ly - 3.4 * S, lx + 3 * S, ly + 3.4 * S], outline=rgba(mul(metal, 0.75)), width=int(1.4 * S))
    d.ellipse([lx - 1.2 * S, ly - 1.2 * S, lx + 1.2 * S, ly + 1.2 * S], fill=rgba(mul(metal, 0.6)))
    hl, hld = overlay(img)
    hld.line([w / 2 - panel_gap, pad - S, w / 2 - panel_gap, h - pad + S], fill=rgba(mul(WOOD_LIGHT, 1.1), 110), width=S // 2)
    hld.line([lx - 9 * S, ly - 2 * S, lx + 9 * S, ly - 2 * S], fill=rgba(mul(metal, 1.4), 200), width=S // 2)
    img = merge(img, hl)

    img = downscale(img, S)
    img = apply_grain(img, opacity=0.04, sigma=20)
    save(img, "Room/room_door.png")


def run():
    print("[Room]")
    gen_floor()
    gen_wall_frame()
    gen_window()
    gen_door()


if __name__ == "__main__":
    run()
