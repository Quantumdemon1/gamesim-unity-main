"""The player's footsteps: four takes of a step on each ground the house has, named Surface_1 to
Surface_4 (HouseSteps.StepSurface), that HouseFootsteps plays in turn as the player walks.

    python ArtSource/audio/bb_steps.py Assets/Gamesim/Resources/Audio/Steps

or under Blender's Python, as the other renders:

    blender --background --python ArtSource/audio/bb_steps.py -- Assets/Gamesim/Resources/Audio/Steps

Floor: a shoe on the wooden floors, a soft heel and a lighter roll onto the toe, in a small room.
Tile: the kitchen's and the games room's tile, harder and brighter, in a room that rings a little.
Grass: the yard's lawn, a soft press and a brush of blades with a few crackles, out of doors.
Deck: the pool's raised deck, hollow wood with a short ring under it, out of doors.

Each take is a heel strike and a roll onto the toe a few hundredths of a second later, made of a low
thump, a knock of band noise and a click, varied take to take; a step, not a loop, 0.4 s long with
its tail faded out. Each is set to its ground's loudness over its loudest tenth of a second with its
peak held under PEAK_CEILING: over the room's bed, under the game's news, the lawn softest.

The randomness is numpy's, seeded here per take: the same files on every render, and nothing to do
with any season's draws.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bb_synth as s  # noqa: E402

# The highest a step's peak may reach, in dBFS.
PEAK_CEILING = -6.0
# Every take's length, in seconds, and the fade over its last part.
LENGTH = 0.40
FADE = 0.03
# How many takes of each ground.
TAKES = 4


def band_noise(rng, n, low, high):
    """Band-limited noise at unit peak, filtered before it is shaped so the FFT's wrap cannot reach the step."""
    x = s.highpass(s.lowpass(rng.standard_normal(n * 2), high), low)[:n]
    peak = float(np.max(np.abs(x)))
    return x / peak if peak > 0.0 else x


def envelope(n, attack, tau):
    """A rise over attack seconds and a fall with time constant tau."""
    t = np.arange(n) / s.SR
    return (1.0 - np.exp(-t / max(attack, 1e-4))) * np.exp(-t / tau)


def knock(rng, low, high, attack, tau):
    """A burst of band noise: the knock of a sole, the click of a heel, the brush of grass."""
    n = s.seconds(min(LENGTH, tau * 8.0 + attack * 4.0 + 0.01))
    x = band_noise(rng, n, low, high) * envelope(n, attack, tau)
    peak = float(np.max(np.abs(x)))
    return x / peak if peak > 0.0 else x


def thump(f_start, f_end, tau):
    """The low weight of the body landing: a sine falling from f_start toward f_end."""
    n = s.seconds(min(LENGTH, tau * 8.0))
    t = np.arange(n) / s.SR
    f = f_end + (f_start - f_end) * np.exp(-t * 30.0)
    phase = 2.0 * math.pi * np.cumsum(f) / s.SR
    return np.sin(phase) * envelope(n, 0.002, tau)


def ring(freq, tau):
    """A board ringing under the foot."""
    n = s.seconds(min(LENGTH, tau * 8.0))
    t = np.arange(n) / s.SR
    return np.sin(2.0 * math.pi * freq * t) * envelope(n, 0.001, tau)


def place(out, sound, at, gain):
    start = s.seconds(at)
    end = min(len(out), start + len(sound))
    if end > start:
        out[start:end] += sound[:end - start] * gain


def floor(rng):
    out = np.zeros(s.seconds(LENGTH))
    heel = rng.uniform(0.003, 0.006)
    toe = heel + rng.uniform(0.07, 0.095)
    place(out, thump(rng.uniform(130.0, 150.0), rng.uniform(70.0, 85.0), 0.035), heel, 0.55)
    place(out, knock(rng, 250.0, rng.uniform(1700.0, 2300.0), 0.0006, rng.uniform(0.015, 0.02)), heel, 0.5)
    place(out, knock(rng, 2500.0, 7000.0, 0.0002, 0.004), heel, 0.14)
    place(out, knock(rng, 400.0, 2500.0, 0.0008, 0.012), toe, rng.uniform(0.25, 0.35))
    place(out, knock(rng, 3000.0, 8000.0, 0.0002, 0.003), toe, 0.07)
    return s.reverb(out, size=0.3, mix=0.12)


