# Smoke-test audio pair generator (no ffmpeg available -> both versions synthesized).
# clear  : voice-band mixture 180~2900 Hz, syllable-like 4 Hz AM + slight vibrato, 4.0 s loop
# muffled: same synthesis but only components <= 600 Hz, overall -3 dB (simulates offline lowpass 600 Hz)
# Both share identical envelope/phase so the dual-source crossfade stays phase-coherent (architecture §7.2).
import math, struct, wave, os

SR = 44100
DUR = 4.0
N = int(SR * DUR)
OUT = os.path.dirname(os.path.abspath(__file__))

# (freq Hz, relative amp) — rough voice spectrum
COMPONENTS = [
    (180.0, 1.00), (360.0, 0.65), (540.0, 0.45),
    (720.0, 0.35), (1100.0, 0.28), (1500.0, 0.22),
    (2200.0, 0.15), (2900.0, 0.10),
]
LOWPASS_HZ = 600.0
MUFFLE_GAIN = 10 ** (-3.0 / 20.0)  # -3 dB

def synth(comps, gain):
    buf = []
    for i in range(N):
        t = i / SR
        # syllable-like AM (4 Hz) kept >0 so loop has no dead silence
        am = 0.55 + 0.45 * math.sin(2 * math.pi * 4.0 * t - math.pi / 2)
        vib = 1.0 + 0.004 * math.sin(2 * math.pi * 5.5 * t)
        s = 0.0
        for f, a in comps:
            s += a * math.sin(2 * math.pi * f * vib * t)
        # 10 ms edge fade against loop clicks
        edge = min(1.0, i / (SR * 0.01), (N - 1 - i) / (SR * 0.01))
        buf.append(s * am * edge * gain)
    return buf

full = synth(COMPONENTS, 1.0)
peak = max(abs(v) for v in full)
norm = 0.8 / peak  # normalize clear version to -1.9 dBFS-ish

low_comps = [(f, a) for f, a in COMPONENTS if f <= LOWPASS_HZ]

def write(name, comps, gain):
    data = synth(comps, norm * gain)
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(
            struct.pack("<h", max(-32767, min(32767, int(v * 32767)))) for v in data))
    print(name, "written")

write("smoke_voice_clear.wav", COMPONENTS, 1.0)
write("smoke_voice_muffled.wav", low_comps, MUFFLE_GAIN)
