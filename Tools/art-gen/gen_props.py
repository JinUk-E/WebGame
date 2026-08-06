# -*- coding: utf-8 -*-
"""Props — 소금 더미·불상·TV·요강·이불·벽시계·벽 부적. 탑뷰(3/4), PPU 100.

크기 근거: architecture.md §3.4 소품 표 (유닛×100=px).
"""
import math
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

from artgen_common import (
    CHAR, HANJI_BRIGHT, HANJI_DIM, TALISMAN_PAPER, TALISMAN_RED, TV_GLOW,
    WOOD_DARK, WOOD_LIGHT, apply_grain, apply_streaks, blob_poly, canvas,
    clip_to, contact_shadow, downscale, merge, mix, mul, overlay,
    radial_mask, rgba, rim_from_mask, save, vgrad, wobble_rect_poly,
)

S = 4  # 슈퍼샘플 배율 (공통)

# 소금 4단계 팔레트: (기본, 밝은부, 그늘)
SALT_STAGES = {
    "white": ((221, 214, 198), (240, 234, 218), (172, 164, 146)),
    "gray": ((126, 122, 112), (156, 152, 140), (90, 86, 78)),
    "black": ((46, 42, 38), (74, 68, 60), (24, 21, 18)),
    "black_deep": ((40, 34, 32), (66, 58, 52), (20, 16, 15)),
}


