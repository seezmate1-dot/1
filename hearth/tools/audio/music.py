"""The music and the ambience of the fortress, synthesised (no samples, nothing to license).

- Hearth.ogg: a calm loop for the fortress. A plucked harp (Karplus-Strong) plays arpeggios over a soft
  pad in D dorian, a wooden flute answers now and then; slow, quiet, made to be heard for hours.
- Battle.ogg: the same harp and pad, faster, with low drums: for fights and the Surge.
- Fire.ogg: the crackle of the hearth (a low rumble and sparse pops), a seamless loop.
Run: python hearth/tools/audio/music.py (needs numpy and ffmpeg).
"""
import os
import subprocess
import wave

import numpy as np

SR = 32000
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                   "unity", "LastHearth", "Assets", "LastHearth", "Resources", "Audio")
rng = np.random.default_rng(7)


def note(name):
    names = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
    base = names[name[0]]
    i = 1
    if name[1] in "#b":
        base += 1 if name[1] == "#" else -1
        i = 2
    octave = int(name[i:])
    return 440.0 * 2 ** ((base + 12 * (octave - 4) - 9) / 12)


def pluck(freq, dur, bright=0.5, decay=0.996):
    """Karplus-Strong string: a burst of noise in a delay line, damped by averaging."""
    n = int(SR * dur)
    period = max(2, int(SR / freq))
    buf = rng.uniform(-1, 1, period)
    # soften the attack: lowpass the initial burst
    for _ in range(int(3 * (1 - bright)) + 1):
        buf = 0.5 * (buf + np.roll(buf, 1))
    out = np.empty(n)
    idx = 0
    for i in range(n):
        out[i] = buf[idx]
        nxt = (idx + 1) % period
        buf[idx] = decay * 0.5 * (buf[idx] + buf[nxt])
        idx = nxt
    env = np.minimum(1, np.arange(n) / (0.004 * SR))
    return out * env


def pad(freqs, dur, attack=1.2, release=1.5):
    t = np.arange(int(SR * dur)) / SR
    s = np.zeros_like(t)
    for f in freqs:
        for det in (-0.6, 0.0, 0.7):
            s += np.sin(2 * np.pi * (f + det) * t + rng.uniform(0, 6.28)) * 0.33
            s += 0.12 * np.sin(4 * np.pi * (f + det) * t)
    env = np.minimum(1, t / attack) * np.minimum(1, (dur - t) / release)
    return s * np.clip(env, 0, 1) / max(1, len(freqs))


def flute(freq, dur):
    t = np.arange(int(SR * dur)) / SR
    vib = 1 + 0.004 * np.sin(2 * np.pi * 5.2 * t) * np.minimum(1, t / 0.4)
    s = np.sin(2 * np.pi * freq * vib * t) + 0.18 * np.sin(4 * np.pi * freq * vib * t) + 0.05 * rng.normal(size=t.size)
    env = np.minimum(1, t / 0.12) * np.minimum(1, (dur - t) / 0.35)
    return s * np.clip(env, 0, 1)


def drum(dur=0.6, freq=60):
    t = np.arange(int(SR * dur)) / SR
    pitch = freq * (1 + 1.5 * np.exp(-t * 30))
    s = np.sin(2 * np.pi * np.cumsum(pitch) / SR) * np.exp(-t * 7)
    s += 0.2 * rng.normal(size=t.size) * np.exp(-t * 40)
    return s


def reverb(x, mix=0.3):
    """A small hall: a few combs and allpasses (Schroeder)."""
    out = np.zeros_like(x)
    for d, g in ((1557, 0.77), (1617, 0.76), (1491, 0.75), (1422, 0.74)):
        d = int(d * SR / 44100)
        y = np.copy(x)
        for i in range(d, len(y)):
            y[i] += g * y[i - d]
        out += y * 0.25
    for d, g in ((225, 0.5), (556, 0.5)):
        d = int(d * SR / 44100)
        y = np.copy(out)
        z = np.zeros_like(y)
        for i in range(len(y)):
            z[i] = -g * y[i] + (y[i - d] if i >= d else 0) + g * (z[i - d] if i >= d else 0)
        out = z
    return (1 - mix) * x + mix * out


def place(track, sound, at):
    i = int(at * SR)
    j = min(len(track), i + len(sound))
    if i < len(track):
        track[i:j] += sound[: j - i]


def loop_tail(track, tail):
    """Folds the reverb/decay tail over the start, so the loop is seamless."""
    n = len(track) - tail
    out = track[:n].copy()
    out[:tail] += track[n:]
    return out


