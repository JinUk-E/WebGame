# -*- coding: utf-8 -*-
"""
TV 잡음 루프 생성 (v0.6). 절차 생성·시드 고정 — 외부 에셋 아님.
산출: Assets/_Project/Audio/SFX_TV/tv_whisper_loop.wav

TV를 켜면 그 근처에서만 들리는 소리다. 설계 의도:
  ① 바탕은 **모래알 잡음**(대역제한 화이트) — 켜져 있다는 사실 자체를 알리는 층
  ② 그 위에 **말 같은 것**이 이따금 스쳐 간다 — 포먼트가 천천히 움직이는 아주 조용한 웅얼거림.
     알아들을 수 있으면 안 된다. 알아듣는 순간 자막이 필요해지고, 그러면 "가짜 목소리"와 문법이 겹친다.
  ③ 전원 험(60Hz 부근) 아주 약하게 — 브라운관의 몸통

루프 이음새는 크로스페이드. 볼륨·정위는 런타임(SoundManager)이 정한다.
실행: python Tools/gen_tv_whisper.py
"""
import math
import os
import random
import struct
import wave

SEED = 20260806
RATE = 22050
DURATION = 6.0
FADE = 0.5
PEAK = 0.6

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                   "..", "Assets", "_Project", "Audio", "SFX_TV", "tv_whisper_loop.wav")


def main():
    rng = random.Random(SEED)
    total = int(RATE * (DURATION + FADE))

    # ① 잡음 층 — 1극 저역통과 2단(≈4.5kHz) + 고역통과(≈420Hz)
    lp_a = 1.0 - math.exp(-2.0 * math.pi * 4500.0 / RATE)
    hp_a = math.exp(-2.0 * math.pi * 420.0 / RATE)
    lp1 = lp2 = 0.0
    hp_pin = hp_pout = 0.0

    # ② 웅얼거림 층 — 2극 공진기 2개(F1/F2)를 천천히 흔든다
    f1_state = [0.0, 0.0]
    f2_state = [0.0, 0.0]

    raw = []
    for n in range(total):
        t = n / RATE
        white = rng.uniform(-1.0, 1.0)

        lp1 += lp_a * (white - lp1)
        lp2 += lp_a * (lp1 - lp2)
        hp_out = hp_a * (hp_pout + lp2 - hp_pin)
        hp_pin, hp_pout = lp2, hp_out
        hiss = hp_out * (0.55 + 0.45 * (0.5 + 0.5 * math.sin(2.0 * math.pi * 0.23 * t)))

        # 말 같은 층 — 문장처럼 끊기는 게이트(0.6~1.4초 주기)
        gate = max(0.0, math.sin(2.0 * math.pi * 0.31 * t + math.sin(t * 0.7) * 1.3))
        f1 = 480.0 + 150.0 * math.sin(2.0 * math.pi * 0.9 * t)
        f2 = 1150.0 + 420.0 * math.sin(2.0 * math.pi * 0.6 * t + 1.1)
        src = white * 0.5
        w1 = 2.0 * math.pi * f1 / RATE
        w2 = 2.0 * math.pi * f2 / RATE
        y1 = src + 2.0 * 0.982 * math.cos(w1) * f1_state[0] - 0.982 * 0.982 * f1_state[1]
        f1_state[1], f1_state[0] = f1_state[0], y1
        y2 = src + 2.0 * 0.975 * math.cos(w2) * f2_state[0] - 0.975 * 0.975 * f2_state[1]
        f2_state[1], f2_state[0] = f2_state[0], y2
        murmur = (y1 * 0.030 + y2 * 0.018) * gate

        # ③ 전원 험
        hum = 0.02 * math.sin(2.0 * math.pi * 59.5 * t) + 0.01 * math.sin(2.0 * math.pi * 119.0 * t)

        raw.append(hiss * 0.55 + murmur + hum)

    # 루프 이음새 크로스페이드
    n_loop = int(RATE * DURATION)
    n_fade = int(RATE * FADE)
    out = raw[:n_loop]
    for i in range(n_fade):
        k = i / n_fade
        out[i] = out[i] * k + raw[n_loop + i] * (1.0 - k)

    peak = max(1e-6, max(abs(s) for s in out))
    scale = PEAK / peak
    frames = b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s * scale)) * 32767)) for s in out)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with wave.open(OUT, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(frames)
    print("  %s  %.1fs loop" % (os.path.normpath(OUT), DURATION))


if __name__ == "__main__":
    main()
