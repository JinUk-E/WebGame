# -*- coding: utf-8 -*-
"""플레이어 4방향 × 3프레임 (v0.6).

기존 player_boy.png 1장을 Z회전시키던 방식은 위에서 누른 벌레처럼 보였다 —
탑뷰 스프라이트를 화면 안에서 돌리면 사람이 걷는 게 아니라 물체가 회전한다.
그래서 방향별 그림을 따로 만들고 이동 중에는 프레임을 갈아끼운다.

산출물 (Props/):
  player_boy_up_0..2    등 (화면 위로 이동)
  player_boy_down_0..2  정면 (화면 아래로 이동)
  player_boy_side_0..2  옆 (오른쪽 기준 — 왼쪽은 런타임 flipX)

프레임 규약: 0 = 정지, 1·2 = 걷기 (발이 번갈아 나간다).
크기·PPU·팔레트·림라이트 방향은 gen_props.gen_player와 동일하게 유지한다.
"""
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

from artgen_common import (
    apply_grain, blob_poly, canvas, clip_to, contact_shadow, downscale,
    merge, mix, mul, overlay, rgba, rim_from_mask, save, vgrad,
)

S = 4
W, H = 70, 90

PLAYER_TEE = (56, 60, 76)
PLAYER_TEE_HI = (80, 86, 106)
PLAYER_TEE_SH = (36, 39, 50)
PLAYER_HAIR = (46, 38, 31)
PLAYER_HAIR_HI = (96, 80, 63)
PLAYER_SKIN = (186, 158, 126)
PLAYER_PANTS = (34, 34, 42)
TV_GLOW = (150, 190, 210)

# 걷기 프레임별 (앞발 오프셋, 뒷발 오프셋, 몸통 들림) — 단위는 S 적용 전 px.
# 70x90 스프라이트가 화면에서 작게 보이므로 보폭을 과장한다 — 작게 주면 걷는지 미끄러지는지 구분이 안 된다.
GAITS = {
    0: ((0, 0), (0, 0), 0.0),
    1: ((-6, -3), (6, 3), -1.6),
    2: ((6, 3), (-6, -3), -1.6),
}


def _feet(img, cx, h, frame, side):
    """발 — 정면·후면은 좌우로, 옆모습은 앞뒤로 엇갈린다."""
    (ax, ay), (bx, by), _ = GAITS[frame]
    feet, fd = overlay(img)
    if side:
        # 옆모습: 한 발이 앞(오른쪽)으로, 다른 발이 뒤로. 크기·명도 차로 원근을 준다
        for (ox, oy, scale, shade) in ((ax, ay, 1.0, 1.0), (bx, by, 0.86, 0.66)):
            fx = cx + (3 + ox) * S
            fy = h * 0.90 + oy * S * 0.4
            rx, ry = 7 * S * scale, 4.6 * S * scale
            fd.ellipse([fx - rx, fy - ry, fx + rx, fy + ry], fill=rgba(mul(PLAYER_PANTS, shade), 255))
    else:
        # 정면·후면: 발이 좌우로 벌어지며 번갈아 앞으로 나온다 (몸통 아래로 확실히 삐져나오게)
        for sx, (ox, oy) in ((-1, (ax, ay)), (1, (bx, by))):
            fx = cx + sx * 9.5 * S + ox * S * 0.30
            fy = h * 0.90 + oy * S * 0.55
            scale = 1.0 + oy * 0.03
            fd.ellipse([fx - 6.2 * S * scale, fy - 4.4 * S * scale,
                        fx + 6.2 * S * scale, fy + 5 * S * scale], fill=rgba(PLAYER_PANTS, 255))
    return merge(img, feet)


