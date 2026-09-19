"""A small offline synthesiser for the house's music and cues (MASTER-PLAN §3.C).

Runs under Blender's Python because that is the numpy the project already has:

    blender --background --python ArtSource/audio/bb_music.py -- Assets/Gamesim/Resources/Audio

Everything is rendered, never played: oscillators with envelopes into a stereo buffer, an
FFT low-pass, a feedback delay and a small reverb, then 16-bit WAV. Loops are rendered twice
and the second pass kept, so the tails of the end already sit under the start and the loop
point is not a click.
"""
import math
import os
import wave

import numpy as np

SR = 44100


def seconds(n):
    return int(round(n * SR))


def osc(kind, freq, n, detune_cents=0.0, phase=0.0):
    f = freq * (2.0 ** (detune_cents / 1200.0))
    t = np.arange(n) / SR
    x = 2.0 * math.pi * f * t + phase
    if kind == "sine":
        return np.sin(x)
    if kind == "tri":
        return 2.0 / math.pi * np.arcsin(np.sin(x))
    if kind == "saw":
        # Additive up to the Nyquist so the saw does not alias into a fizz.
        out = np.zeros(n)
        k = 1
        while k * f < SR * 0.45 and k <= 40:
            out += np.sin(k * x) / k
            k += 1
        return out * (2.0 / math.pi)
    if kind == "square":
        out = np.zeros(n)
        k = 1
        while k * f < SR * 0.45 and k <= 40:
            out += np.sin(k * x) / k
            k += 2
        return out * (4.0 / math.pi)
    if kind == "noise":
        rng = np.random.default_rng(int(freq * 1000) & 0xFFFFFFFF)
        return rng.standard_normal(n)
    raise ValueError(kind)


def adsr(n, a, d, s, r):
    """Attack, decay, sustain level, release, in seconds and level; the release fits inside n."""
    env = np.zeros(n)
    na, nd, nr = seconds(a), seconds(d), seconds(r)
    ns = max(0, n - na - nd - nr)
    i = 0
    if na:
        env[i:i + na] = np.linspace(0.0, 1.0, na, endpoint=False)
        i += na
    if nd:
        env[i:i + nd] = np.linspace(1.0, s, nd, endpoint=False)
        i += nd
    if ns:
        env[i:i + ns] = s
        i += ns
    if nr:
        tail = min(nr, n - i)
        env[i:i + tail] = np.linspace(s, 0.0, tail, endpoint=False)
    return env[:n]


def lowpass(x, cutoff_hz, order=2.0):
    """A smooth spectral low-pass: vectorised, phase-free, good enough for a pad."""
    spectrum = np.fft.rfft(x)
    freqs = np.fft.rfftfreq(len(x), 1.0 / SR)
    gain = 1.0 / np.sqrt(1.0 + (freqs / max(cutoff_hz, 1.0)) ** (2 * order))
    return np.fft.irfft(spectrum * gain, n=len(x))


def highpass(x, cutoff_hz, order=2.0):
    spectrum = np.fft.rfft(x)
    freqs = np.fft.rfftfreq(len(x), 1.0 / SR)
    gain = 1.0 - 1.0 / np.sqrt(1.0 + (freqs / max(cutoff_hz, 1.0)) ** (2 * order))
    return np.fft.irfft(spectrum * gain, n=len(x))


def delay(x, time_s, feedback=0.35, mix=0.3, taps=8, damp_hz=4000.0):
    d = seconds(time_s)
    wet = np.zeros_like(x)
    tap = x.copy()
    for k in range(1, taps + 1):
        tap = lowpass(tap, damp_hz) * feedback
        shifted = np.zeros_like(x)
        if k * d < len(x):
            shifted[k * d:] = tap[:len(x) - k * d]
        wet += shifted
    return x + wet * mix


def reverb(x, size=0.6, mix=0.25):
    """Four dampened delays at inharmonic times; a room, not a hall."""
    times = [0.0297, 0.0371, 0.0411, 0.0437]
    wet = np.zeros_like(x)
    for t in times:
        wet += delay(x, t * (0.6 + size), feedback=0.5 + 0.3 * size, mix=1.0, taps=6, damp_hz=3200.0) - x
    return x + wet * (mix / len(times))


class Track:
    """A stereo buffer notes are added into."""

    def __init__(self, length_s):
        self.n = seconds(length_s)
        self.left = np.zeros(self.n)
        self.right = np.zeros(self.n)

    def add(self, mono, start_s, pan=0.0, gain=1.0):
        start = seconds(start_s) % self.n
        m = mono * gain
        # Wrap around the loop so a note across the end lands at the start.
        end = start + len(m)
        l = math.cos((pan + 1.0) * math.pi / 4.0)
        r = math.sin((pan + 1.0) * math.pi / 4.0)
        if end <= self.n:
            self.left[start:end] += m * l
            self.right[start:end] += m * r
        else:
            first = self.n - start
            self.left[start:] += m[:first] * l
            self.right[start:] += m[:first] * r
            rest = m[first:]
            rest = rest[:self.n]
            self.left[:len(rest)] += rest * l
            self.right[:len(rest)] += rest * r

    def process(self, fn):
        self.left = fn(self.left)
        self.right = fn(self.right)

    def stereo(self):
        return np.stack([self.left, self.right], axis=1)


def note(kind, freq, dur_s, env=(0.01, 0.1, 0.7, 0.2), detune=0.0, cutoff=None, gain=1.0):
    n = seconds(dur_s)
    x = osc(kind, freq, n, detune)
    if cutoff:
        x = lowpass(x, cutoff)
    return x * adsr(n, *env) * gain


def chorus_pad(freqs, dur_s, cutoff=1400.0, env=(0.6, 0.4, 0.8, 0.8), gain=0.2):
    """Detuned saws per note, low-passed: the pad under everything."""
    n = seconds(dur_s)
    out = np.zeros(n)
    for f in freqs:
        for cents in (-7.0, 0.0, 7.0):
            out += osc("saw", f, n, cents)
    out = lowpass(out, cutoff) / (3.0 * len(freqs))
    return out * adsr(n, *env) * gain


def normalize(stereo, peak_db=-1.0):
    peak = np.max(np.abs(stereo))
    if peak <= 0:
        return stereo
    return stereo * (10.0 ** (peak_db / 20.0) / peak)


def loop_twice(render, length_s):
    """Renders the loop twice end to end and keeps the second half: the end's tails are under the start."""
    track = Track(length_s * 2)
    render(track, 0.0)
    render(track, length_s)
    n = seconds(length_s)
    stereo = track.stereo()
    return stereo[n:n * 2]


def write_wav(path, stereo, sr=SR):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    data = np.clip(stereo, -1.0, 1.0)
    pcm = (data * 32767.0).astype("<i2")
    with wave.open(path, "wb") as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(sr)
        f.writeframes(pcm.tobytes())
    return path


NOTE_NAMES = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6,
              "G": 7, "G#": 8, "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}


def hz(name):
    """'A4' -> 440.0."""
    letter = name[:-1]
    octave = int(name[-1])
    semitone = NOTE_NAMES[letter] + (octave - 4) * 12 - 9
    return 440.0 * (2.0 ** (semitone / 12.0))