# ---------------------------------------------------------------- 소금 더미 (0.6u → 60x52)
def gen_salt(stage, seed):
    rng = random.Random(seed)
    W, H = 60, 52
    w, h = W * S, H * S
    img = canvas(w, h)
    base, hi, sh = SALT_STAGES[stage]

    cx, cy = w / 2, h * 0.55
    rx, ry = w * 0.42, h * 0.34
    poly = blob_poly(rng, cx, cy, rx, ry, irregularity=0.14, n=32)
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).polygon(poly, fill=255)

    # 몸통: 위(빛)→아래(그늘) 그라디언트
    body = vgrad(w, h, hi, sh).convert("RGBA")
    body.putalpha(mask)
    # 꼭대기 하이라이트 (위쪽으로 치우친 타원)
    top = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    td = ImageDraw.Draw(top)
    td.ellipse([cx - rx * 0.55, cy - ry * 0.95, cx + rx * 0.55, cy - ry * 0.1],
               fill=rgba(mix(hi, (255, 255, 255), 0.25), 110))
    top = top.filter(ImageFilter.GaussianBlur(6 * S // 2))
    clip_to(top, mask)
    body = Image.alpha_composite(body, top)
    # 아랫단 그늘
    bot = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    bd = ImageDraw.Draw(bot)
    bd.ellipse([cx - rx, cy + ry * 0.15, cx + rx, cy + ry * 1.1], fill=rgba(sh, 130))
    bot = bot.filter(ImageFilter.GaussianBlur(5 * S // 2))
    clip_to(bot, mask)
    body = Image.alpha_composite(body, bot)

    # 알갱이 질감 (소금 입자 — 강한 고주파 노이즈)
    body = apply_grain(body, opacity=0.16, sigma=48)
    # 흩어진 낱알 몇 개
    gl, d = overlay(body)
    for _ in range(14):
        a = rng.uniform(0, math.tau)
        rr = rng.uniform(1.0, 1.25)
        px, py = cx + math.cos(a) * rx * rr, cy + math.sin(a) * ry * rr
        if 0 < px < w and 0 < py < h:
            g = rng.randint(1, 2) * S // 2
            d.ellipse([px - g, py - g, px + g, py + g], fill=rgba(base, rng.randint(120, 220)))
    body = merge(body, gl)

    # 심화 단계: 가장자리 붉은 균열
    if stage == "black_deep":
        crl, cr = overlay(body)
        for _ in range(7):
            a = rng.uniform(0, math.tau)
            px, py = cx + math.cos(a) * rx * 0.95, cy + math.sin(a) * ry * 0.95
            pts = [(px, py)]
            ca = a + math.pi + rng.uniform(-0.5, 0.5)
            ln = rng.uniform(rx * 0.35, rx * 0.7)
            steps = 4
            for sN in range(steps):
                ca += rng.uniform(-0.7, 0.7)
                px += math.cos(ca) * ln / steps
                py += math.sin(ca) * ln / steps * 0.75
                pts.append((px, py))
            wd = rng.choice((2, 3)) * S // 2
            cr.line(pts, fill=rgba(TALISMAN_RED, 235), width=wd, joint="curve")
            cr.line(pts, fill=rgba(mix(TALISMAN_RED, (255, 120, 90), 0.5), 130), width=max(1, wd // 2))
        clip_to(crl, mask)
        body = merge(body, crl)

    # 접지 그림자
    img = Image.alpha_composite(img, body)
    img = contact_shadow(img, [cx - rx * 1.05, cy + ry * 0.45, cx + rx * 1.05, cy + ry * 1.25],
                         alpha=60, blur=4 * S)
    img = downscale(img, S)
    save(img, f"Props/prop_salt_{stage}.png")


# ---------------------------------------------------------------- 불상 + 상 (0.8u → 80x112)
def gen_buddha():
    rng = random.Random(42010)
    W, H = 80, 112
    w, h = W * S, H * S
    img = canvas(w, h)
    d = ImageDraw.Draw(img, "RGBA")
    cx = w / 2

    # 상(좌대 탁자) — 3/4 시점: 윗면 + 앞면
    top_y0, top_y1 = h * 0.52, h * 0.80
    front_y1 = h * 0.94
    d.rounded_rectangle([w * 0.06, top_y0, w * 0.94, top_y1], radius=4 * S,
                        fill=rgba(mul(WOOD_LIGHT, 1.05)))
    d.rounded_rectangle([w * 0.06, top_y1 - 4 * S, w * 0.94, front_y1], radius=3 * S,
                        fill=rgba(mul(WOOD_DARK, 0.85)))
    d.line([w * 0.06, top_y1 - 2 * S, w * 0.94, top_y1 - 2 * S], fill=rgba(mul(WOOD_DARK, 0.55)), width=S)
    # 다리 그늘
    d.rectangle([w * 0.10, front_y1 - 2 * S, w * 0.90, front_y1], fill=rgba(mul(WOOD_DARK, 0.5)))

    # 불상 — 놋쇠 톤 (채도 낮은 황동)
    brass = (112, 96, 58)
    brass_hi = (150, 132, 84)
    brass_sh = (72, 60, 38)
    # 결가부좌 하단 (무릎 타원)
    knee_y = h * 0.60
    d.ellipse([cx - w * 0.30, knee_y - h * 0.07, cx + w * 0.30, knee_y + h * 0.07], fill=rgba(brass))
    # 몸통 (둥근 어깨)
    body_top = h * 0.30
    d.rounded_rectangle([cx - w * 0.22, body_top, cx + w * 0.22, knee_y + h * 0.02],
                        radius=9 * S, fill=rgba(brass))
    # 머리 + 육계
    head_r = w * 0.13
    head_cy = h * 0.22
    d.ellipse([cx - head_r, head_cy - head_r, cx + head_r, head_cy + head_r], fill=rgba(brass))
    d.ellipse([cx - head_r * 0.4, head_cy - head_r * 1.35, cx + head_r * 0.4, head_cy - head_r * 0.7],
              fill=rgba(brass))
    # 귀 (긴 귓불)
    for sx in (-1, 1):
        d.rounded_rectangle([cx + sx * head_r * 1.05 - 2 * S, head_cy - head_r * 0.2,
                             cx + sx * head_r * 1.05 + 2 * S, head_cy + head_r * 0.75],
                            radius=2 * S, fill=rgba(brass))

    # 위에서 오는 빛 — 머리·어깨 하이라이트
    lit = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ld = ImageDraw.Draw(lit)
    ld.ellipse([cx - head_r * 0.8, head_cy - head_r * 0.95, cx + head_r * 0.8, head_cy], fill=rgba(brass_hi, 160))
    ld.ellipse([cx - w * 0.20, body_top - 2 * S, cx + w * 0.20, body_top + 8 * S], fill=rgba(brass_hi, 110))
    ld.ellipse([cx - w * 0.28, knee_y - h * 0.06, cx + w * 0.28, knee_y - h * 0.015], fill=rgba(brass_hi, 60))
    lit = lit.filter(ImageFilter.GaussianBlur(3 * S))
    img = Image.alpha_composite(img, lit)
    d = ImageDraw.Draw(img, "RGBA")
    # 아래 그늘 (몸통 하단·무릎 밑)
    shd = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shd)
    sd.ellipse([cx - w * 0.30, knee_y + h * 0.02, cx + w * 0.30, knee_y + h * 0.085], fill=rgba(brass_sh, 150))
    sd.rectangle([cx - w * 0.22, h * 0.44, cx + w * 0.22, h * 0.52], fill=rgba(brass_sh, 80))
    shd = shd.filter(ImageFilter.GaussianBlur(2 * S))
    img = Image.alpha_composite(img, shd)
    # 합장한 손 (작은 밝은 타원)
    hl, hd = overlay(img)
    hd.ellipse([cx - 4 * S, h * 0.42, cx + 4 * S, h * 0.48], fill=rgba(brass_hi, 200))
    img = merge(img, hl)

    img = apply_grain(img, opacity=0.06, sigma=24)
    img = contact_shadow(img, [w * 0.08, h * 0.86, w * 0.92, h * 0.99], alpha=70, blur=5 * S)
    img = downscale(img, S)
    save(img, "Props/prop_buddha_altar.png")


# ---------------------------------------------------------------- TV (1.2x0.8u → 120x80, 2장)
def _tv_base():
    W, H = 120, 80
    w, h = W * S, H * S
    img = canvas(w, h)
    d = ImageDraw.Draw(img, "RGBA")
    body = (58, 50, 44)
    # 본체
    d.rounded_rectangle([0, 2 * S, w - 1, h - 5 * S], radius=5 * S, fill=rgba(body))
    d.rounded_rectangle([0, 2 * S, w - 1, h - 5 * S], radius=5 * S, outline=rgba(mul(body, 0.55)), width=S)
    hl, hld = overlay(img)
    hld.line([4 * S, 3 * S, w - 4 * S, 3 * S], fill=rgba(mul(body, 1.5), 140), width=S)
    img = Image.alpha_composite(img, hl)
    d = ImageDraw.Draw(img, "RGBA")
    # 다리
    for fx in (0.14, 0.86):
        d.rounded_rectangle([w * fx - 4 * S, h - 6 * S, w * fx + 4 * S, h - S], radius=2 * S,
                            fill=rgba(mul(body, 0.6)))
    # 오른쪽 컨트롤 패널
    px0 = w * 0.78
    d.rectangle([px0, 6 * S, w - 4 * S, h - 8 * S], fill=rgba(mul(body, 0.8)))
    for i, fy in enumerate((0.22, 0.42)):
        cy = h * fy + 4 * S
        r = 3.2 * S
        d.ellipse([px0 + 6 * S - r, cy - r, px0 + 6 * S + r, cy + r], fill=rgba(mul(body, 1.6)))
        d.ellipse([px0 + 6 * S - r * 0.4, cy - r * 0.4, px0 + 6 * S + r * 0.4, cy + r * 0.4],
                  fill=rgba(mul(body, 0.5)))
    for j in range(5):  # 스피커 그릴
        y = h * 0.62 + j * 3.2 * S
        d.line([px0 + 2.5 * S, y, w - 6.5 * S, y], fill=rgba(mul(body, 0.5)), width=S)
    screen_box = [5 * S, 6 * S, px0 - 3 * S, h - 9 * S]
    # 스크린 베젤
    d.rounded_rectangle([screen_box[0] - S, screen_box[1] - S, screen_box[2] + S, screen_box[3] + S],
                        radius=6 * S, fill=rgba(mul(body, 0.45)))
    return img, screen_box, w, h


def gen_tv():
    rng = random.Random(42020)
    # --- 소등 ---
    img, sb, w, h = _tv_base()
    d = ImageDraw.Draw(img, "RGBA")
    d.rounded_rectangle(sb, radius=5 * S, fill=rgba((24, 28, 32)))
    # 유리 반사 (사선 밴드)
    refl = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    rd = ImageDraw.Draw(refl)
    rd.polygon([(sb[0] + 6 * S, sb[1]), (sb[0] + 20 * S, sb[1]),
                (sb[0] + 2 * S, sb[3]), (sb[0], sb[3] - 6 * S)], fill=rgba((190, 205, 215), 26))
    refl = refl.filter(ImageFilter.GaussianBlur(2 * S))
    m = Image.new("L", (w, h), 0)
    ImageDraw.Draw(m).rounded_rectangle(sb, radius=5 * S, fill=255)
    clip_to(refl, m)
    img = Image.alpha_composite(img, refl)
    img = apply_grain(img, opacity=0.05, sigma=22)
    save(downscale(img, S), "Props/prop_tv_off.png")

    # --- 점등 ---
    img, sb, w, h = _tv_base()
    sw, sh = int(sb[2] - sb[0]), int(sb[3] - sb[1])
    # 화면: 중심 밝은 청백광 → 가장자리 어두운 청색
    scr = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    grad_m = radial_mask(sw, sh)  # 중심0 가장자리255
    bright = Image.new("RGB", (sw, sh), mix(TV_GLOW, (235, 246, 250), 0.55))
    dark = Image.new("RGB", (sw, sh), mul(TV_GLOW, 0.45))
    scr_rgb = Image.composite(dark, bright, grad_m)
    scr = scr_rgb.convert("RGBA")
    # 정적 노이즈 (지지직)
    scr = apply_grain(scr, opacity=0.22, sigma=64)
    # 주사선
    sd = ImageDraw.Draw(scr, "RGBA")
    for y in range(0, sh, int(1.6 * S)):
        sd.line([0, y, sw, y], fill=rgba((20, 40, 50), 46), width=S // 2)
    m = Image.new("L", (sw, sh), 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, sw - 1, sh - 1], radius=5 * S, fill=255)
    scr.putalpha(m)
    img.paste(scr, (int(sb[0]), int(sb[1])), scr)
    # 화면광 번짐 (본체로 새어나오는 글로우)
    glow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.rounded_rectangle([sb[0] - 4 * S, sb[1] - 4 * S, sb[2] + 4 * S, sb[3] + 4 * S],
                         radius=8 * S, fill=rgba(TV_GLOW, 70))
    glow = glow.filter(ImageFilter.GaussianBlur(5 * S))
    img = Image.alpha_composite(img, glow)
    img = apply_grain(img, opacity=0.05, sigma=22)
    save(downscale(img, S), "Props/prop_tv_on.png")


# ---------------------------------------------------------------- 요강 (0.5u → 50x56)
def gen_jar():
    rng = random.Random(42030)
    W, H = 50, 56
    w, h = W * S, H * S
    img = canvas(w, h)
    d = ImageDraw.Draw(img, "RGBA")
    cx = w / 2
    porcelain = (206, 199, 184)
    porcelain_hi = (232, 227, 214)
    porcelain_sh = (156, 148, 132)

    # 몸통 (아래로 좁아지는 단지)
    body_top, body_bot = h * 0.34, h * 0.90
    d.polygon([(w * 0.10, body_top + 6 * S), (w * 0.90, body_top + 6 * S),
               (w * 0.80, body_bot), (w * 0.20, body_bot)], fill=rgba(porcelain))
    d.ellipse([w * 0.10, body_top, w * 0.90, body_top + 12 * S], fill=rgba(porcelain))
    d.ellipse([w * 0.20, body_bot - 5 * S, w * 0.80, body_bot + 5 * S], fill=rgba(mul(porcelain, 0.92)))
    # 좌측광 셰이딩
    shade = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shade)
    sd.polygon([(cx + w * 0.05, body_top), (w * 0.92, body_top + 6 * S),
                (w * 0.82, body_bot + 4 * S), (cx + w * 0.10, body_bot + 4 * S)],
               fill=rgba(porcelain_sh, 110))
    sd.polygon([(w * 0.12, body_top + 4 * S), (w * 0.30, body_top + 2 * S),
                (w * 0.32, body_bot), (w * 0.22, body_bot - 2 * S)], fill=rgba(porcelain_hi, 130))
    shade = shade.filter(ImageFilter.GaussianBlur(2.5 * S))
    img = Image.alpha_composite(img, shade)
    d = ImageDraw.Draw(img, "RGBA")

    # 뚜껑 (윗면 타원 + 꼭지)
    lid_cy = h * 0.28
    d.ellipse([w * 0.14, lid_cy - 7 * S, w * 0.86, lid_cy + 7 * S], fill=rgba(mix(porcelain, porcelain_hi, 0.5)))
    d.ellipse([cx - 4 * S, lid_cy - 5.5 * S, cx + 4 * S, lid_cy + 1.5 * S], fill=rgba(porcelain_hi))
    # 반투명 라인류는 오버레이로
    hl, hld = overlay(img)
    hld.ellipse([w * 0.14, lid_cy - 7 * S, w * 0.86, lid_cy + 7 * S], outline=rgba(porcelain_sh, 160), width=S)
    hld.ellipse([cx - 4 * S, lid_cy - 5.5 * S, cx + 4 * S, lid_cy + 1.5 * S], outline=rgba(porcelain_sh, 140), width=S // 2)
    hld.ellipse([cx - 2.2 * S, lid_cy - 4.4 * S, cx - 0.2 * S, lid_cy - 2.4 * S], fill=rgba((255, 255, 250), 170))
    # 뚜껑-몸통 경계선
    hld.arc([w * 0.12, body_top - 2 * S, w * 0.88, body_top + 13 * S], 10, 170, fill=rgba(porcelain_sh, 150), width=S)
    # 청화 줄무늬 (은은한 남색 띠)
    hld.arc([w * 0.14, body_bot - 22 * S, w * 0.86, body_bot + 2 * S], 195, 345, fill=rgba((70, 84, 120), 90), width=int(1.5 * S))
    img = merge(img, hl)

    img = apply_grain(img, opacity=0.05, sigma=20)
    img = contact_shadow(img, [w * 0.12, body_bot - 4 * S, w * 0.88, body_bot + 7 * S], alpha=65, blur=4 * S)
    img = downscale(img, S)
    save(img, "Props/prop_jar.png")


# ---------------------------------------------------------------- 이불 (2.0x1.4u → 200x140, 2장)
def _blanket_base(rng):
    W, H = 200, 140
    w, h = W * S, H * S
    img = canvas(w, h)
    d = ImageDraw.Draw(img, "RGBA")
    fabric = (104, 56, 50)       # 채도 낮춘 자주(이불감)
    fabric_hi = (128, 74, 66)
    border = mul(HANJI_DIM, 0.95)  # 홑청(아이보리 테)

    # 몸체 — 가장자리 살짝 우글거리는 사각
    outer = wobble_rect_poly(rng, [3 * S, 3 * S, w - 3 * S, h - 3 * S], amp=1.6 * S, step=14 * S)
    d.polygon(outer, fill=rgba(border))
    inner = wobble_rect_poly(rng, [12 * S, 12 * S, w - 12 * S, h - 12 * S], amp=1.4 * S, step=14 * S)
    d.polygon(inner, fill=rgba(fabric))

    # 원단 바탕 그라디언트 (위 밝게)
    grad = vgrad(w, h, fabric_hi, mul(fabric, 0.85)).convert("RGBA")
    m = Image.new("L", (w, h), 0)
    ImageDraw.Draw(m).polygon(inner, fill=255)
    grad.putalpha(m.point(lambda v: v * 60 // 255))
    img = Image.alpha_composite(img, grad)
    d = ImageDraw.Draw(img, "RGBA")

    # 누빔 대각 격자
    q = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    qd = ImageDraw.Draw(q)
    sp = 24 * S
    for k in range(-h, w + h, sp):
        qd.line([(k, 0), (k + h, h)], fill=rgba(mul(fabric, 0.7), 60), width=S)
        qd.line([(k + h, 0), (k, h)], fill=rgba(mul(fabric, 0.7), 60), width=S)
    clip_to(q, m)
    img = Image.alpha_composite(img, q)

    # 홑청 경계선
    hl, hld = overlay(img)
    hld.polygon(inner, outline=rgba(mul(fabric, 0.55), 200))
    img = merge(img, hl)
    return img, m, w, h, fabric, fabric_hi


def gen_blanket():
    rng = random.Random(42040)
    # --- 펼친 상태 ---
    img, m, w, h, fabric, fabric_hi = _blanket_base(rng)
    # 잔주름 (은은한 밝은 줄 + 그늘 줄)
    fold = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    fd = ImageDraw.Draw(fold)
    for _ in range(5):
        x = rng.uniform(w * 0.2, w * 0.8)
        bend = rng.uniform(-10 * S, 10 * S)
        fd.line([(x, 8 * S), (x + bend, h - 8 * S)], fill=rgba(fabric_hi, 40), width=3 * S)
        fd.line([(x + 2 * S, 8 * S), (x + bend + 2 * S, h - 8 * S)], fill=rgba(mul(fabric, 0.6), 36), width=2 * S)
    fold = fold.filter(ImageFilter.GaussianBlur(2 * S))
    clip_to(fold, m)
    img = Image.alpha_composite(img, fold)
    img = apply_grain(img, opacity=0.07, sigma=26)
    save(downscale(img, S), "Props/prop_blanket_flat.png")

    # --- 사람 들어간 볼록 상태 ---
    rng = random.Random(42041)
    img, m, w, h, fabric, fabric_hi = _blanket_base(rng)
    # 중앙 봉긋한 융기 — 상단광 하이라이트 + 우하단 그늘 초승달
    bump = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    bd = ImageDraw.Draw(bump)
    bcx, bcy = w * 0.46, h * 0.5
    brx, bry = w * 0.26, h * 0.30
    bd.ellipse([bcx - brx, bcy - bry, bcx + brx, bcy + bry], fill=rgba(fabric_hi, 120))
    bd.ellipse([bcx - brx * 0.55, bcy - bry * 0.75, bcx + brx * 0.35, bcy - bry * 0.05],
               fill=rgba(mix(fabric_hi, (200, 160, 140), 0.4), 120))
    bump = bump.filter(ImageFilter.GaussianBlur(6 * S))
    clip_to(bump, m)
    img = Image.alpha_composite(img, bump)
    # 그늘 초승달 (융기 우하단)
    cres = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    cd = ImageDraw.Draw(cres)
    cd.ellipse([bcx - brx * 0.9 + 8 * S, bcy - bry * 0.9 + 10 * S,
                bcx + brx * 1.15, bcy + bry * 1.2], fill=rgba(mul(fabric, 0.5), 110))
    cd.ellipse([bcx - brx * 0.95, bcy - bry * 1.0, bcx + brx * 0.9, bcy + bry * 0.85],
               fill=rgba((0, 0, 0), 0))
    cres = cres.filter(ImageFilter.GaussianBlur(7 * S))
    clip_to(cres, m)
    img = Image.alpha_composite(img, cres)
    # 융기 주변 당겨진 주름 (방사형)
    fold = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    fd = ImageDraw.Draw(fold)
    for a_deg in (200, 235, 305, 340, 25, 155):
        a = math.radians(a_deg)
        x0 = bcx + math.cos(a) * brx * 0.9
        y0 = bcy + math.sin(a) * bry * 0.9
        x1 = bcx + math.cos(a) * brx * 1.6
        y1 = bcy + math.sin(a) * bry * 1.7
        fd.line([(x0, y0), (x1, y1)], fill=rgba(mul(fabric, 0.62), 70), width=2 * S)
    fold = fold.filter(ImageFilter.GaussianBlur(2 * S))
    clip_to(fold, m)
    img = Image.alpha_composite(img, fold)
    img = apply_grain(img, opacity=0.07, sigma=26)
    save(downscale(img, S), "Props/prop_blanket_bulge.png")


# ---------------------------------------------------------------- 벽시계 (0.8u → 80x80 + 바늘 2장)
def gen_clock():
    W = 80
    w = W * S
    # --- 문자판 ---
    img = canvas(w, w)
    d = ImageDraw.Draw(img, "RGBA")
    cx = w / 2
    ring_w = 7 * S
    d.ellipse([0, 0, w - 1, w - 1], fill=rgba(mul(WOOD_DARK, 0.9)))
    d.ellipse([0, 0, w - 1, w - 1], outline=rgba(mul(WOOD_DARK, 0.5)), width=S)
    hl, hld = overlay(img)
    hld.arc([S, S, w - S, w - S], 200, 340, fill=rgba(mul(WOOD_LIGHT, 1.25), 150), width=S)
    img = merge(img, hl)
    d = ImageDraw.Draw(img, "RGBA")
    face_col = (198, 188, 162)  # 오래된 상아빛 문자판
    d.ellipse([ring_w, ring_w, w - ring_w, w - ring_w], fill=rgba(face_col))
    # 문자판 내부 음영 (아래쪽 은은한 그늘)
    inn = Image.new("RGBA", (w, w), (0, 0, 0, 0))
    idr = ImageDraw.Draw(inn)
    idr.ellipse([ring_w + 2 * S, w * 0.5, w - ring_w - 2 * S, w - ring_w + 2 * S],
                fill=rgba(mul(face_col, 0.8), 90))
    inn = inn.filter(ImageFilter.GaussianBlur(4 * S))
    mface = Image.new("L", (w, w), 0)
    ImageDraw.Draw(mface).ellipse([ring_w, ring_w, w - ring_w, w - ring_w], fill=255)
    clip_to(inn, mface)
    img = Image.alpha_composite(img, inn)
    d = ImageDraw.Draw(img, "RGBA")
    # 눈금 (12개 — 3·6·9·12 굵게)
    r_out = w / 2 - ring_w - 2 * S
    tk, tkd = overlay(img)
    for i in range(12):
        a = math.tau * i / 12
        major = i % 3 == 0
        r_in = r_out - (5 * S if major else 3 * S)
        x0, y0 = cx + math.sin(a) * r_in, cx - math.cos(a) * r_in
        x1, y1 = cx + math.sin(a) * r_out, cx - math.cos(a) * r_out
        tkd.line([(x0, y0), (x1, y1)], fill=rgba((52, 44, 36), 230), width=(2 * S if major else S))
    img = merge(img, tk)
    d = ImageDraw.Draw(img, "RGBA")
    # 중심 축 구멍
    d.ellipse([cx - 2.5 * S, cx - 2.5 * S, cx + 2.5 * S, cx + 2.5 * S], fill=rgba((52, 44, 36)))
    img = apply_grain(img, opacity=0.05, sigma=20)
    save(downscale(img, S), "Props/prop_clock_face.png")

    # --- 바늘 (80x80 캔버스, 축=캔버스 중심, 12시 방향 — 코드에서 Z회전) ---
    def hand(length, base_w, tip_w, name):
        him = canvas(w, w)
        hd = ImageDraw.Draw(him, "RGBA")
        col = (34, 30, 26)
        # v0.6 — 밑동을 **정확히 축 위**에서 시작한다 (꼬리 0). 축 아래로 조금이라도 남으면
        # 회전할 때 그 부분이 반대편으로 흔들려 "중간을 축으로 돈다"로 읽힌다.
        # 축 캡(허브 원)은 남긴다 — 회전 중심이 어디인지 눈으로 짚어주는 역할이라 오히려 필요하다.
        # ⚠ 스프라이트 피벗은 잘린 rect의 중심이 아니라 **캔버스 중심(=이 축)**이어야 한다.
        #    메타 rect·피벗은 Tools/art-gen/fix_clock_pivot.py가 알파 경계에서 다시 계산해 박는다.
        hd.polygon([(cx - base_w / 2, cx), (cx + base_w / 2, cx),
                    (cx + tip_w / 2, cx - length), (cx - tip_w / 2, cx - length)], fill=rgba(col))
        hd.ellipse([cx - 3.5 * S, cx - 3.5 * S, cx + 3.5 * S, cx + 3.5 * S], fill=rgba(col))
        hl, hld = overlay(him)
        hld.line([(cx - base_w / 2 + S, cx - S), (cx - tip_w / 2 + S, cx - length + 2 * S)],
                 fill=rgba((96, 88, 76), 140), width=S // 2)
        him = merge(him, hl)
        him = downscale(him, S)
        save(him, f"Props/prop_clock_hand_{name}.png")

    hand(length=17 * S, base_w=5 * S, tip_w=2.6 * S, name="hour")
    hand(length=26 * S, base_w=3.6 * S, tip_w=1.6 * S, name="minute")


# ---------------------------------------------------------------- 벽 부적 소형 (32x96)
def gen_talisman_wall():
    rng = random.Random(42060)
    W, H = 32, 96
    w, h = W * S, H * S
    img = canvas(w, h)
    d = ImageDraw.Draw(img, "RGBA")

    # 황지 — 찢긴 가장자리
    poly = wobble_rect_poly(rng, [1.5 * S, 1.5 * S, w - 1.5 * S, h - 1.5 * S], amp=1.2 * S, step=7 * S)
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).polygon(poly, fill=255)
    paper = vgrad(w, h, mix(TALISMAN_PAPER, (235, 196, 110), 0.35), mul(TALISMAN_PAPER, 0.8)).convert("RGBA")
    paper.putalpha(mask)
    img = Image.alpha_composite(img, paper)

    # 주사 문양 — 상단 원+획, 중단 지그재그 획, 하단 삼지창 획
    red = TALISMAN_RED
    marks = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    md = ImageDraw.Draw(marks)
    cx = w / 2
    lw = int(2.2 * S)
    md.ellipse([cx - 8 * S, 6 * S, cx + 8 * S, 22 * S], outline=rgba(red), width=lw)
    md.line([(cx, 9 * S), (cx, 19 * S)], fill=rgba(red), width=lw)
    md.line([(cx - 5 * S, 14 * S), (cx + 5 * S, 14 * S)], fill=rgba(red), width=lw)
    # 세로 중심 획 + 번개 지그재그
    zig = [(cx, 26 * S)]
    xs = (-5, 5, -4, 5, -3)
    for i, sx in enumerate(xs):
        zig.append((cx + sx * S, (34 + i * 9) * S))
    zig.append((cx, 78 * S))
    md.line(zig, fill=rgba(red), width=lw, joint="curve")
    # 가지 획
    md.line([(cx - 7 * S, 42 * S), (cx + 7 * S, 38 * S)], fill=rgba(red), width=int(1.6 * S))
    md.line([(cx - 7 * S, 58 * S), (cx + 7 * S, 54 * S)], fill=rgba(red), width=int(1.6 * S))
    # 하단 삼지창
    md.line([(cx, 78 * S), (cx - 6 * S, 90 * S)], fill=rgba(red), width=int(1.8 * S))
    md.line([(cx, 78 * S), (cx, 91 * S)], fill=rgba(red), width=int(1.8 * S))
    md.line([(cx, 78 * S), (cx + 6 * S, 90 * S)], fill=rgba(red), width=int(1.8 * S))
    # 먹이 살짝 번진 느낌
    marks = Image.alpha_composite(marks.filter(ImageFilter.GaussianBlur(S // 2)), marks)
    clip_to(marks, mask)
    img = Image.alpha_composite(img, marks)

    img = apply_grain(img, opacity=0.09, sigma=30)
    img = downscale(img, S)
    save(img, "Props/prop_talisman_wall.png")


# ---------------------------------------------------------------- 플레이어 소년 (0.7×0.9u → 70x90, 탑뷰)
# 어두운 티셔츠 톤 — 밤 남색(NIGHT_LIGHT)보다 반 톤 밝게, 장판(한지)과 명도 분리
PLAYER_TEE = (56, 60, 76)
PLAYER_TEE_HI = (80, 86, 106)
PLAYER_TEE_SH = (36, 39, 50)
PLAYER_HAIR = (46, 38, 31)        # 까까머리 — 짧은 모발 아래 두피가 비치는 갈흑
PLAYER_HAIR_HI = (96, 80, 63)
PLAYER_SKIN = (186, 158, 126)
PLAYER_PANTS = (34, 34, 42)


def gen_player():
    """탑뷰(정수리+어깨) 소년. PlayerController는 이동 방향 회전을 하지 않으므로
    방향 중립(좌우 대칭·정수리 중심) 1장으로 제작 — 회전 도입 시에도 이 1장을 Z회전만 하면 됨."""
    rng = random.Random(42060)
    W, H = 70, 90
    w, h = W * S, H * S
    img = canvas(w, h)
    cx = w / 2

    # --- 발끝 힌트 (어깨 밑으로 살짝 — 좌우 대칭) ---
    feet, fd = overlay(img)
    for sx in (-1, 1):
        fx = cx + sx * 9 * S
        fd.ellipse([fx - 6 * S, h * 0.86 - 4 * S, fx + 6 * S, h * 0.86 + 5 * S],
                   fill=rgba(PLAYER_PANTS, 255))
    img = merge(img, feet)

    # --- 어깨·등 (위에서 본 티셔츠) ---
    t_cy, t_rx, t_ry = h * 0.58, w * 0.43, h * 0.27
    t_poly = blob_poly(rng, cx, t_cy, t_rx, t_ry, irregularity=0.05, n=32)
    t_mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(t_mask).polygon(t_poly, fill=255)
    torso = vgrad(w, h, PLAYER_TEE_HI, PLAYER_TEE_SH).convert("RGBA")
    torso.putalpha(t_mask)
    # 등 중앙 능선광 (위에서 조명 — 척추 라인 살짝 밝게)
    ridge = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(ridge).ellipse(
        [cx - t_rx * 0.30, t_cy - t_ry * 0.55, cx + t_rx * 0.30, t_cy + t_ry * 0.75],
        fill=rgba(mix(PLAYER_TEE_HI, (255, 255, 255), 0.12), 90))
    ridge = ridge.filter(ImageFilter.GaussianBlur(4 * S))
    clip_to(ridge, t_mask)
    torso = Image.alpha_composite(torso, ridge)
    # 티셔츠 주름 — 어깨선 따라 얕은 그늘 줄 (좌우 대칭 쌍)
    folds, fo = overlay(torso)
    for sx in (-1, 1):
        fo.arc([cx + sx * t_rx * 0.15 - t_rx * 0.55, t_cy - t_ry * 0.9,
                cx + sx * t_rx * 0.15 + t_rx * 0.55, t_cy + t_ry * 0.5],
               200 if sx < 0 else 280, 340 if sx < 0 else 60,
               fill=rgba(mul(PLAYER_TEE, 0.62), 70), width=2 * S)
    folds.paste(folds.filter(ImageFilter.GaussianBlur(S)), (0, 0))
    clip_to(folds, t_mask)
    torso = Image.alpha_composite(torso, folds)
    img = Image.alpha_composite(img, torso)

    # --- 머리 (정수리 — 까까머리) ---
    h_cy, h_r = h * 0.33, w * 0.285
    head_mask = Image.new("L", (w, h), 0)
    hd = ImageDraw.Draw(head_mask)
    hd.ellipse([cx - h_r, h_cy - h_r, cx + h_r, h_cy + h_r], fill=255)
    # 귀 — 좌우 대칭 (소년 실루엣 읽힘 포인트)
    for sx in (-1, 1):
        hd.ellipse([cx + sx * h_r - 4 * S, h_cy - 5 * S, cx + sx * h_r + 4 * S, h_cy + 7 * S], fill=255)
    head = vgrad(w, h, mix(PLAYER_HAIR, PLAYER_HAIR_HI, 0.35), mul(PLAYER_HAIR, 0.8)).convert("RGBA")
    head.putalpha(head_mask)
    # 귀만 피부톤 덮기
    ears, ed = overlay(head)
    for sx in (-1, 1):
        ed.ellipse([cx + sx * h_r - 3 * S, h_cy - 4 * S, cx + sx * h_r + 3 * S, h_cy + 6 * S],
                   fill=rgba(mul(PLAYER_SKIN, 0.82), 235))
    head = merge(head, ears, blur=S // 2)
    # 정수리 하이라이트 (두피 비침 — 중앙 위 약간 좌측)
    crown = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(crown).ellipse(
        [cx - h_r * 0.55 - 3 * S, h_cy - h_r * 0.62 - 3 * S, cx + h_r * 0.35 - 3 * S, h_cy + h_r * 0.05],
        fill=rgba(mix(PLAYER_HAIR_HI, PLAYER_SKIN, 0.35), 105))
    crown = crown.filter(ImageFilter.GaussianBlur(3 * S))
    clip_to(crown, head_mask)
    head = Image.alpha_composite(head, crown)
    # 가마 — 정수리 소용돌이 점
    whorl, wd = overlay(head)
    wd.ellipse([cx + 2 * S - 2 * S, h_cy - h_r * 0.30 - 2 * S, cx + 2 * S + 2 * S, h_cy - h_r * 0.30 + 2 * S],
               fill=rgba(mul(PLAYER_HAIR, 0.65), 150))
    head = merge(head, whorl, blur=S // 2)
    # 머리가 어깨 위에 얹힘 + 머리 밑 접촉 그늘
    neck_sh = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(neck_sh).ellipse(
        [cx - h_r * 1.05, h_cy - h_r * 0.2, cx + h_r * 1.05, h_cy + h_r * 1.35],
        fill=rgba((0, 0, 0), 80))
    neck_sh = neck_sh.filter(ImageFilter.GaussianBlur(4 * S))
    clip_to(neck_sh, t_mask)
    img = Image.alpha_composite(img, neck_sh)
    img = Image.alpha_composite(img, head)

    # --- 미세 림라이트 (좌상단 청백 — 초상·소품과 동일 방향) ---
    sil = ImageChops.lighter(t_mask, head_mask)
    rim = rim_from_mask(sil, dx=2 * S, dy=3 * S, blur=2 * S)
    rim_layer = Image.new("RGBA", (w, h), rgba(mix(TV_GLOW, (255, 255, 255), 0.25), 0))
    rim_layer.putalpha(rim.point(lambda v: v * 68 // 255))
    img = Image.alpha_composite(img, rim_layer)

    # --- 접지 그림자 → 질감 → 저장 ---
    img = contact_shadow(img, [cx - t_rx * 0.95, t_cy + t_ry * 0.30, cx + t_rx * 0.95, h * 0.95],
                         alpha=55, blur=4 * S)
    img = apply_grain(img, opacity=0.07, sigma=26)
    save(downscale(img, S), "Props/player_boy.png")


def run():
    print("[Props]")
    gen_salt("white", 42001)
    gen_salt("gray", 42002)
    gen_salt("black", 42003)
    gen_salt("black_deep", 42004)
    gen_buddha()
    gen_tv()
    gen_jar()
    gen_blanket()
    gen_clock()
    gen_talisman_wall()
    gen_player()


if __name__ == "__main__":
    run()
