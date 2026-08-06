# -*- coding: utf-8 -*-
"""
전조 두드림 생성 (v0.6 — 공격 전조의 촉각적 신호).
절차 생성·시드 고정 — 외부 에셋 아님. 산출: Assets/_Project/Audio/SFX_Knock/knock.wav

설계: 벽 **바깥**에서 두드리는 소리다. 그래서
  ① 저역 쿵(감쇠 사인 ~78Hz + 배음) = 벽을 타고 오는 몸통 울림
  ② 나무 딱(짧은 대역 노이즈, 초고속 감쇠) = 표면 접촉음. 벽 너머라 고역은 많이 깎는다
  ③ 꼬리 울림(저역 노이즈 감쇠) = 방 안에 남는 잔향
톤이 아니라 충격음이라 속삭임·대사와 대역이 겹치지 않는다. 방향 인지는 CornerSource 정위가 맡는다.
실행: python Tools/gen_knock.py
"""
import math
import os
import random
import struct
import wave

SEED = 20260806
RATE = 22050
DURATION = 0.62
PEAK = 0.82

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                   "..", "Assets", "_Project", "Audio", "SFX_Knock", "knock.wav")


def main():
    rng = random.Random(SEED)
    n_total = int(RATE * DURATION)

    # 벽 너머 소리 — 고역을 크게 깎는 1극 저역통과 2단(≈900Hz)
    lp_a = 1.0 - math.exp(-2.0 * math.pi * 900.0 / RATE)
    lp1 = lp2 = 0.0

    samples = []
    for n in range(n_total):
        t = n / RATE

        # ① 저역 쿵 — 두 성분이 살짝 어긋나게 감쇠해 "퉁"이 아니라 "쿵"으로 들린다
        body = (math.sin(2.0 * math.pi * 78.0 * t) * math.exp(-t * 16.0)
                + 0.45 * math.sin(2.0 * math.pi * 124.0 * t) * math.exp(-t * 26.0))

        # ② 나무 딱 — 첫 12ms에만 존재
        crack = 0.0
        if t < 0.012:
            crack = rng.uniform(-1.0, 1.0) * math.exp(-t * 260.0)

        # ③ 꼬리 울림
        tail = rng.uniform(-1.0, 1.0) * 0.30 * math.exp(-t * 9.0)

        raw = body * 0.85 + crack * 0.9 + tail
        lp1 += lp_a * (raw - lp1)
        lp2 += lp_a * (lp1 - lp2)
        samples.append(lp2)

    # 앞 2ms 페이드인 — 클릭 방지
    fade_in = int(RATE * 0.002)
    for i in range(fade_in):
        samples[i] *= i / fade_in

    peak = max(1e-6, max(abs(s) for s in samples))
    scale = PEAK / peak
    frames = b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s * scale)) * 32767)) for s in samples)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with wave.open(OUT, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(frames)
    print("  %s  %.2fs %dHz" % (os.path.normpath(OUT), DURATION, RATE))


if __name__ == "__main__":
    main()