def _torso(img, rng, cx, h, w, frame, side):
    _, _, lift = GAITS[frame]
    t_cy = h * 0.58 + lift * S
    t_rx = w * (0.33 if side else 0.43)
    t_ry = h * 0.27
    poly = blob_poly(rng, cx, t_cy, t_rx, t_ry, irregularity=0.05, n=32)
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).polygon(poly, fill=255)
    torso = vgrad(w, h, PLAYER_TEE_HI, PLAYER_TEE_SH).convert("RGBA")
    torso.putalpha(mask)

    ridge = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(ridge).ellipse(
        [cx - t_rx * 0.30, t_cy - t_ry * 0.55, cx + t_rx * 0.30, t_cy + t_ry * 0.75],
        fill=rgba(mix(PLAYER_TEE_HI, (255, 255, 255), 0.12), 90))
    ridge = ridge.filter(ImageFilter.GaussianBlur(4 * S))
    clip_to(ridge, mask)
    torso = Image.alpha_composite(torso, ridge)

    # 팔 — 옆모습에서만 앞으로 흔들리는 팔 하나가 보인다 (걷는 인상의 8할)
    if side:
        arm, ad = overlay(torso)
        swing = GAITS[frame][0][0] * 0.45
        ax_ = cx + (t_rx / S * 0.72 + swing) * S
        ay_ = t_cy - t_ry * 0.05
        ad.ellipse([ax_ - 3.2 * S, ay_ - 4.6 * S, ax_ + 3.2 * S, ay_ + 5.4 * S],
                   fill=rgba(mul(PLAYER_TEE, 0.72), 255))
        torso = merge(torso, arm, blur=S // 2)
    return Image.alpha_composite(img, torso), mask, t_cy, t_rx, t_ry


def _head(img, w, h, cx, t_cy, frame, facing):
    """facing: 'up' | 'down' | 'side'"""
    _, _, lift = GAITS[frame]
    h_cy = h * 0.33 + lift * S * 0.6
    h_r = w * 0.285
    mask = Image.new("L", (w, h), 0)
    hd = ImageDraw.Draw(mask)
    hd.ellipse([cx - h_r, h_cy - h_r, cx + h_r, h_cy + h_r], fill=255)
    if facing == "side":
        # 옆머리 — 진행 방향으로 살짝 쏠린 타원 + 뒤쪽에 귀 하나
        hd.ellipse([cx - h_r * 0.80, h_cy - h_r * 0.92, cx + h_r * 1.02, h_cy + h_r * 0.94], fill=255)
        hd.ellipse([cx - h_r * 0.30 - 3.5 * S, h_cy - 2 * S, cx - h_r * 0.30 + 3.5 * S, h_cy + 8 * S], fill=255)
    else:
        for sx in (-1, 1):
            hd.ellipse([cx + sx * h_r - 4 * S, h_cy - 5 * S, cx + sx * h_r + 4 * S, h_cy + 7 * S], fill=255)

    head = vgrad(w, h, mix(PLAYER_HAIR, PLAYER_HAIR_HI, 0.35), mul(PLAYER_HAIR, 0.8)).convert("RGBA")
    head.putalpha(mask)

    # 귀 (피부톤)
    ears, ed = overlay(head)
    if facing == "side":
        ed.ellipse([cx - h_r * 0.35 - 3 * S, h_cy - 3 * S, cx - h_r * 0.35 + 3 * S, h_cy + 7 * S],
                   fill=rgba(mul(PLAYER_SKIN, 0.82), 235))
    else:
        for sx in (-1, 1):
            ed.ellipse([cx + sx * h_r - 3 * S, h_cy - 4 * S, cx + sx * h_r + 3 * S, h_cy + 6 * S],
                       fill=rgba(mul(PLAYER_SKIN, 0.82), 235))
    head = merge(head, ears, blur=S // 2)

    face, fd = overlay(head)
    if facing == "down":
        # 정면 — 얼굴은 머리 아래쪽 절반에만. 넓게 잡으면 주둥이처럼 읽힌다.
        # 위를 앞머리로 덮어 이마 경계를 만들고, 눈은 넓게·높게 둔다.
        fd.ellipse([cx - h_r * 0.58, h_cy + h_r * 0.06, cx + h_r * 0.58, h_cy + h_r * 0.92],
                   fill=rgba(PLAYER_SKIN, 255))
        fd.ellipse([cx - h_r * 0.85, h_cy - h_r * 0.62, cx + h_r * 0.85, h_cy + h_r * 0.30],
                   fill=rgba(mul(PLAYER_HAIR, 0.92), 255))     # 앞머리
        for sx in (-1, 1):
            ex = cx + sx * h_r * 0.30
            ey = h_cy + h_r * 0.46
            fd.ellipse([ex - 2.1 * S, ey - 2.5 * S, ex + 2.1 * S, ey + 2.5 * S],
                       fill=rgba(mul(PLAYER_HAIR, 0.45), 245))
    elif facing == "side":
        # 옆 — 얼굴은 진행 방향 가장자리의 좁은 초승달. 눈 하나, 앞머리가 이마를 덮는다
        fd.ellipse([cx + h_r * 0.18, h_cy + h_r * 0.02, cx + h_r * 0.98, h_cy + h_r * 0.86],
                   fill=rgba(PLAYER_SKIN, 255))
        fd.ellipse([cx - h_r * 0.30, h_cy - h_r * 0.60, cx + h_r * 1.00, h_cy + h_r * 0.26],
                   fill=rgba(mul(PLAYER_HAIR, 0.92), 255))     # 앞머리
        ex = cx + h_r * 0.62
        ey = h_cy + h_r * 0.44
        fd.ellipse([ex - 2.1 * S, ey - 2.5 * S, ex + 2.1 * S, ey + 2.5 * S],
                   fill=rgba(mul(PLAYER_HAIR, 0.45), 245))
    else:
        # 뒤통수 — 가마만
        fd.ellipse([cx + 0 * S, h_cy - h_r * 0.30 - 2 * S, cx + 4 * S, h_cy - h_r * 0.30 + 2 * S],
                   fill=rgba(mul(PLAYER_HAIR, 0.65), 150))
    clip_to(face, mask)
    head = merge(head, face, blur=S // 3)

    # 머리 밑 접촉 그늘
    neck = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(neck).ellipse(
        [cx - h_r * 1.05, h_cy - h_r * 0.2, cx + h_r * 1.05, h_cy + h_r * 1.35],
        fill=rgba((0, 0, 0), 80))
    neck = neck.filter(ImageFilter.GaussianBlur(4 * S))
    return Image.alpha_composite(img, Image.alpha_composite(neck, head)), mask


def gen(facing, frame):
    rng = random.Random(42060 + frame)
    w, h = W * S, H * S
    img = canvas(w, h)
    cx = w / 2
    side = facing == "side"

    img = _feet(img, cx, h, frame, side)
    img, t_mask, t_cy, t_rx, t_ry = _torso(img, rng, cx, h, w, frame, side)
    img, h_mask = _head(img, w, h, cx, t_cy, frame, facing)

    sil = ImageChops.lighter(t_mask, h_mask)
    rim = rim_from_mask(sil, dx=2 * S, dy=3 * S, blur=2 * S)
    rim_layer = Image.new("RGBA", (w, h), rgba(mix(TV_GLOW, (255, 255, 255), 0.25), 0))
    rim_layer.putalpha(rim.point(lambda v: v * 68 // 255))
    img = Image.alpha_composite(img, rim_layer)

    img = contact_shadow(img, [cx - t_rx * 0.95, t_cy + t_ry * 0.30, cx + t_rx * 0.95, h * 0.95],
                         alpha=55, blur=4 * S)
    img = apply_grain(img, opacity=0.07, sigma=26)
    save(downscale(img, S), "Props/player_boy_%s_%d.png" % (facing, frame))


def run():
    print("[Player dirs]")
    for facing in ("up", "down", "side"):
        for frame in (0, 1, 2):
            gen(facing, frame)


if __name__ == "__main__":
    run()
