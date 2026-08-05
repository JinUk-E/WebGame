# -*- coding: utf-8 -*-
"""UI — 대화 프레임·화자명 패널·버튼·키캡·슬라이더·상태 부적(연소 5단계).

기준 캔버스 1920x1080 (CanvasScaler Match 1). 버튼/패널 텍스트는 코드(TMP)가 얹는다 — 전부 빈 판.
"""
import math
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

from artgen_common import (
    CHAR, EMBER, EMBER_HOT, HANJI_BRIGHT, HANJI_DIM, INK, TALISMAN_PAPER,
    TALISMAN_RED, apply_grain, brush_border, canvas, clip_to, downscale,
    merge, mix, mul, overlay, radial_mask, rgba, save, vgrad,
    wobble_rect_poly,
)

S = 2  # UI는 2배 슈퍼샘플 (대형이라 4배는 과함)

# 연소 진행률 (0=멀쩡 → 1=완전 연소). 스펙: 0/10/30/60/완전
BURN_STAGES = (0.0, 0.10, 0.30, 0.60, 0.97)


# ---------------------------------------------------------------- 한지 패널 공통
def _hanji_panel(rng, W, H, base_dark, base_light, border_w, jitter, radius=10,
                 alpha=255, corner_flick=True):
    w, h = W * S, H * S
    img = canvas(w, h)
    d = ImageDraw.Draw(img, "RGBA")
    inset = int(border_w * S * 1.4)
    box = [inset, inset, w - inset, h - inset]

    # 종이 본체
    paper = vgrad(w, h, base_light, base_dark).convert("RGBA")
    m = Image.new("L", (w, h), 0)
    ImageDraw.Draw(m).rounded_rectangle(box, radius=radius * S, fill=int(alpha))
    paper.putalpha(m)
    paper = apply_grain(paper, opacity=0.10, sigma=30, blur=1)
    img = Image.alpha_composite(img, paper)

    # 안쪽 비네트 (가장자리 어둡게)
    vig = radial_mask(w, h).point(lambda v: int(v * 0.18))
    dark = Image.new("RGBA", (w, h), rgba(mul(base_dark, 0.55), 255))
    vig_l = ImageChops.multiply(vig, m.point(lambda v: 255 if v else 0))
    img = Image.composite(dark, img, vig_l).convert("RGBA")
    img.putalpha(m)  # 알파는 패널 마스크 그대로

    # 먹 붓 테두리 — 이중 스트로크 (두꺼운 반투명 + 얇은 진한)
    ink_layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    idw = ImageDraw.Draw(ink_layer)
    bb = [inset, inset, w - inset, h - inset]
    brush_border(idw, bb, rng, width=border_w * S * 1.5, jitter=jitter * S * 0.9,
                 color=INK, alpha=110, step=4 * S)
    brush_border(idw, bb, rng, width=border_w * S, jitter=jitter * S * 0.5,
                 color=INK, alpha=235, step=3 * S)
    if corner_flick:
        # 모서리 붓 삐침
        fl = border_w * S * 2.2
        for cxa, cya, dx, dy in ((bb[0], bb[1], -1, -1), (bb[2], bb[1], 1, -1),
                                 (bb[2], bb[3], 1, 1), (bb[0], bb[3], -1, 1)):
            idw.line([(cxa, cya), (cxa + dx * fl, cya + dy * fl * 0.7)],
                     fill=rgba(INK, 190), width=int(border_w * S * 0.7))
    img = Image.alpha_composite(img, ink_layer)
    return img


# ---------------------------------------------------------------- 대화 프레임 (하단 와이드)
def gen_dialogue_frame():
    rng = random.Random(43001)
    # 어두운 한지 (자막 가독 우선 — 톤 다운)
    img = _hanji_panel(rng, 1600, 360,
                       base_dark=(44, 39, 31), base_light=(62, 55, 43),
                       border_w=5, jitter=2.2, radius=14, alpha=242)
    img = downscale(img, S)
    save(img, "UI/ui_dialogue_frame.png")


