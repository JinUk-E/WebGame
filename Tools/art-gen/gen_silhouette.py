# -*- coding: utf-8 -*-
"""Silhouette — 어둠 속 실루엣 1종 (명세 v0.5 §2).

**분위기 전용**이므로 공격 전조와 시각 문법이 겹치면 안 된다:
  전조 = 붉은 점멸 / 실루엣 = **색 없는 명도 차**. 그래서 팔레트 상수를 쓰지 않고 순수 무채색으로만 그린다.
탑뷰(정수리+어깨+길게 흘러내린 머리) — 소년 플레이어보다 세로로 길어 "사람이되 사람 비례가 아닌" 형체.
윤곽은 blur로 뭉갠다: 선명하면 오브젝트로 읽히고, 오브젝트로 읽히면 "대응해야 하나?"가 된다.
"""
import random

from PIL import Image, ImageDraw, ImageFilter

from artgen_common import apply_grain, blob_poly, canvas, downscale, rgba, save

S = 4        # 슈퍼샘플 배율 (gen_props와 동일)
SEED = 42500


def gen_silhouette():
    W, H = 64, 132              # 0.64 × 1.32u (PPU 100) — 플레이어 0.7×0.9보다 길다
    w, h = W * S, H * S
    rng = random.Random(SEED)
    img = canvas(w, h)
    cx = w / 2

    mask = Image.new("L", (w, h), 0)
    md = ImageDraw.Draw(mask)

    # 머리카락 — 정수리에서 아래로 길게 흘러내린 덩어리 (탑뷰에서 키를 대신 읽히게 하는 부분)
    hair = blob_poly(rng, cx, h * 0.55, w * 0.33, h * 0.39, irregularity=0.16, n=36)
    md.polygon(hair, fill=205)

    # 어깨 — 좁고 각짐
    md.polygon(blob_poly(rng, cx, h * 0.34, w * 0.36, h * 0.12, irregularity=0.08, n=28), fill=235)

    # 정수리
    hr = w * 0.26
    md.ellipse([cx - hr, h * 0.24 - hr, cx + hr, h * 0.24 + hr], fill=255)

    # 흘러내린 머리끝 — 몇 가닥이 더 길게 (바닥에 끌리는 인상)
    for _ in range(5):
        x = cx + rng.uniform(-w * 0.22, w * 0.22)
        y0 = h * 0.72
        y1 = h * rng.uniform(0.86, 0.98)
        md.line([(x, y0), (x + rng.uniform(-w * 0.06, w * 0.06), y1)],
                fill=170, width=int(rng.uniform(2, 4) * S))

    # 경계를 뭉갠다 — 명도 차만 남기고 형태의 확신을 지운다
    mask = mask.filter(ImageFilter.GaussianBlur(5 * S))

    # 무채색 단일 톤. 실제 밝기·알파는 런타임(SilhouetteDirector)이 정한다 — 여기서는 형태만 낸다.
    body = Image.new("RGBA", (w, h), rgba((228, 228, 232), 0))
    body.putalpha(mask)
    img = Image.alpha_composite(img, body)
    img = apply_grain(img, opacity=0.05, sigma=22)
    save(downscale(img, S), "Props/prop_silhouette.png")


def run():
    print("[Silhouette]")
    gen_silhouette()


if __name__ == "__main__":
    run()