def tile(rng):
    out = np.zeros(s.seconds(LENGTH))
    heel = rng.uniform(0.003, 0.006)
    toe = heel + rng.uniform(0.06, 0.08)
    place(out, thump(rng.uniform(170.0, 190.0), rng.uniform(95.0, 110.0), 0.025), heel, 0.38)
    place(out, knock(rng, 600.0, rng.uniform(3600.0, 4400.0), 0.0004, rng.uniform(0.009, 0.012)), heel, 0.55)
    place(out, knock(rng, 3000.0, 10000.0, 0.0002, 0.003), heel, 0.3)
    place(out, knock(rng, 800.0, 5000.0, 0.0004, 0.008), toe, rng.uniform(0.3, 0.4))
    place(out, knock(rng, 4000.0, 11000.0, 0.0002, 0.002), toe, 0.15)
    return s.reverb(out, size=0.45, mix=0.2)


def grass(rng):
    out = np.zeros(s.seconds(LENGTH))
    heel = rng.uniform(0.004, 0.008)
    toe = heel + rng.uniform(0.08, 0.11)
    place(out, thump(rng.uniform(85.0, 95.0), rng.uniform(50.0, 60.0), 0.04), heel, 0.35)
    place(out, knock(rng, 700.0, rng.uniform(4500.0, 5500.0), 0.008, rng.uniform(0.045, 0.055)), heel, 0.6)
    place(out, knock(rng, 900.0, 6000.0, 0.006, 0.04), toe, rng.uniform(0.35, 0.45))
    # The blades giving under the sole: a scatter of small crackles over the press and the roll.
    for _ in range(int(rng.integers(8, 13))):
        at = heel + rng.uniform(0.0, toe - heel + 0.06)
        place(out, knock(rng, 2000.0, 9000.0, 0.0002, 0.0015), at, rng.uniform(0.08, 0.22))
    return out


def deck(rng):
    out = np.zeros(s.seconds(LENGTH))
    heel = rng.uniform(0.003, 0.006)
    toe = heel + rng.uniform(0.07, 0.09)
    low = rng.uniform(190.0, 230.0)
    place(out, thump(rng.uniform(115.0, 125.0), rng.uniform(70.0, 80.0), 0.05), heel, 0.55)
    place(out, ring(low, 0.07), heel, 0.35)
    place(out, ring(low * rng.uniform(2.05, 2.2), 0.04), heel, 0.15)
    place(out, knock(rng, 300.0, 2500.0, 0.0006, 0.015), heel, 0.45)
    place(out, knock(rng, 2500.0, 7000.0, 0.0002, 0.003), heel, 0.12)
    place(out, knock(rng, 400.0, 3000.0, 0.0006, 0.01), toe, rng.uniform(0.25, 0.35))
    place(out, ring(low, 0.05), toe, 0.15)
    return out


def at_level(mono, loudest_db, ceiling_db=PEAK_CEILING):
    """The take scaled so its loudest tenth of a second is loudest_db RMS, unless that would put its peak over ceiling_db."""
    window = s.seconds(0.1)
    power = np.concatenate([[0.0], np.cumsum(mono ** 2)])
    loudest = max(float(power[i + window] - power[i]) / window for i in range(0, len(mono) - window + 1, 44))
    peak = float(np.max(np.abs(mono)))
    if loudest <= 0.0 or peak <= 0.0:
        return mono
    gain = min(10.0 ** (loudest_db / 20.0) / math.sqrt(loudest), 10.0 ** (ceiling_db / 20.0) / peak)
    return mono * gain


def faded(mono):
    """The tail faded out over FADE seconds, so a take ends on silence, not a click."""
    n = s.seconds(FADE)
    out = mono.copy()
    out[-n:] *= 0.5 * (1.0 + np.cos(np.linspace(0.0, math.pi, n)))
    return out


# Each ground's render, its seed and its loudness over its loudest tenth of a second, in dBFS.
GROUNDS = {
    "Floor": (floor, 1100, -25.0),
    "Tile": (tile, 1200, -24.0),
    "Grass": (grass, 1300, -27.0),
    "Deck": (deck, 1400, -25.0),
}

if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    out = os.path.abspath(args[0] if args else "Assets/Gamesim/Resources/Audio/Steps")
    for name, (render, seed, loudness) in GROUNDS.items():
        for take in range(1, TAKES + 1):
            rng = np.random.default_rng(seed + take)
            mono = at_level(faded(render(rng)), loudness)
            stereo = np.stack([mono, mono], axis=1)
            print(name, take, "->", s.write_wav(os.path.join(out, "%s_%d.wav" % (name, take)), stereo))
