# -*- coding: utf-8 -*-
"""목적지 서클 (prop_stand_marker.png) — 프롤로그 학습 구간의 "여기 서라" 바닥 마커.

왜 이 모양인가
--------------
* **바닥에 눌린 타원** — 탑뷰 원근(salt_mark와 같은 SQUASH 0.78). 정원이면 벽에 붙은 표지처럼 보인다.
* **원반 + 대각 브래킷** — 결계 소금길(salt_ward: 방 둘레를 도는 가는 폐곡선)과 형태가 겹치지 않게,
  그리고 기도 빔(직선)과도 다르게. 브래킷 4개는 대각(45/135/225/315°)에 둔다 — 조준할 네 귀퉁이의 방향이다.
* **따뜻한 아이보리** — 붉은색은 전조의 것이라 주의 유도에 쓰면 "위험"과 "안내"가 섞인다 (v0.6 시각 문법).
  틴트는 런타임(DestinationMarkerView)이 얹으므로 텍스처는 **흰색 계열 + 알파**로만 뽑는다.

시드 고정(20260807) — 재실행 시 동일 산출물.
실행: python Tools/art-gen/gen_marker.py
"""
import math
import random

from PIL import ImageDraw, ImageFilter

from artgen_common import canvas, downscale, save

S = 4                    # 슈퍼샘플 배율
SIZE = 160               # 최종 픽셀 (PPU 100 → 1.6유닛)
SQUASH = 0.78            # 바닥 원근 — DestinationMarkerView.verticalSquash와 같은 값이어야 한다
SEED = 20260807

R_DISC = 56              # 원반 반경(가로, 최종 px) — arriveRadius 0.55u ≈ 55px와 짝
R_RING = 58
R_BRACKET = 72


def _ellipse(d, cx, cy, rx, ry, **kw):
    d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], **kw)


def build():
    rnd = random.Random(SEED)
    n = SIZE * S
    img = canvas(n, n)
    d = ImageDraw.Draw(img)
    c = n / 2
    rx = R_DISC * S
    ry = rx * SQUASH

    # ① 안쪽 원반 — 가운데는 거의 비운다. 플레이어가 그 위에 서므로 속이 차 있으면 캐릭터를 가린다.
    steps = 22
    for i in range(steps):
        t = i / (steps - 1)
        a = int(3 + 30 * (t ** 3.0))
        _ellipse(d, c, c, rx * (0.35 + 0.65 * t), ry * (0.35 + 0.65 * t), outline=(255, 252, 244, a), width=int(2.6 * S))

    # ② 테두리 링 — 얇은 이중선. 안쪽이 굵고 바깥이 가늘어 "바닥에 찍힌 자국"처럼 보인다.
    _ellipse(d, c, c, R_RING * S, R_RING * S * SQUASH, outline=(255, 250, 238, 150), width=int(2.2 * S))
    _ellipse(d, c, c, (R_RING + 4) * S, (R_RING + 4) * S * SQUASH, outline=(255, 248, 232, 64), width=int(1.2 * S))

    # ③ 대각 브래킷 4개 — 조준할 네 귀퉁이 방향. 원 둘레를 끊어 놓아 소금길(연속 폐곡선)과 구분된다.
    bx = R_BRACKET * S
    by = bx * SQUASH
    span = 26
    for center in (45, 135, 225, 315):
        box = [c - bx, c - by, c + bx, c + by]
        # 화면 좌표계는 y가 아래로 커진다 — 각도 부호를 뒤집어 논리각과 맞춘다
        d.arc(box, -center - span / 2, -center + span / 2, fill=(255, 250, 240, 225), width=int(3.6 * S))

    # ④ 먼지 알갱이 — 다른 소품과 같은 질감(완전 매끈한 도형은 이 방의 물건으로 안 보인다)
    for _ in range(220):
        ang = rnd.uniform(0, math.tau)
        rad = math.sqrt(rnd.random())
        px = c + math.cos(ang) * rad * rx * 0.95
        py = c + math.sin(ang) * rad * ry * 0.95
        r = rnd.uniform(0.6, 1.8) * S
        a = rnd.randint(10, 40)
        d.ellipse([px - r, py - r, px + r, py + r], fill=(255, 253, 246, a))

    img = img.filter(ImageFilter.GaussianBlur(radius=1.1 * S))
    return downscale(img, S)


if __name__ == "__main__":
    print("목적지 서클 생성:")
    save(build(), "Props/prop_stand_marker.png")
