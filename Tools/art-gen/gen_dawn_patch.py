# -*- coding: utf-8 -*-
"""바닥 창틀 빛 무늬 (명세 v0.7 §1) — 창을 통과한 빛이 바닥에 만드는 격자.

왜 두 장인가
------------
"아침이 갈수록 **선명해진다**"는 한 장으로는 못 만든다 (스프라이트에 블러를 걸 수 없다).
그래서 같은 발자국을 가진 두 겹을 만들고 런타임에서 알파를 반대로 움직인다:

* ``prop_dawn_patch_haze.png`` — 형태 없는 빛무리. **초반에 진하다.**
* ``prop_dawn_patch_grid.png`` — 창살 격자가 또렷한 판. **아침에 진하다.**

왜 이 모양인가
--------------
* **사다리꼴** — 창(뒷벽 y≈2.13)에서 방 안쪽(화면 아래)으로 퍼진다. 직사각형이면 바닥이 아니라
  벽에 붙은 판으로 보인다.
* **가운데 살 하나(= 두 칸)** — 실제로 배포되는 창 아트 ``Art/Room/room_window.png``(136×83px)는
  **투명한 창 두 칸 + 중앙 세로살 하나**다(알파 측정: 살 x61~72 ≈ 폭의 8.8%). 살 수가 다르면
  "저 빛이 저 창에서 왔다"가 안 읽힌다. ⚠ **창 아트를 다시 뽑아 살 배치가 바뀌면 여기도 고칠 것.**
  (``gen_room.gen_window``의 코드는 세로살 7개짜리 옛 버전이라 배포 파일과 다르다 — 기준은 **파일**이다.)
* **먼 쪽 감쇠** — 빛은 멀어질수록 흩어진다. 끝을 안 죽이면 바닥에 스티커를 붙인 것처럼 보인다.
* 색은 흰색 계열 + 알파만. 틴트(남색→회청→주황)는 런타임 ``DawnWindowView``가 얹는다.

크기: 200×260px @PPU100 = 2.0×2.6유닛 (``DawnWindowView.patchSpriteSize``와 짝).
시드 고정(20260809) — 재실행 시 동일 산출물.
실행: python Tools/art-gen/gen_dawn_patch.py
"""
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

from artgen_common import canvas, downscale, save

S = 3                 # 슈퍼샘플 배율
W, H = 200, 260       # 최종 픽셀 (PPU 100)
SEED = 20260809

TOP_HALF = 74         # 윗변(창 쪽) 반폭 — 창호지 폭 1.64u의 절반보다 조금 좁다
BOT_HALF = 98         # 아랫변(방 안쪽) 반폭 — 퍼진다
V_BARS = 1            # 세로살 개수 — 배포 창 아트가 중앙 살 하나(두 칸)다
BAR_PX = 16           # 살 그림자 폭(최종 px) — 창의 살 비율 8.8% × 무늬 폭 ≈ 16px


def _half_at(t):
    """세로 위치 t(0~1)에서의 반폭 (최종 px 기준)."""
    return TOP_HALF + (BOT_HALF - TOP_HALF) * t


def _footprint(n_w, n_h):
    """사다리꼴 발자국 마스크 — 두 겹이 정확히 같은 자리를 쓴다."""
    m = Image.new("L", (n_w, n_h), 0)
    d = ImageDraw.Draw(m)
    cx = n_w / 2
    d.polygon(
        [
            (cx - TOP_HALF * S, 0),
            (cx + TOP_HALF * S, 0),
            (cx + BOT_HALF * S, n_h - 1),
            (cx - BOT_HALF * S, n_h - 1),
        ],
        fill=255,
    )
    return m


