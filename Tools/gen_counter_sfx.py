# -*- coding: utf-8 -*-
"""방어 판정 효과음 2종 (v0.6). 절차 생성·시드 고정 — 외부 에셋 아님.

  SFX_Purify/purify.wav  상쇄 성공 — 결계가 다시 잠기는 소리
  SFX_Poppo/poppo.wav    상쇄 실패 — 팔척님의 '포포포' (프롤로그에서 예고된 그 소리)

설계 원칙: 두 소리는 **대역과 결이 정반대**여야 한다. 성공은 맑고 위로 열리는 금속성,
실패는 탁하고 아래로 내려앉는 사람 목소리. 어두운 화면에서 눈을 감고도 결과가 갈려야 한다.
실행: python Tools/gen_counter_sfx.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "_Project", "Audio")


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
    print("  %s  %.2fs" % (os.path.normpath(path), len(samples) / RATE))


def resonator(x, freq, r, state):
    """2극 공진기 — 포먼트(목소리의 '오' 음색)를 만든다. state = [y1, y2]."""
    w = 2.0 * math.pi * freq / RATE
    y = x + 2.0 * r * math.cos(w) * state[0] - r * r * state[1]
    state[1] = state[0]
    state[0] = y
    return y


# ---------------------------------------------------------------- 상쇄 성공
def gen_purify():
    """맑은 종 + 위로 흩어지는 반짝임. 짧게 끝난다 — 성공은 여운보다 확실함이 중요하다."""
    rng = random.Random(52001)
    dur = 1.15
    n_total = int(RATE * dur)
    # 부분음: 완전5도·옥타브 위주 (불협을 피해 '정화'로 들리게)
    partials = [(660.0, 1.00, 3.2), (990.0, 0.55, 4.1), (1320.0, 0.35, 5.0), (1980.0, 0.18, 7.0)]
    out = []
    for n in range(n_total):
        t = n / RATE
        v = 0.0
        for f, amp, decay in partials:
            v += amp * math.sin(2.0 * math.pi * f * t) * math.exp(-t * decay)
        # 반짝임 — 첫 0.25초 고역 노이즈가 위로 빠진다
        if t < 0.25:
            v += rng.uniform(-1.0, 1.0) * 0.22 * math.exp(-t * 18.0)
        # 바닥을 받치는 저역 (몸통 없이 종만 있으면 얇게 들린다)
        v += 0.35 * math.sin(2.0 * math.pi * 165.0 * t) * math.exp(-t * 6.5)
        out.append(v)

    attack = int(RATE * 0.004)
    for i in range(attack):
        out[i] *= i / attack
    write(os.path.join(ROOT, "SFX_Purify", "purify.wav"), out, 0.78)


# ---------------------------------------------------------------- 상쇄 실패
def gen_poppo():
    """
    '포 포 포' — 입술 파열음 + 낮은 '오' 모음 3연.
    음정을 조금씩 **내려** 잡는다. 올라가면 장난스럽고, 내려가야 조롱으로 들린다.
    """
    rng = random.Random(52002)
    syllables = [(0.00, 172.0), (0.30, 163.0), (0.60, 152.0)]
    dur = 1.35
    n_total = int(RATE * dur)
    out = [0.0] * n_total

    for start, f0 in syllables:
        s0 = int(RATE * start)
        length = int(RATE * 0.26)
        # 포먼트 상태 — 음절마다 새로 (앞 음절 울림이 새 음절로 새면 뭉갠다)
        f1_state = [0.0, 0.0]
        f2_state = [0.0, 0.0]
        for i in range(length):
            if s0 + i >= n_total:
                break
            t = i / RATE
            # ① 파열음 'ㅍ' — 앞 10ms 숨 터짐
            burst = rng.uniform(-1.0, 1.0) * math.exp(-t * 300.0) if t < 0.012 else 0.0
            # ② 성대 — 펄스열(성문파 근사). 하강 피치가 목소리를 무겁게 만든다
            f = f0 * (1.0 - 0.08 * (t / 0.26))
            phase = (t * f) % 1.0
            glottal = (1.0 - 2.0 * phase) ** 3   # 톱니보다 부드럽고 배음이 풍부
            # ③ 모음 '오' 포먼트 (F1 430 / F2 760)
            v = resonator(glottal * 0.5 + burst, 430.0, 0.976, f1_state) * 0.6
            v += resonator(glottal * 0.35, 760.0, 0.968, f2_state) * 0.35
            # ④ 숨 섞임 — 사람이되 사람 같지 않게
            v += rng.uniform(-1.0, 1.0) * 0.05 * math.exp(-t * 6.0)
            # 음절 포락선 (부드럽게 열고 닫힘)
            env = math.sin(math.pi * min(1.0, t / 0.26)) ** 0.8
            out[s0 + i] += v * env * 0.5

    # 방 울림 꼬리 — 아주 짧은 지연 반사 2개 (문 밖에서 들어온 소리라는 인상)
    for delay, gain in ((int(RATE * 0.055), 0.28), (int(RATE * 0.11), 0.16)):
        for n in range(n_total - 1, delay - 1, -1):
            out[n] += out[n - delay] * gain

    write(os.path.join(ROOT, "SFX_Poppo", "poppo.wav"), out, 0.80)


def run():
    print("[Counter SFX]")
    gen_purify()
    gen_poppo()


if __name__ == "__main__":
    run()
