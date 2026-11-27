"""The sounds of the player's own activities: one loop for each activity that makes a sound, named
for it (HouseFurnitureActivity), that HouseAudio plays on its activity voice while the player is at it.

    python ArtSource/audio/bb_foley.py Assets/Gamesim/Resources/Audio/Activities

or under Blender's Python, as the other renders:

    blender --background --python ArtSource/audio/bb_foley.py -- Assets/Gamesim/Resources/Audio/Activities

PrepareSnack: a knife on a board, in runs, and a bite. Cook: a pan sizzling, with crackles. Swim: strokes
and the water lapping. Soak: the hot tub's jets and its bubbles. Sleep: slow breathing with a soft snore.
Dance: a bright four-bar beat at 120 beats a minute, C, A minor, F, G. Sitting at the table and resting on
a lounger make no sound of their own (ActivitySounds has no row for them).

Every loop is rendered twice with the second pass kept (bb_synth.loop_twice), so the end's tails sit under
the start, and its noise beds are filtered as whole loops, which wraps them too: the seam is not a click.
Each is set to its own loudness (RMS over both channels) with its peak held under PEAK_CEILING: under the
game's news cues, over the room's bed, and the dance loudest because it is the music.

The randomness is numpy's, seeded here per loop: the same files on every render, and nothing to do with any
season's draws.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bb_synth as s  # noqa: E402

# The highest a loop's peak may reach, in dBFS.
PEAK_CEILING = -3.0


def at_level(stereo, rms_db, ceiling_db=PEAK_CEILING):
    """The loop scaled so its RMS is rms_db, unless that would put its peak over ceiling_db."""
    rms = float(np.sqrt(np.mean(stereo ** 2)))
    peak = float(np.max(np.abs(stereo)))
    if rms <= 0.0 or peak <= 0.0:
        return stereo
    gain = min(10.0 ** (rms_db / 20.0) / rms, 10.0 ** (ceiling_db / 20.0) / peak)
    return stereo * gain


def unit(x):
    """x at a standard deviation of one, so a gain is a level."""
    sd = float(np.std(x))
    return x / sd if sd > 0.0 else x


def band(x, low, high):
    return s.highpass(s.lowpass(x, high), low)


def bed(rng, n, low=None, high=None):
    """A whole loop of noise, filtered as a loop (the FFT is circular, so it wraps), at unit level."""
    x = rng.standard_normal(n)
    if high:
        x = s.lowpass(x, high)
    if low:
        x = s.highpass(x, low)
    return unit(x)


def wave(n, length, cycles, phase=0.0):
    """A sine that turns a whole number of times over the loop, so it wraps."""
    t = np.arange(n) / s.SR
    return np.sin(2.0 * math.pi * cycles * t / length + phase)


def burst(rng, dur, low, high, attack, tau):
    """A short burst of band noise: a rise over attack seconds and a fall with time constant tau, peak one."""
    n = max(8, s.seconds(dur))
    t = np.arange(n) / s.SR
    x = band(rng.standard_normal(n), low, high)
    env = (1.0 - np.exp(-t / max(attack, 1e-4))) * np.exp(-t / tau)
    x = x * env
    peak = float(np.max(np.abs(x)))
    return x / peak if peak > 0.0 else x


def bubble(f0, dur, rise=0.8):
    """A bubble: a sine whose pitch climbs as it rises, gone in dur seconds."""
    n = max(8, s.seconds(dur))
    t = np.arange(n) / s.SR
    phase = 2.0 * math.pi * f0 * (t + 0.5 * rise * t * t / dur)
    env = (1.0 - np.exp(-t / 0.001)) * np.exp(-t / (dur / 3.0))
    return np.sin(phase) * env


def glide(f_start, f_end, dur, tau):
    """A sine falling from f_start toward f_end, as a drum's skin or a hand into water does."""
    n = max(8, s.seconds(dur))
    t = np.arange(n) / s.SR
    f = f_end + (f_start - f_end) * np.exp(-t * 25.0)
    phase = 2.0 * math.pi * np.cumsum(f) / s.SR
    env = (1.0 - np.exp(-t / 0.002)) * np.exp(-t / tau)
    return np.sin(phase) * env


# ------------------------------------------------------------------------------------------ the loops