def _falloff(n_w, n_h):
    """먼 쪽으로 갈수록 흩어지는 감쇠 (세로) × 가장자리가 무른 감쇠 (가로)."""
    m = Image.new("L", (n_w, n_h), 0)
    px = m.load()
    cx = n_w / 2
    for y in range(n_h):
        t = y / (n_h - 1)
        # 창 바로 아래가 가장 진하고, 끝에서 거의 사라진다 (지수에 가까운 곡선)
        v = (1.0 - t) ** 1.35
        v = 0.10 + 0.90 * v
        half = _half_at(t) * S
        for x in range(n_w):
            e = abs(x - cx) / half if half > 0 else 2.0
            if e >= 1.0:
                px[x, y] = 0
                continue
            # 가장자리 20%에서 부드럽게 죽인다 — 칼로 자른 경계는 빛으로 안 보인다
            edge = 1.0 if e < 0.78 else max(0.0, 1.0 - (e - 0.78) / 0.22)
            px[x, y] = int(255 * v * edge)
    return m


def _bars(n_w, n_h):
    """창살 그림자 마스크 (흰=살, 검=빈칸) — 격자판에서 빼낸다."""
    m = Image.new("L", (n_w, n_h), 0)
    d = ImageDraw.Draw(m)
    cx = n_w / 2
    # 세로살 — 사다리꼴을 따라 벌어지는 선. 창에서 멀수록 그림자도 함께 벌어진다.
    for i in range(1, V_BARS + 1):
        f = i / (V_BARS + 1) * 2.0 - 1.0            # -1 ~ +1 사이 상대 위치
        x0 = cx + f * TOP_HALF * S
        x1 = cx + f * BOT_HALF * S
        d.line([(x0, 0), (x1, n_h - 1)], fill=255, width=int(BAR_PX * S))
    return m


def _speckle(rnd, n_w, n_h):
    """미세 얼룩 — 완전히 균질한 빛은 바닥이 아니라 UI 오버레이로 보인다."""
    speck = Image.new("L", (n_w, n_h), 0)
    sd = ImageDraw.Draw(speck)
    for _ in range(260):
        t = rnd.random()
        half = _half_at(t) * S
        x = n_w / 2 + rnd.uniform(-0.9, 0.9) * half
        y = t * (n_h - 1)
        r = rnd.uniform(1.5, 5.0) * S
        sd.ellipse([x - r, y - r, x + r, y + r], fill=rnd.randint(6, 20))
    return speck.filter(ImageFilter.GaussianBlur(2.0 * S))


def build():
    rnd = random.Random(SEED)
    n_w, n_h = W * S, H * S
    black = Image.new("L", (n_w, n_h), 0)

    foot = _footprint(n_w, n_h)
    base_a = Image.composite(_falloff(n_w, n_h), black, foot)

    # ---------- 격자판 ----------
    # 살 아래는 완전히 검지 않다 — 창호지가 빛을 돌려보내 흐릿하게 남는다 (88%만 깎는다)
    bars = _bars(n_w, n_h).filter(ImageFilter.GaussianBlur(1.6 * S))
    keep = bars.point(lambda v: 255 - int(v * 0.88))
    grid_a = ImageChops.multiply(base_a, keep)
    grid_a = ImageChops.multiply(ImageChops.add(grid_a, _speckle(rnd, n_w, n_h)), foot.point(lambda v: 255 if v else 0))
    grid = canvas(n_w, n_h, (255, 255, 255, 0))
    grid.putalpha(grid_a)
    grid = grid.filter(ImageFilter.GaussianBlur(0.5 * S))

    # ---------- 빛무리 ----------
    haze = canvas(n_w, n_h, (255, 255, 255, 0))
    haze.putalpha(base_a.filter(ImageFilter.GaussianBlur(9.0 * S)))

    return downscale(grid, S), downscale(haze, S)


if __name__ == "__main__":
    print("바닥 창틀 빛 무늬 생성:")
    g, hz = build()
    save(g, "Props/prop_dawn_patch_grid.png")
    save(hz, "Props/prop_dawn_patch_haze.png")
