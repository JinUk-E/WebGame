# -*- coding: utf-8 -*-
"""
시계 바늘 스프라이트의 rect·피벗을 알파 경계에서 다시 계산해 .meta에 박는다 (v0.6).

**왜 필요한가**: 바늘은 80x80 캔버스에 **축=캔버스 중심** 기준으로 그려지는데, 스프라이트 rect는
알파 경계로 잘려 있다. 피벗이 Center(잘린 rect의 중심)면 회전축이 캔버스 중심이 아니라 바늘 몸통 중간에
놓여서, 시계가 돌 때 "바늘이 중간을 축으로 도는" 그림이 된다.
그래서 rect는 실제 알파 경계로, 피벗은 **캔버스 중심이 rect 안에서 차지하는 비율**(Custom)로 넣는다.
아트를 다시 뽑을 때마다 이 스크립트를 함께 돌리면 축이 어긋날 일이 없다.

guid·internalID·spriteID는 건드리지 않는다 — 씬 참조가 끊긴다.
실행: python Tools/art-gen/fix_clock_pivot.py
"""
import io
import os
import re

from PIL import Image

ART = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..",
                   "Assets", "_Project", "Art", "Props")
TARGETS = ("prop_clock_hand_hour", "prop_clock_hand_minute")


def fix(name):
    png = os.path.join(ART, name + ".png")
    meta = png + ".meta"
    im = Image.open(png).convert("RGBA")
    W, H = im.size
    box = im.split()[3].getbbox()
    if box is None:
        print("  %s: 비어 있음 — 건너뜀" % name)
        return
    x0, y0_pil, x1, y1_pil = box
    w, h = x1 - x0, y1_pil - y0_pil
    y0 = H - y1_pil                    # PIL(위 기준) → 텍스처(아래 기준)

    # 캔버스 중심 = 바늘의 회전축. rect 안에서의 정규화 위치가 곧 피벗이다.
    cx, cy = W / 2.0, H / 2.0
    pivot_x = (cx - x0) / w
    pivot_y = (cy - y0) / h

    t = io.open(meta, encoding="utf-8", newline="").read()
    t = re.sub(r"(        x: )\d+", r"\g<1>%d" % x0, t, count=1)
    t = re.sub(r"(        y: )\d+", r"\g<1>%d" % y0, t, count=1)
    t = re.sub(r"(        width: )\d+", r"\g<1>%d" % w, t, count=1)
    t = re.sub(r"(        height: )\d+", r"\g<1>%d" % h, t, count=1)
    # 스프라이트시트 항목의 alignment(9=Custom)와 pivot — 파일 상단 기본값(alignment/spritePivot)이 아니라
    # **시트 항목** 쪽이 실제로 적용된다.
    t = re.sub(r"(      alignment: )\d+", r"\g<1>9", t, count=1)
    t = re.sub(r"(      pivot: \{x: )[-\d.]+(, y: )[-\d.]+(\})",
               r"\g<1>%.4f\g<2>%.4f\g<3>" % (pivot_x, pivot_y), t, count=1)
    # 임포터 상단 기본값도 같은 값으로 맞춰 둔다 (인스펙터에서 열었을 때 혼동 방지)
    t = re.sub(r"(  alignment: )\d+", r"\g<1>9", t, count=1)
    t = re.sub(r"(  spritePivot: \{x: )[-\d.]+(, y: )[-\d.]+(\})",
               r"\g<1>%.4f\g<2>%.4f\g<3>" % (pivot_x, pivot_y), t, count=1)
    io.open(meta, "w", encoding="utf-8", newline="").write(t)
    print("  %s: rect=(%d,%d,%d,%d) pivot=(%.4f, %.4f)" % (name, x0, y0, w, h, pivot_x, pivot_y))


def run():
    print("[Clock pivot]")
    for n in TARGETS:
        fix(n)


if __name__ == "__main__":
    run()