def prepare_snack(track, offset, length):
    rng = np.random.default_rng(505)
    n = s.seconds(length)
    track.add(bed(rng, n, high=600.0) * 0.004, offset)                       # the kitchen's air under it
    for start, count, gap in ((0.25, 5, 0.26), (2.55, 4, 0.28), (4.55, 6, 0.24)):
        for i in range(count):
            at = start + i * gap + rng.uniform(-0.02, 0.02)
            board = glide(rng.uniform(210.0, 260.0), rng.uniform(160.0, 190.0), 0.16, 0.035)
            blade = burst(rng, 0.012, 2000.0, 8000.0, 0.0005, 0.002)
            knock = burst(rng, 0.05, 400.0, 1500.0, 0.001, 0.015)
            chop = np.zeros(max(len(board), len(blade), len(knock)))
            chop[:len(board)] += board * 0.35
            chop[:len(blade)] += blade * 0.22
            chop[:len(knock)] += knock * 0.15
            track.add(chop * rng.uniform(0.8, 1.0), offset + at, pan=0.1)
    # The bag opened between the second and third runs, and a crisp bite at the end.
    rustle = burst(rng, 0.4, 1500.0, 6000.0, 0.06, 0.12)
    track.add(rustle * 0.06, offset + 3.9, pan=-0.2)
    for k in range(8):
        crumb = burst(rng, 0.03, 1500.0, 6000.0, 0.0008, 0.008)
        track.add(crumb * rng.uniform(0.08, 0.2), offset + 6.9 + k * 0.03 + rng.uniform(0.0, 0.01), pan=0.0)


def cook(track, offset, length):
    rng = np.random.default_rng(101)
    n = s.seconds(length)
    swell = 0.8 + 0.12 * wave(n, length, 3) + 0.08 * wave(n, length, 7, 1.3)
    track.add(bed(rng, n, 1800.0, 9000.0) * swell * 0.05, offset, pan=-0.5)  # the sizzle, wide
    track.add(bed(rng, n, 1800.0, 9000.0) * swell * 0.05, offset, pan=0.5)
    track.add(bed(rng, n, high=220.0) * 0.02, offset)                        # the pan on the flame
    for at in rng.uniform(0.0, length, int(length * 30)):                     # the fat crackling
        pop = burst(rng, rng.uniform(0.004, 0.012), 2500.0, 10000.0, 0.0003, 0.002)
        track.add(pop * math.exp(rng.uniform(math.log(0.04), math.log(0.3))), offset + at, pan=rng.uniform(-0.6, 0.6))


def swim(track, offset, length):
    rng = np.random.default_rng(202)
    n = s.seconds(length)
    cycles = round(length * 0.5)
    track.add(bed(rng, n, high=500.0) * (0.6 + 0.4 * wave(n, length, cycles)) * 0.05, offset, pan=-0.3)
    track.add(bed(rng, n, high=500.0) * (0.6 + 0.4 * wave(n, length, cycles, 2.0)) * 0.05, offset, pan=0.3)
    strokes = np.arange(0.0, length, 1.0)
    for i, at in enumerate(strokes):
        pan = 0.4 if i % 2 == 0 else -0.4
        at = at + rng.uniform(-0.06, 0.06)
        track.add(burst(rng, 0.45, 250.0, 3500.0, 0.025, 0.12) * 0.3, offset + at, pan=pan)
        track.add(glide(320.0, 140.0, 0.12, 0.04) * 0.12, offset + at + 0.03, pan=pan)
    for at in rng.uniform(0.0, length, int(length * 5)):                      # drops falling back
        drop = bubble(rng.uniform(900.0, 2200.0), rng.uniform(0.012, 0.03))
        track.add(drop * rng.uniform(0.02, 0.07), offset + at, pan=rng.uniform(-0.7, 0.7))


def soak(track, offset, length):
    rng = np.random.default_rng(303)
    n = s.seconds(length)
    track.add(bed(rng, n, high=260.0) * 0.06, offset, pan=-0.4)              # the jets
    track.add(bed(rng, n, high=260.0) * 0.06, offset, pan=0.4)
    churn = bed(rng, n, 400.0, 1400.0) * (0.75 + 0.25 * wave(n, length, 13))
    track.add(churn * 0.025, offset)                                          # the water churning
    t = np.arange(n) / s.SR
    track.add((np.sin(2.0 * math.pi * 60.0 * t) + 0.5 * np.sin(2.0 * math.pi * 120.0 * t)) * 0.006, offset)  # the pump
    for at in rng.uniform(0.0, length, int(length * 45)):                     # the bubbles
        b = bubble(rng.uniform(280.0, 1300.0), rng.uniform(0.02, 0.08))
        track.add(b * rng.uniform(0.02, 0.09), offset + at, pan=rng.uniform(-0.8, 0.8))