def save(name, x, quality=4):
    x = x / (np.max(np.abs(x)) + 1e-9) * 0.8
    os.makedirs(OUT, exist_ok=True)
    wav = os.path.join(OUT, name + ".wav")
    with wave.open(wav, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-c:a", "libvorbis", "-q:a", str(quality), os.path.join(OUT, name + ".ogg")], check=True)
    os.remove(wav)


# D dorian: Dm - C - Bb - C | Dm - F - C - Am (each chord one bar)
PROGRESSION = [
    ("D3", ["D3", "A3", "D4", "F4"]), ("C3", ["C3", "G3", "C4", "E4"]), ("Bb2", ["Bb2", "F3", "Bb3", "D4"]), ("C3", ["C3", "G3", "C4", "E4"]),
    ("D3", ["D3", "A3", "D4", "F4"]), ("F2", ["F3", "C4", "F4", "A4"]), ("C3", ["C3", "G3", "C4", "E4"]), ("A2", ["A2", "E3", "A3", "C4"]),
]
MELODY = [  # (bar, beat, note, beats) - a few phrases of the flute, silent most of the time
    (4, 0, "A4", 1.5), (4, 1.5, "G4", 0.5), (4, 2, "F4", 1), (4, 3, "E4", 1), (5, 0, "D4", 3),
    (12, 0, "F4", 1), (12, 1, "G4", 1), (12, 2, "A4", 2), (13, 0, "C5", 1.5), (13, 1.5, "Bb4", 0.5), (13, 2, "A4", 2),
    (14, 0, "G4", 2), (14, 2, "F4", 1), (14, 3, "E4", 1), (15, 0, "D4", 4),
]


def hearth_theme(bpm=66, bars=16):
    beat = 60 / bpm
    total = bars * 4 * beat
    tail = 3.0
    track = np.zeros(int(SR * (total + tail)))
    for bar in range(bars):
        root, chord = PROGRESSION[bar % len(PROGRESSION)]
        start = bar * 4 * beat
        place(track, 0.45 * pad([note(n) for n in chord[:3]], 4 * beat + 1.5), start)
        place(track, 0.5 * pluck(note(root) / 2, 3.0, bright=0.2, decay=0.998), start)
        # arpeggio: up and down the chord in eighths, a little loose in time
        pattern = [0, 1, 2, 3, 2, 1, 2, 3]
        for k, p in enumerate(pattern):
            if bar % 4 == 3 and k > 5:
                continue
            at = start + k * beat / 2 + rng.uniform(0, 0.012)
            place(track, 0.32 * pluck(note(chord[p]), 2.2, bright=0.55), at)
    for bar, b, n, length in MELODY:
        place(track, 0.18 * flute(note(n), length * beat), (bar * 4 + b) * beat)
    track = reverb(track, 0.32)
    return loop_tail(track, int(SR * tail))


def battle_theme(bpm=104, bars=8):
    beat = 60 / bpm
    total = bars * 4 * beat
    tail = 2.0
    track = np.zeros(int(SR * (total + tail)))
    prog = [("D3", ["D3", "A3", "D4", "F4"]), ("Bb2", ["Bb2", "F3", "Bb3", "D4"]), ("C3", ["C3", "G3", "C4", "E4"]), ("A2", ["A2", "E3", "A3", "C#4"])]
    for bar in range(bars):
        root, chord = prog[bar % 4]
        start = bar * 4 * beat
        place(track, 0.35 * pad([note(n) for n in chord[:3]], 4 * beat + 0.8, attack=0.3, release=0.8), start)
        for k in range(8):
            place(track, 0.3 * pluck(note(chord[(k * 3) % 4]), 1.0, bright=0.8, decay=0.994), start + k * beat / 2)
        for k, accent in ((0, 1.0), (1.5, 0.6), (2, 0.9), (3, 0.6), (3.5, 0.5)):
            place(track, 0.5 * accent * drum(), start + k * beat)
    track = reverb(track, 0.22)
    return loop_tail(track, int(SR * tail))


def fire_loop(seconds=16):
    n = int(SR * seconds)
    white = rng.normal(size=n)
    brown = np.cumsum(white)
    brown -= np.convolve(brown, np.ones(2000) / 2000, mode="same")
    brown /= np.max(np.abs(brown)) + 1e-9
    rumble = np.convolve(brown, np.ones(40) / 40, mode="same") * 0.35
    track = rumble.copy()
    for _ in range(int(seconds * 9)):
        at = rng.uniform(0, seconds - 0.05)
        length = rng.uniform(0.003, 0.02)
        k = int(SR * length)
        pop = rng.normal(size=k) * np.exp(-np.arange(k) / (k / 4)) * rng.uniform(0.1, 0.6)
        place(track, pop, at)
    fade = int(SR * 0.5)
    track[:fade] *= np.linspace(0, 1, fade)
    track[-fade:] *= np.linspace(1, 0, fade)
    return track


if __name__ == "__main__":
    save("Hearth", hearth_theme())
    save("Battle", battle_theme())
    save("Fire", fire_loop(), quality=2)
    print("music written to", OUT)
