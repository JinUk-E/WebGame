# -*- coding: utf-8 -*-
"""
귀퉁이 속삭임 루프 생성 (명세 v0.5 §1 — 흑화 귀퉁이 방향 청각 상시 인지).
절차 생성·시드 고정 — 외부 에셋 아님. 산출: Assets/_Project/Audio/SFX_Corner/whisper_loop.wav
  대역제한 노이즈(숨소리) + 저주파 진폭 변조(들숨/날숨) + 루프 이음새 크로스페이드.
  톤이 아니라 노이즈라 방향 인지에만 쓰이고 대사·전조음을 가리지 않는다.
실행: python Tools/gen_whisper_loop.py
"""
import math
import os
import random
import struct
import wave

SEED = 20260806
RATE = 22050
DURATION = 5.0          # 루프 길이(초)
FADE = 0.45             # 이음새 크로스페이드
PEAK = 0.55             # 정규화 피크 (실제 볼륨은 런타임 BalanceConfig가 결정)

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                   "..", "Assets", "_Project", "Audio", "SFX_Corner", "whisper_loop.wav")


def main():
    rng = random.Random(SEED)
    total = int(RATE * (DURATION + FADE))

    # 대역제한: 1극 저역통과 2단(≈1.6kHz) 후 1극 고역통과(≈300Hz) — 숨소리 대역만 남긴다
    lp_a = 1.0 - math.exp(-2.0 * math.pi * 1600.0 / RATE)
    hp_a = math.exp(-2.0 * math.pi * 300.0 / RATE)
    lp1 = lp2 = 0.0
    hp_prev_in = hp_prev_out = 0.0

    raw = []
    for n in range(total):
        white = rng.uniform(-1.0, 1.0)
        lp1 += lp_a * (white - lp1)
        lp2 += lp_a * (lp1 - lp2)
        hp_out = hp_a * (hp_prev_out + lp2 - hp_prev_in)
        hp_prev_in, hp_prev_out = lp2, hp_out

        # 들숨/날숨 — 루프 길이의 정수배 주파수만 써서 이음새에서 위상이 맞는다
        t = n / RATE
        env = (0.42
               + 0.34 * (0.5 + 0.5 * math.sin(2.0 * math.pi * (2.0 / DURATION) * t))
               + 0.24 * (0.5 + 0.5 * math.sin(2.0 * math.pi * (3.0 / DURATION) * t + 1.7)))
        raw.append(hp_out * env)

    # 이음새 크로스페이드 — 꼬리 FADE초를 머리에 겹쳐 클릭 없는 루프로
    body = int(RATE * DURATION)
    fade = int(RATE * FADE)
    out = raw[:body]
    for i in range(fade):
        w = i / fade
        out[i] = out[i] * w + raw[body + i] * (1.0 - w)

    peak = max(abs(s) for s in out) or 1.0
    gain = PEAK / peak
    frames = b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s * gain)) * 32767)) for s in out)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with wave.open(OUT, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(frames)
    print("wrote", os.path.normpath(OUT), len(frames) // 2, "samples")


if __name__ == "__main__":
    main()