def sleep(track, offset, length):
    rng = np.random.default_rng(404)
    n = s.seconds(length)
    track.add(bed(rng, n, high=400.0) * 0.006, offset)                       # the room's air
    half = length / 2.0
    for at, flutter, gain in ((0.3, 29.0, 1.0), (0.3 + half, 32.0, 0.85)):
        # Breathing in, with the soft palate's flutter: the snore.
        d = 1.6
        m = s.seconds(d)
        tt = np.arange(m) / s.SR
        env = np.sin(math.pi * tt / d) ** 2
        pulse = (0.5 + 0.5 * np.sin(2.0 * math.pi * flutter * tt)) ** 3
        snore = unit(s.lowpass(rng.standard_normal(m) * pulse, 700.0)) * 0.05
        snore += np.sin(2.0 * math.pi * 95.0 * tt) * pulse * 0.02
        track.add(snore * env * gain, offset + at)
        # Breathing out: air, softer and longer.
        d2 = 2.0
        m2 = s.seconds(d2)
        t2 = np.arange(m2) / s.SR
        env2 = np.sin(math.pi * t2 / d2) ** 2
        track.add(unit(band(rng.standard_normal(m2), 300.0, 1800.0)) * env2 * 0.022 * gain, offset + at + 1.8)


BPM = 120.0
CHORDS = (("C3", "E3", "G3"), ("A2", "C3", "E3"), ("F2", "A2", "C3"), ("G2", "B2", "D3"))
ROOTS = ("C2", "A1", "F1", "G1")


def dance(track, offset, length):
    rng = np.random.default_rng(606)
    beat = 60.0 / BPM
    eighth = beat / 2.0
    bar = beat * 4.0
    for b, (chord, root) in enumerate(zip(CHORDS, ROOTS)):
        b0 = offset + b * bar
        freqs = [s.hz(name) for name in chord]
        track.add(s.chorus_pad(freqs, bar, cutoff=900.0, env=(0.05, 0.2, 0.7, 0.3), gain=0.05), b0)
        low = s.hz(root)
        for k in range(8):                                                   # the bass, on the eighths
            f = low * (2.0 if k in (2, 5) else 1.5 if k == 7 else 1.0)
            track.add(s.note("saw", f, 0.22, env=(0.005, 0.08, 0.6, 0.06), cutoff=500.0, gain=0.22), b0 + k * eighth)
        for k in range(4):
            at = b0 + k * beat
            track.add(glide(150.0, 45.0, 0.3, 0.12) * 0.9, at)               # the kick, every beat
            if k in (1, 3):                                                   # the clap, on two and four
                clap = np.zeros(s.seconds(0.25))
                for j in range(3):
                    hit = burst(rng, 0.2, 1000.0, 5000.0, 0.0005, 0.03 if j < 2 else 0.08)
                    start = s.seconds(j * 0.01)
                    clap[start:start + len(hit)] += hit[:len(clap) - start]
                clap += np.concatenate([glide(190.0, 180.0, 0.1, 0.05), np.zeros(len(clap) - s.seconds(0.1))])[:len(clap)] * 0.4
                track.add(clap * 0.35, at, pan=0.05)
            track.add(burst(rng, 0.06, 7000.0, 14000.0, 0.0003, 0.02) * 0.06, at, pan=0.3)            # the hat
            track.add(burst(rng, 0.06, 7000.0, 14000.0, 0.0003, 0.02) * 0.12, at + eighth, pan=0.3)
        for stab in (1.5, 3.5):                                               # the stabs, on the and of two and four
            for i, f in enumerate(freqs):
                note = s.note("saw", f * 2.0, 0.2, env=(0.004, 0.06, 0.4, 0.1), cutoff=1800.0, gain=0.08)
                track.add(note, b0 + stab * beat, pan=-0.25 + 0.25 * i)


# Each loop's length in seconds (a whole number of bars for the dance), its render and its loudness in dBFS RMS.
LOOPS = {
    "PrepareSnack": (8.0, prepare_snack, -27.0),
    "Cook": (8.0, cook, -25.0),
    "Swim": (8.0, swim, -25.0),
    "Soak": (10.0, soak, -25.0),
    "Sleep": (9.0, sleep, -32.0),
    "Dance": (8.0, dance, -21.0),
}

if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    out = os.path.abspath(args[0] if args else "Assets/Gamesim/Resources/Audio/Activities")
    for name, (length, render, loudness) in LOOPS.items():
        stereo = s.loop_twice(lambda track, offset, r=render, l=length: r(track, offset, l), length)
        stereo = at_level(stereo, loudness)
        print(name, "->", s.write_wav(os.path.join(out, name + ".wav"), stereo))