# ---------------------------------------------------------------- 화자명 소패널
def gen_name_panel():
    rng = random.Random(43002)
    img = _hanji_panel(rng, 360, 90,
                       base_dark=(58, 51, 39), base_light=(84, 74, 56),
                       border_w=4, jitter=1.8, radius=10, alpha=248)
    img = downscale(img, S)
    save(img, "UI/ui_name_panel.png")


# ---------------------------------------------------------------- 버튼 2상태 (빈 판)
def gen_buttons():
    rng = random.Random(43003)
    # 일반 — 어두운 한지 태그
    img = _hanji_panel(rng, 360, 110,
                       base_dark=mul(HANJI_DIM, 0.62), base_light=mul(HANJI_DIM, 0.80),
                       border_w=4, jitter=1.6, radius=8)
    img = downscale(img, S)
    save(img, "UI/ui_button_normal.png")

    # 호버 — 밝은 한지 + 황지빛 테두리광
    rng = random.Random(43003)  # 같은 시드 → 같은 붓 자국 (상태 전환 시 형태 유지)
    img = _hanji_panel(rng, 360, 110,
                       base_dark=mul(HANJI_BRIGHT, 0.72), base_light=mul(HANJI_BRIGHT, 0.92),
                       border_w=4, jitter=1.6, radius=8)
    w, h = img.size
    glow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.rounded_rectangle([2 * S, 2 * S, w - 2 * S, h - 2 * S], radius=10 * S,
                         outline=rgba(TALISMAN_PAPER, 150), width=3 * S)
    glow = glow.filter(ImageFilter.GaussianBlur(3 * S))
    img = Image.alpha_composite(glow, img)
    img = downscale(img, S)
    save(img, "UI/ui_button_hover.png")


# ---------------------------------------------------------------- E 프롬프트 키캡 (빈 판 — 글자는 TMP)
def gen_keycap():
    W = 72
    w = W * S * 2  # 작으니 4배
    img = canvas(w, w)
    d = ImageDraw.Draw(img, "RGBA")
    body = (46, 42, 38)
    top = (88, 82, 72)
    r = 9 * S * 2
    d.rounded_rectangle([0, 0, w - 1, w - 1], radius=r, fill=rgba(mul(body, 0.7)))
    d.rounded_rectangle([0, 0, w - 1, w - 5 * S], radius=r, fill=rgba(body))
    pad = 7 * S * 2
    d.rounded_rectangle([pad, pad * 0.8, w - pad, w - pad * 1.2], radius=r // 2, fill=rgba(top))
    hl, hld = overlay(img)
    hld.rounded_rectangle([pad, pad * 0.8, w - pad, w - pad * 1.2], radius=r // 2,
                          outline=rgba(mul(top, 1.35), 120), width=S)
    hld.rounded_rectangle([0, 0, w - 1, w - 1], radius=r, outline=rgba((14, 12, 10), 220), width=S * 2)
    img = merge(img, hl)
    img = downscale(img, S * 2)
    img = apply_grain(img, opacity=0.05, sigma=22)
    save(img, "UI/ui_key_prompt.png")


# ---------------------------------------------------------------- 볼륨 슬라이더
def gen_slider():
    # 트랙
    W, H = 400, 24
    w, h = W * S, H * S
    img = canvas(w, h)
    d = ImageDraw.Draw(img, "RGBA")
    r = h // 2
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=r, fill=rgba((28, 26, 32)))
    # 위 오목 그늘 / 아래 미광 / 외곽선 (반투명 → 오버레이)
    hl, hld = overlay(img)
    hld.rounded_rectangle([0, 0, w - 1, h - 1], radius=r, outline=rgba((10, 9, 12), 230), width=S)
    hld.line([r, 2 * S, w - r, 2 * S], fill=rgba((8, 8, 10), 160), width=S)
    hld.line([r, h - 2 * S, w - r, h - 2 * S], fill=rgba((96, 92, 104), 70), width=S)
    img = merge(img, hl)
    img = downscale(img, S)
    save(img, "UI/ui_slider_track.png")

    # 핸들 — 한지 원판 + 주사 점
    W = 36
    w = W * S * 2
    img = canvas(w, w)
    d = ImageDraw.Draw(img, "RGBA")
    pad = 2 * S * 2
    d.ellipse([pad, pad, w - pad, w - pad], fill=rgba(mul(HANJI_DIM, 0.95)))
    grad = radial_mask(w, w).point(lambda v: int(v * 0.35))
    dark = Image.new("RGBA", (w, w), rgba(mul(HANJI_DIM, 0.55)))
    img = Image.composite(dark, img, grad).convert("RGBA")
    d = ImageDraw.Draw(img, "RGBA")
    d.ellipse([pad, pad, w - pad, w - pad], outline=rgba(INK, 230), width=int(1.6 * S * 2))
    cx = w / 2
    d.ellipse([cx - 3.2 * S * 2, cx - 3.2 * S * 2, cx + 3.2 * S * 2, cx + 3.2 * S * 2],
              fill=rgba(TALISMAN_RED, 235))
    img = downscale(img, S * 2)
    img = apply_grain(img, opacity=0.05, sigma=22)
    save(img, "UI/ui_slider_handle.png")


