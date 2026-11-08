"""
Synthesises the Level 6 seagull sounds (no external assets, pure stdlib):
  Assets/Resources/Sounds/seagull_cry.wav  - three "kyow" calls
  Assets/Resources/Sounds/poop_splat.wav   - wet splat on the camera lens
Run from the project root:  python 3d/level6/make_seagull_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.realpath(__file__)), "..", "..", "Assets", "Resources", "Sounds")


def write(name, samples):
    peak = max(1e-6, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * 0.9 * 32767)) for s in samples))
    print("wrote", name, "%.2f s" % (len(samples) / RATE))


def gull_call(dur, f_start, f_peak, f_end, rng):
    out, phase = [], 0.0
    n = int(dur * RATE)
    for i in range(n):
        t = i / n
        # pitch rises fast to the peak, then falls ("kee-YOW")
        f = f_start + (f_peak - f_start) * min(1.0, t / 0.25) if t < 0.25 else f_peak + (f_end - f_peak) * (t - 0.25) / 0.75
        f *= 1.0 + 0.03 * math.sin(2 * math.pi * 38 * i / RATE)          # throat flutter
        phase += 2 * math.pi * f / RATE
        env = min(1.0, t / 0.06) * (1.0 - t) ** 1.3
        s = (math.sin(phase) + 0.6 * math.sin(2 * phase + 0.4) + 0.45 * math.sin(3 * phase)
             + 0.25 * math.sin(4 * phase + 1.1) + 0.12 * math.sin(5 * phase))
        s = math.tanh(1.8 * s)                                             # raspy
        out.append(env * (s + 0.08 * rng.uniform(-1, 1)))
    return out


def seagull_cry():
    rng = random.Random(3)
    out = []
    for dur, a, b, c, gap in ((0.22, 900, 1500, 1150, 0.07), (0.22, 950, 1550, 1150, 0.07), (0.42, 850, 1450, 800, 0.0)):
        out += gull_call(dur, a, b, c, rng) + [0.0] * int(gap * RATE)
    return out


def poop_splat():
    rng = random.Random(9)
    out, lp, lp2 = [], 0.0, 0.0
    n = int(0.32 * RATE)
    for i in range(n):
        t = i / RATE
        noise = rng.uniform(-1, 1)
        cut = 0.35 * math.exp(-t * 14) + 0.04                              # darkening low-pass sweep
        lp += cut * (noise - lp)
        lp2 += 0.5 * (lp - lp2)
        thump = math.sin(2 * math.pi * (140 - 200 * t) * t) * math.exp(-t * 30)
        drip = 0.0
        if t > 0.16:                                                       # small trailing drip
            td = t - 0.16
            drip = 0.35 * math.sin(2 * math.pi * (600 + 2500 * td) * td) * math.exp(-td * 45)
        out.append(math.exp(-t * 11) * 1.6 * lp2 + 0.9 * thump + drip)
    return out


write("seagull_cry.wav", seagull_cry())
write("poop_splat.wav", poop_splat())
