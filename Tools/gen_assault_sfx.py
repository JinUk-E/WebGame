# -*- coding: utf-8 -*-
"""P5 삼중 습격(triple-assault) 효과음 2종. 절차 생성·시드 고정 — 외부 에셋 아님.

  SFX_Phone/phone_ring.wav    전화벨 — 1970~80년대 시골 유선전화 (다른 방/복도에서 울린다)
  SFX_Handle/handle_rattle.wav  손잡이 덜컹 — 문 손잡이·걸쇠가 밖에서 흔들린다

자막은 예전부터 "(손잡이가 덜컹거린다 — 전화벨 — 노크 소리가 동시에 울린다)"라고 말해 왔지만
실제로 나는 소리는 문 효과음 하나뿐이었다. 말과 소리가 어긋나면 자막이 거짓말이 된다.

설계
  전화벨: 두 개의 종(gong)을 클래퍼가 **번갈아** 때리는 것이 옛 기계식 벨의 정체다.
          한 톤만 쓰면 알람이 되고, 두 톤이 교대해야 '따르릉'이 된다.
          울림 1초 → 쉼 1초 → 울림 1초 (기계식 벨은 계전기가 끊었다 붙었다 한다).
          **다른 방에서 들리는 소리**라 고역을 크게 깎고(1250Hz 3단 저역통과) 하우징 공명(172/244Hz)을
          남긴다 — 벽을 넘어오는 것은 몸통 울림이지 금속의 반짝임이 아니다.
          복도 반사 3탭으로 "여기가 아닌 곳"이라는 공간을 준다.
  손잡이: 짧고 강한 충격 6번(3+3, 두 번의 덜컹). 금속 걸쇠 클래터 + 나무 문짝 몸통 + 문틀 울림.
          **타격 시각은 C# RattlePattern.DoorHandle과 같은 표(HANDLE_HITS)** — 문짝이 흔들리는
          그림이 이 표를 그대로 쓰기 때문이다. 어긋나면 "쿵 —(늦게) 흔들림"이 되어 인과가 안 읽힌다.
          EditMode RattleSyncTests가 이 wav의 실제 온셋을 재서 C# 표와 대조한다.

실행: python Tools/gen_assault_sfx.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "_Project", "Audio")

# ⚠ C# Morae.Game.Core.RattlePattern.DoorHandle 의 타격표와 **같은 값**이어야 한다 (초, 세기)
HANDLE_HITS = [(0.000, 1.00), (0.115, 0.62), (0.230, 0.78),
               (0.800, 0.95), (0.915, 0.60), (1.030, 0.72)]
HANDLE_DURATION = 1.35

PHONE_DURATION = 3.60          # ⚠ C# TripleAssaultCue.PhoneDurationSec 와 같아야 한다
PHONE_BURSTS = [0.00, 2.05]    # 울림 시작 시각 (각 1.00초)
PHONE_BURST_LEN = 1.00


def write(path, samples, peak):
    peak_now = max(1e-6, max(abs(s) for s in samples))
    scale = peak / peak_now
    frames = b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s * scale)) * 32767)) for s in samples)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(frames)
    print("  %s  %.2fs %dHz" % (os.path.normpath(path), len(samples) / RATE, RATE))


def lowpass(samples, hz, stages):
    """1극 저역통과 다단 — 벽·복도 너머로 오는 소리는 고역이 먼저 죽는다."""
    a = 1.0 - math.exp(-2.0 * math.pi * hz / RATE)
    for _ in range(stages):
        y = 0.0
        for i, x in enumerate(samples):
            y += a * (x - y)
            samples[i] = y
    return samples


def highpass(samples, hz):
    """1극 고역통과 — DC·초저역 뭉침 제거 (없으면 저역이 서로 더해져 클리핑만 만든다)."""
    a = 1.0 - math.exp(-2.0 * math.pi * hz / RATE)
    y = 0.0
    out = []
    for x in samples:
        y += a * (x - y)
        out.append(x - y)
    return out


def reflections(samples, taps):
    """복도 반사 — 짧은 지연 몇 개면 '다른 방'이라는 인상이 생긴다 (poppo의 방 울림과 같은 수법)."""
    n = len(samples)
    for delay_sec, gain in taps:
        d = int(RATE * delay_sec)
        for i in range(n - 1, d - 1, -1):
            samples[i] += samples[i - d] * gain
    return samples


# ---------------------------------------------------------------- 전화벨
def gen_phone_ring():
    rng = random.Random(20260807)
    n_total = int(RATE * PHONE_DURATION)
    out = [0.0] * n_total

    # 두 개의 종 — 단3도쯤 벌린 두 톤이 교대한다. 부분음은 비조화(종의 특징)
    gongs = (605.0, 742.0)
    partials = ((1.00, 1.00, 7.5), (2.72, 0.38, 13.0), (5.36, 0.15, 20.0))
    clapper_period = 0.0513          # 19.5Hz — 옛 벨의 트릴 속도
    strike_tail = 0.22               # 한 타의 울림 길이 (다음 타보다 길어 서로 겹친다 = 연속된 '르르')

    for burst_start in PHONE_BURSTS:
        strike = 0
        t = 0.0
        while t < PHONE_BURST_LEN:
            f0 = gongs[strike % 2]                      # 교대 타격
            s0 = int(RATE * (burst_start + t))
            length = int(RATE * strike_tail)
            amp = 0.92 + 0.16 * rng.random()            # 타마다 미세한 세기 차 — 없으면 기계음이 된다
            for i in range(length):
                idx = s0 + i
                if idx >= n_total:
                    break
                lt = i / RATE
                v = 0.0
                for ratio, pa, decay in partials:
                    v += pa * math.sin(2.0 * math.pi * f0 * ratio * lt) * math.exp(-lt * decay)
                # 하우징 공명 — 벨이 앉은 나무/베이클라이트 몸통. 벽을 넘어오는 성분의 주역
                v += 0.62 * math.sin(2.0 * math.pi * 172.0 * lt) * math.exp(-lt * 9.0)
                v += 0.38 * math.sin(2.0 * math.pi * 244.0 * lt) * math.exp(-lt * 12.0)
                # 클래퍼가 종에 닿는 순간의 금속 접촉음 (1.5ms)
                if lt < 0.0015:
                    v += rng.uniform(-1.0, 1.0) * 0.30
                out[idx] += v * amp * 0.34
            strike += 1
            t += clapper_period

        # 울림 구간 포락선 — 계전기가 붙고 끊기는 시간 (딱 잘리면 테이프 자른 소리가 난다)
        b0 = int(RATE * burst_start)
        b1 = min(n_total, int(RATE * (burst_start + PHONE_BURST_LEN + strike_tail)))
        attack = int(RATE * 0.012)
        release = int(RATE * 0.09)
        rel_start = int(RATE * (burst_start + PHONE_BURST_LEN))
        for i in range(b0, b1):
            if i - b0 < attack:
                out[i] *= (i - b0) / attack
            elif i > rel_start:
                k = (i - rel_start) / release
                out[i] *= max(0.0, 1.0 - k) if k < 1.0 else 0.0

    out = reflections(out, ((0.037, 0.30), (0.071, 0.19), (0.113, 0.11)))
    out = highpass(out, 95.0)
    out = lowpass(out, 1250.0, 3)   # ← "다른 방에서" 의 실체. 이 값을 올리면 방 안 전화가 된다
    write(os.path.join(ROOT, "SFX_Phone", "phone_ring.wav"), out, 0.74)


# ---------------------------------------------------------------- 손잡이 덜컹
def gen_handle_rattle():
    rng = random.Random(20260808)
    n_total = int(RATE * HANDLE_DURATION)
    out = [0.0] * n_total

    for start, weight in HANDLE_HITS:
        s0 = int(RATE * start)
        length = int(RATE * 0.30)
        # 금속 걸쇠 — 2극 공진기 2개. r은 울림 길이로 역산(τ ≈ 0.030s: 짧게 '철컥'하고 죽어야
        # 다음 덜컹이 별개의 타격으로 들린다. 길면 뭉개져 온셋 검출도 실패한다)
        metal = ((2150.0, 0.9985, 0.55), (3320.0, 0.9978, 0.32))
        states = [[0.0, 0.0] for _ in metal]
        for i in range(length):
            idx = s0 + i
            if idx >= n_total:
                break
            lt = i / RATE
            # 여기 excitation: 앞 2ms 노이즈 = 금속이 부딪는 순간
            exc = rng.uniform(-1.0, 1.0) if lt < 0.002 else 0.0
            v = 0.0
            for k, (freq, r, amp) in enumerate(metal):
                w = 2.0 * math.pi * freq / RATE
                st = states[k]
                y = exc + 2.0 * r * math.cos(w) * st[0] - r * r * st[1]
                st[1] = st[0]
                st[0] = y
                # 금속 비중을 낮추면 노크(저역 일색)와 대역이 겹쳐 삼중 습격에서 둘이 한 덩어리로 뭉갠다
                v += y * amp * 0.17
            # 나무 문짝 몸통 — 문이 문틀에 부딪히는 저역. 이게 없으면 열쇠고리 소리가 된다
            v += 0.45 * math.sin(2.0 * math.pi * 96.0 * lt) * math.exp(-lt * 30.0)
            v += 0.32 * math.sin(2.0 * math.pi * 148.0 * lt) * math.exp(-lt * 38.0)
            # 문틀 울림
            v += 0.28 * math.sin(2.0 * math.pi * 340.0 * lt) * math.exp(-lt * 45.0)
            out[idx] += v * weight

    out = highpass(out, 70.0)
    out = lowpass(out, 2800.0, 2)   # 문은 바로 거기 있다 — 전화벨만큼 깎지 않는다
    # 앞 1ms 페이드인 (클릭 방지)
    fade = int(RATE * 0.001)
    for i in range(fade):
        out[i] *= i / fade
    write(os.path.join(ROOT, "SFX_Handle", "handle_rattle.wav"), out, 0.80)


def run():
    print("[Assault SFX]")
    gen_phone_ring()
    gen_handle_rattle()


if __name__ == "__main__":
    run()
