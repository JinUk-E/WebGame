# -*- coding: utf-8 -*-
"""Portraits — 프롤로그 대화용 상반신 4종 (256x384).

스타일: 어두운 실루엣 + 좌상단 단방향 림라이트 (청백 — TV광/여명 톤).
이목구비 없이 윤곽·복식으로 인물 구분:
  소년=까까머리+티셔츠 / 할아버지=성긴 머리+한복 조끼 / K씨=갓+무복 견장 / 할머니=쪽진 머리+저고리
"""
import math
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

from artgen_common import (
    NIGHT_DARK, NIGHT_LIGHT, TV_GLOW, apply_grain, canvas, clip_to,
    cubic_bezier, downscale, mix, mul, rgba, rim_from_mask, save, shift_mask,
    vgrad,
)

S = 4
W, H = 256, 384
w, h = W * S, H * S
CX = w / 2

SIL_TOP = (30, 34, 50)      # 실루엣 상부 (밤 남색 위)
SIL_BOT = (14, 16, 26)      # 실루엣 하부
DETAIL = (52, 58, 80)       # 내부 디테일 선 (은은한 명부)
RIM = TV_GLOW               # 림라이트 색


# ---------------------------------------------------------------- 공통 조립
def _assemble(mask, detail_layer, rng, rim_strength=1.0):
    """실루엣 마스크 + 디테일 → 최종 초상."""
    img = canvas(w, h)
    # 본체: 위→아래 어두운 그라디언트
    body = vgrad(w, h, SIL_TOP, SIL_BOT).convert("RGBA")
    body.putalpha(mask)
    img = Image.alpha_composite(img, body)

    # 내부 디테일 (복식 윤곽 — 은은하게)
    if detail_layer is not None:
        clip_to(detail_layer, mask)
        detail_layer = detail_layer.filter(ImageFilter.GaussianBlur(S // 2))
        img = Image.alpha_composite(img, detail_layer)

    # 림라이트 — 좌측 + 위쪽 약간 (광원: 좌상단)
    rim_l = rim_from_mask(mask, dx=int(5.5 * S), dy=0, blur=int(1.2 * S))
    rim_t = rim_from_mask(mask, dx=0, dy=int(3.5 * S), blur=int(1.2 * S))
    rim = ImageChops.lighter(rim_l, rim_t.point(lambda v: int(v * 0.6)))
    # 아래로 갈수록 감쇠
    fade = Image.linear_gradient("L").resize((w, h)).point(lambda v: 255 - int(v * 0.75))
    rim = ImageChops.multiply(rim, fade)
    rim = rim.point(lambda v: min(255, int(v * 1.35 * rim_strength)))
    rim_col = Image.new("RGBA", (w, h), rgba(mix(RIM, (235, 245, 250), 0.3)))
    rim_col.putalpha(rim)
    img = Image.alpha_composite(img, rim_col)
    # 림 안쪽으로 번지는 은은한 이차광
    rim2 = rim_from_mask(mask, dx=int(14 * S), dy=int(4 * S), blur=int(4 * S))
    rim2 = ImageChops.multiply(rim2, fade).point(lambda v: int(v * 0.25))
    rim2_col = Image.new("RGBA", (w, h), rgba(mul(RIM, 0.8)))
    rim2_col.putalpha(rim2)
    img = Image.alpha_composite(img, rim2_col)

    img = apply_grain(img, opacity=0.05, sigma=22)
    return downscale(img, S)


def _shoulders(md, top_y, half_w, neck_half, slope=0.55, sag=0.0):
    """어깨~하단 몸통 폴리곤 (베지어 어깨 곡선)."""
    left = cubic_bezier((CX - neck_half, top_y), (CX - neck_half - 30 * S, top_y + 10 * S * slope),
                        (CX - half_w, top_y + 60 * S * slope + sag), (CX - half_w, h))
    right = cubic_bezier((CX + neck_half, top_y), (CX + neck_half + 30 * S, top_y + 10 * S * slope),
                         (CX + half_w, top_y + 60 * S * slope + sag), (CX + half_w, h))
    md.polygon(left + [(CX - half_w, h)], fill=255)
    md.polygon(right + [(CX + half_w, h)], fill=255)
    md.polygon([(CX - neck_half, top_y), (CX + neck_half, top_y),
                (CX + half_w, h), (CX - half_w, h)], fill=255)
    # 위 폴리곤들 합집합으로 어깨 곡선 형성
    md.polygon(left + list(reversed(right)), fill=255)


# ---------------------------------------------------------------- 소년 (주인공)
def gen_boy():
    rng = random.Random(44001)
    mask = Image.new("L", (w, h), 0)
    md = ImageDraw.Draw(mask)
    # 머리 — 까까머리: 매끈한 구형 (위 살짝 평평)
    hr = 46 * S
    hcy = 120 * S
    md.ellipse([CX - hr, hcy - hr * 0.96, CX + hr, hcy + hr * 1.04], fill=255)
    # 귀
    for sx in (-1, 1):
        md.ellipse([CX + sx * hr - 7 * S, hcy - 4 * S, CX + sx * hr + 7 * S, hcy + 16 * S], fill=255)
    # 목
    md.rectangle([CX - 15 * S, hcy + hr * 0.7, CX + 15 * S, hcy + hr * 0.7 + 34 * S], fill=255)
    # 어깨 (좁고 둥글게 — 소년)
    _shoulders(md, hcy + hr * 0.7 + 26 * S, 86 * S, 16 * S, slope=0.8)

    det = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    dd = ImageDraw.Draw(det)
    # 티셔츠 크루넥 라인
    ny = hcy + hr * 0.7 + 40 * S
    dd.arc([CX - 26 * S, ny - 8 * S, CX + 26 * S, ny + 18 * S], 20, 160, fill=rgba(DETAIL, 150), width=2 * S)
    # 어깨 솔기
    dd.line([(CX - 60 * S, ny + 26 * S), (CX - 74 * S, h * 0.86)], fill=rgba(DETAIL, 90), width=S)
    dd.line([(CX + 60 * S, ny + 26 * S), (CX + 74 * S, h * 0.86)], fill=rgba(DETAIL, 90), width=S)

    save(_assemble(mask, det, rng, rim_strength=1.0), "Portraits/portrait_boy.png")


# ---------------------------------------------------------------- 할아버지
def gen_grandfather():
    rng = random.Random(44002)
    mask = Image.new("L", (w, h), 0)
    md = ImageDraw.Draw(mask)
    hcx = CX - 6 * S  # 살짝 숙인 자세
    hr = 44 * S
    hcy = 128 * S
    md.ellipse([hcx - hr, hcy - hr * 0.94, hcx + hr, hcy + hr * 1.08], fill=255)
    # 성긴 머리카락 — 위로 뻗친 가는 다발
    for i in range(9):
        a = math.pi * (0.18 + 0.64 * i / 8)
        x0 = hcx + math.cos(a + math.pi) * hr * 0.85
        y0 = hcy - math.sin(a) * hr * 0.92
        ln = rng.uniform(8, 16) * S
        x1 = x0 + math.cos(a + math.pi) * ln * 0.4 + rng.uniform(-3, 3) * S
        y1 = y0 - ln
        md.line([(x0, y0), (x1, y1)], fill=200, width=int(rng.uniform(1.2, 2.2) * S))
    # 귀 (크게)
    for sx in (-1, 1):
        md.ellipse([hcx + sx * hr - 8 * S, hcy - 2 * S, hcx + sx * hr + 8 * S, hcy + 22 * S], fill=255)
    # 목 (가늘고 앞으로)
    md.polygon([(hcx - 13 * S, hcy + hr * 0.75), (hcx + 15 * S, hcy + hr * 0.75),
                (CX + 17 * S, hcy + hr * 0.75 + 30 * S), (CX - 15 * S, hcy + hr * 0.75 + 30 * S)], fill=255)
    # 어깨 (넓지만 처짐)
    _shoulders(md, hcy + hr * 0.75 + 22 * S, 100 * S, 17 * S, slope=1.15, sag=8 * S)

    det = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    dd = ImageDraw.Draw(det)
    ny = hcy + hr * 0.75 + 34 * S
    # 한복 저고리 깃 (깊은 V) + 흰 동정
    dd.line([(CX - 34 * S, ny + 2 * S), (CX + 4 * S, ny + 66 * S)], fill=rgba((150, 148, 138), 170), width=int(2.6 * S))
    dd.line([(CX + 34 * S, ny + 2 * S), (CX + 4 * S, ny + 66 * S)], fill=rgba((150, 148, 138), 170), width=int(2.6 * S))
    dd.line([(CX - 38 * S, ny + 4 * S), (CX + 2 * S, ny + 72 * S)], fill=rgba(DETAIL, 120), width=int(1.6 * S))
    # 조끼(배자) 라인 — 어깨에서 내려오는 사선 + 단추
    dd.line([(CX - 62 * S, ny + 30 * S), (CX - 44 * S, h * 0.92)], fill=rgba(DETAIL, 110), width=int(1.8 * S))
    dd.line([(CX + 62 * S, ny + 30 * S), (CX + 44 * S, h * 0.92)], fill=rgba(DETAIL, 110), width=int(1.8 * S))
    dd.ellipse([CX + 2 * S, ny + 78 * S, CX + 8 * S, ny + 84 * S], fill=rgba((120, 110, 90), 190))

    save(_assemble(mask, det, rng, rim_strength=0.95), "Portraits/portrait_grandfather.png")


# ---------------------------------------------------------------- 무속인 K씨 (갓 + 무복 견장)
def gen_shaman():
    rng = random.Random(44003)
    mask = Image.new("L", (w, h), 0)
    md = ImageDraw.Draw(mask)
    hr = 40 * S
    hcy = 150 * S
    # 얼굴 (갓 아래)
    md.ellipse([CX - hr, hcy - hr * 0.8, CX + hr, hcy + hr * 1.05], fill=255)
    # 갓 — 모자통(원통) + 넓은 챙(타원)
    brim_y = hcy - hr * 0.55
    md.ellipse([CX - 78 * S, brim_y - 13 * S, CX + 78 * S, brim_y + 13 * S], fill=255)
    md.rounded_rectangle([CX - 34 * S, brim_y - 56 * S, CX + 34 * S, brim_y], radius=6 * S, fill=255)
    # 갓끈 (턱 아래로 두 줄)
    md.line([(CX - 30 * S, brim_y + 8 * S), (CX - 20 * S, hcy + hr * 1.3)], fill=230, width=int(1.6 * S))
    md.line([(CX + 30 * S, brim_y + 8 * S), (CX + 22 * S, hcy + hr * 1.3)], fill=230, width=int(1.6 * S))
    # 목
    md.rectangle([CX - 14 * S, hcy + hr * 0.7, CX + 14 * S, hcy + hr * 0.7 + 30 * S], fill=255)
    # 어깨 — 무복: 넓고 각진 + 견장 솟음
    top_y = hcy + hr * 0.7 + 22 * S
    _shoulders(md, top_y, 96 * S, 15 * S, slope=0.5)
    for sx in (-1, 1):
        # 견장 (어깨 끝 솟은 자락)
        md.polygon([(CX + sx * 60 * S, top_y + 26 * S),
                    (CX + sx * 100 * S, top_y + 2 * S),
                    (CX + sx * 104 * S, top_y + 34 * S),
                    (CX + sx * 70 * S, top_y + 52 * S)], fill=255)

    det = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    dd = ImageDraw.Draw(det)
    # 갓은 더 짙게 (얼굴보다 어두운 블록)
    dd.ellipse([CX - 78 * S, brim_y - 13 * S, CX + 78 * S, brim_y + 13 * S], fill=rgba((8, 10, 16), 150))
    dd.rounded_rectangle([CX - 34 * S, brim_y - 56 * S, CX + 34 * S, brim_y], radius=6 * S, fill=rgba((8, 10, 16), 150))
    ny = hcy + hr * 0.7 + 30 * S
    # 무복 깃 교차
    dd.line([(CX - 30 * S, ny), (CX + 10 * S, ny + 60 * S)], fill=rgba(DETAIL, 150), width=int(2.4 * S))
    dd.line([(CX + 30 * S, ny), (CX - 10 * S, ny + 60 * S)], fill=rgba(DETAIL, 150), width=int(2.4 * S))
    # 가슴의 붉은 띠 (무복 색동 — 아주 어둡게)
    dd.line([(CX - 80 * S, ny + 76 * S), (CX + 80 * S, ny + 70 * S)], fill=rgba((116, 48, 42), 150), width=int(5 * S))
    # 견장 윗선 명부
    dd.line([(CX - 98 * S, ny - 4 * S), (CX - 62 * S, ny + 20 * S)], fill=rgba(DETAIL, 130), width=int(1.6 * S))
    dd.line([(CX + 98 * S, ny - 4 * S), (CX + 62 * S, ny + 20 * S)], fill=rgba(DETAIL, 130), width=int(1.6 * S))

    save(_assemble(mask, det, rng, rim_strength=1.1), "Portraits/portrait_shaman.png")


# ---------------------------------------------------------------- 할머니 (쪽진 머리 + 저고리)
def gen_grandmother():
    rng = random.Random(44004)
    mask = Image.new("L", (w, h), 0)
    md = ImageDraw.Draw(mask)
    hr = 42 * S
    hcy = 126 * S
    # 머리 — 가르마 탄 매끈한 두상 (머리카락이 감싸 실루엣 약간 큼)
    md.ellipse([CX - hr * 1.06, hcy - hr * 1.0, CX + hr * 1.06, hcy + hr * 1.02], fill=255)
    # 쪽 (뒤통수 낮은 위치의 쪽머리 — 우측)
    md.ellipse([CX + hr * 0.75, hcy + hr * 0.28, CX + hr * 0.75 + 26 * S, hcy + hr * 0.28 + 24 * S], fill=255)
    # 귀
    md.ellipse([CX - hr - 6 * S, hcy + 2 * S, CX - hr + 8 * S, hcy + 22 * S], fill=255)
    # 목 (가늘게)
    md.rectangle([CX - 12 * S, hcy + hr * 0.72, CX + 12 * S, hcy + hr * 0.72 + 30 * S], fill=255)
    # 어깨 — 좁고 처진 (저고리 소매 둥근 선)
    _shoulders(md, hcy + hr * 0.72 + 22 * S, 80 * S, 14 * S, slope=1.3, sag=10 * S)

    det = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    dd = ImageDraw.Draw(det)
    # 가르마 (정수리 중앙선)
    dd.line([(CX, hcy - hr * 0.98), (CX, hcy - hr * 0.35)], fill=rgba((6, 8, 12), 170), width=int(1.4 * S))
    ny = hcy + hr * 0.72 + 30 * S
    # 저고리 깃 + 흰 동정 (V자)
    dd.line([(CX - 30 * S, ny + 2 * S), (CX + 6 * S, ny + 56 * S)], fill=rgba((160, 156, 146), 180), width=int(2.8 * S))
    dd.line([(CX + 30 * S, ny + 2 * S), (CX + 6 * S, ny + 56 * S)], fill=rgba((160, 156, 146), 180), width=int(2.8 * S))
    # 고름 두 가닥 (가슴에서 아래로)
    dd.line([(CX + 4 * S, ny + 58 * S), (CX - 10 * S, ny + 130 * S)], fill=rgba(DETAIL, 140), width=int(3 * S))
    dd.line([(CX + 10 * S, ny + 60 * S), (CX + 22 * S, ny + 124 * S)], fill=rgba(DETAIL, 110), width=int(2.2 * S))
    # 소매 배래선 (둥근 팔 윤곽)
    dd.arc([CX - 86 * S, ny + 40 * S, CX - 20 * S, h * 0.98], 250, 340, fill=rgba(DETAIL, 90), width=int(1.6 * S))
    dd.arc([CX + 20 * S, ny + 40 * S, CX + 86 * S, h * 0.98], 200, 290, fill=rgba(DETAIL, 90), width=int(1.6 * S))

    save(_assemble(mask, det, rng, rim_strength=0.9), "Portraits/portrait_grandmother.png")


def run():
    print("[Portraits]")
    gen_boy()
    gen_grandfather()
    gen_shaman()
    gen_grandmother()


if __name__ == "__main__":
    run()