# ---------------------------------------------------------------- 상태 부적 대형 + 연소 5단계 (160x480)
def _talisman_large(rng):
    """온전한 상태의 대형 부적 (2배 캔버스)."""
    W, H = 160, 480
    w, h = W * S, H * S
    img = canvas(w, h)

    poly = wobble_rect_poly(rng, [4 * S, 4 * S, w - 4 * S, h - 4 * S], amp=2.4 * S, step=16 * S)
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).polygon(poly, fill=255)
    paper = vgrad(w, h, mix(TALISMAN_PAPER, (238, 200, 116), 0.4), mul(TALISMAN_PAPER, 0.78)).convert("RGBA")
    paper.putalpha(mask)
    paper = apply_grain(paper, opacity=0.09, sigma=28, blur=1)
    img = Image.alpha_composite(img, paper)

    red = TALISMAN_RED
    marks = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    md = ImageDraw.Draw(marks)
    cx = w / 2
    lw = int(3.5 * S)
    # 테두리 문양 (안쪽 붉은 틀)
    md.rectangle([12 * S, 12 * S, w - 12 * S, h - 12 * S], outline=rgba(red, 210), width=int(1.8 * S))
    # 상단 — 해/눈 문양: 동심원 + 방사획
    md.ellipse([cx - 34 * S, 30 * S, cx + 34 * S, 98 * S], outline=rgba(red), width=lw)
    md.ellipse([cx - 16 * S, 48 * S, cx + 16 * S, 80 * S], fill=rgba(red, 220))
    for i in range(8):
        a = math.tau * i / 8
        x0 = cx + math.cos(a) * 38 * S
        y0 = 64 * S + math.sin(a) * 38 * S
        x1 = cx + math.cos(a) * 48 * S
        y1 = 64 * S + math.sin(a) * 48 * S
        md.line([(x0, y0), (x1, y1)], fill=rgba(red), width=int(2.2 * S))
    # 중단 — 강령 획: 세로 대획 + 번개 + 가지
    zig = [(cx, 120 * S)]
    for i, sx in enumerate((-14, 15, -12, 14, -10, 12)):
        zig.append((cx + sx * S, (150 + i * 38) * S))
    zig.append((cx, 396 * S))
    md.line(zig, fill=rgba(red), width=lw, joint="curve")
    for fy, fw in ((168, 26), (226, 30), (284, 26), (342, 22)):
        md.line([(cx - fw * S, (fy + 8) * S), (cx + fw * S, fy * S)], fill=rgba(red), width=int(2.4 * S))
    # 격자 인장 (중단 아래 작은 궁형 표식)
    md.arc([cx - 22 * S, 300 * S, cx + 22 * S, 344 * S], 200, 340, fill=rgba(red), width=int(2.2 * S))
    # 하단 — 삼지창 마무리
    md.line([(cx, 396 * S), (cx - 20 * S, 448 * S)], fill=rgba(red), width=int(2.8 * S))
    md.line([(cx, 396 * S), (cx, 452 * S)], fill=rgba(red), width=int(2.8 * S))
    md.line([(cx, 396 * S), (cx + 20 * S, 448 * S)], fill=rgba(red), width=int(2.8 * S))
    # 붉은 인주 도장 (우하단 사각)
    md.rectangle([cx + 22 * S, 412 * S, cx + 52 * S, 442 * S], fill=rgba(red, 190))
    # 먹 번짐
    marks = Image.alpha_composite(marks.filter(ImageFilter.GaussianBlur(S)), marks)
    clip_to(marks, mask)
    img = Image.alpha_composite(img, marks)
    return img, mask, w, h


def _burn_boundary(rng, w, frac, h):
    """연소 경계 y 좌표열 (아래에서 frac 만큼 소실). 불규칙 곡선."""
    base_y = h * (1.0 - frac)
    phases = [rng.uniform(0, math.tau) for _ in range(3)]
    amps = (14, 8, 5)
    ys = []
    for x in range(w + 1):
        t = x / w
        yy = base_y
        for k in range(3):
            yy += amps[k] * S * math.sin(math.tau * (k + 1) * 1.7 * t + phases[k])
        ys.append(yy)
    return ys


def gen_talisman_status():
    for idx, frac in enumerate(BURN_STAGES):
        rng = random.Random(43050)  # 부적 본체는 전 단계 동일 (같은 부적이 타는 것)
        img, mask, w, h = _talisman_large(rng)
        brng = random.Random(43060 + idx)  # 연소 경계는 단계별

        if frac > 0:
            ys = _burn_boundary(brng, w, frac, h)
            # 소실 마스크 (경계 아래 = 사라짐)
            burn_mask = Image.new("L", (w, h), 255)
            bd = ImageDraw.Draw(burn_mask)
            pts = [(x, ys[x]) for x in range(0, w + 1, 2)] + [(w, h), (0, h)]
            bd.polygon(pts, fill=0)
            img.putalpha(ImageChops.multiply(img.getchannel("A"), burn_mask))

            # 그을음 밴드 (경계 위 12~26px)
            char_layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
            cd = ImageDraw.Draw(char_layer)
            band1 = [(x, ys[x] - brng.uniform(8, 14) * S) for x in range(0, w + 1, 3)]
            cd.polygon(band1 + [(w, ys[w] + 2 * S), (w, h), (0, h), (0, ys[0] + 2 * S)], fill=rgba(CHAR, 245))
            band2 = [(x, ys[x] - brng.uniform(16, 26) * S) for x in range(0, w + 1, 3)]
            cd.polygon(band2 + [(w, h), (0, h)], fill=rgba(mix(CHAR, (80, 48, 28), 0.5), 120))
            char_layer = char_layer.filter(ImageFilter.GaussianBlur(int(1.5 * S)))
            clip_to(char_layer, img.getchannel("A").point(lambda v: 255 if v > 10 else 0))
            img = Image.alpha_composite(img, char_layer)

        img = downscale(img, S)
        # 잔불 픽셀 — 다운스케일 후 1배에서 찍어 또렷하게
        if frac > 0:
            w1, h1 = img.size
            el, d = overlay(img)
            for x in range(0, w1, 2):
                if brng.random() < 0.55:
                    y = ys[min(x * S, w)] / S + brng.uniform(-2, 1)
                    c = EMBER_HOT if brng.random() < 0.4 else EMBER
                    r = brng.choice((1, 1, 2))
                    d.ellipse([x - r / 2, y - r / 2, x + r / 2, y + r / 2],
                              fill=rgba(c, brng.randint(150, 245)))
            img = merge(img, el)
            # 잔불 은은한 글로우
            glow = Image.new("RGBA", (w1, h1), (0, 0, 0, 0))
            gd = ImageDraw.Draw(glow)
            for x in range(0, w1, 6):
                y = ys[min(x * S, w)] / S
                gd.ellipse([x - 4, y - 3, x + 4, y + 3], fill=rgba(EMBER, 34))
            glow = glow.filter(ImageFilter.GaussianBlur(3))
            img = Image.alpha_composite(img, glow)
        save(img, f"UI/ui_talisman_status_{idx}.png")


def run():
    print("[UI]")
    gen_dialogue_frame()
    gen_name_panel()
    gen_buttons()
    gen_keycap()
    gen_slider()
    gen_talisman_status()


if __name__ == "__main__":
    run()
