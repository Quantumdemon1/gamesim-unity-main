"""Room tone per room (MASTER-PLAN §3.C): eight quiet loops, one per HouseRoomMarker name, that
HouseAudio plays under whichever room the camera looks into.

    blender --background --python ArtSource/audio/bb_rooms.py -- Assets/Gamesim/Resources/Audio/Rooms

Each is twelve seconds, rendered twice with the second pass kept so the seam is silent, and quiet
on purpose: a bed is what you notice when it stops. Kitchen: a fridge hum and a slow drip. Living:
soft air and a low mains hum. Bedroom: the quietest air. Private: air and a clock. HoH: warm air and
a faint low chord. Nomination: a low drone that never settles. Games: a fluorescent buzz and an
arcade flicker. Yard: wind in leaves and birds.
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bb_synth as s  # noqa: E402

LENGTH = 12.0


def air(track, offset, cutoff=600.0, gain=0.05, seed=3.0):
    n = s.seconds(LENGTH)
    x = s.lowpass(s.osc("noise", seed, n), cutoff) * gain
    track.add(x, offset)


def hum(track, offset, freq=50.0, gain=0.04, harmonics=(1, 2, 3)):
    n = s.seconds(LENGTH)
    x = np.zeros(n)
    for k in harmonics:
        x += s.osc("sine", freq * k, n) / k
    track.add(x * gain, offset)


def blips(track, offset, times, freq, dur, gain, kind="sine", pan=0.0):
    for t in times:
        track.add(s.note(kind, freq, dur, env=(0.002, dur * 0.3, 0.0, dur * 0.4), gain=gain), offset + t, pan=pan)


def kitchen(track, offset):
    air(track, offset, 500.0, 0.05, 11.0)
    hum(track, offset, 60.0, 0.05, (1, 2, 4))
    blips(track, offset, [1.3, 4.9, 8.2, 10.7], 2400.0, 0.06, 0.08, pan=0.4)          # a drip
    blips(track, offset, [6.1], 3200.0, 0.05, 0.05, pan=-0.3)


def living(track, offset):
    air(track, offset, 700.0, 0.06, 5.0)
    hum(track, offset, 50.0, 0.035)


def bedroom(track, offset):
    air(track, offset, 400.0, 0.04, 7.0)


def private(track, offset):
    air(track, offset, 550.0, 0.045, 13.0)
    blips(track, offset, [t * 0.5 for t in range(24)], 1800.0, 0.02, 0.05, pan=0.5)     # a clock


def hoh(track, offset):
    air(track, offset, 650.0, 0.05, 17.0)
    hum(track, offset, 55.0, 0.03)
    track.add(s.chorus_pad([s.hz("A2"), s.hz("E3")], LENGTH, cutoff=500.0, env=(3.0, 1.0, 0.8, 3.0), gain=0.03), offset)


def nomination(track, offset):
    air(track, offset, 450.0, 0.04, 19.0)
    track.add(s.chorus_pad([s.hz("E2"), s.hz("Bb2")], LENGTH, cutoff=380.0, env=(2.0, 1.0, 0.85, 2.0), gain=0.05), offset)


def games(track, offset):
    air(track, offset, 900.0, 0.04, 23.0)
    hum(track, offset, 120.0, 0.03, (1, 3, 5))                                             # a fluorescent buzz
    blips(track, offset, [0.7, 2.2, 3.1, 5.6, 7.4, 9.9, 11.2], 880.0, 0.05, 0.04, kind="square", pan=-0.5)


def yard(track, offset):
    n = s.seconds(LENGTH)
    wind = s.lowpass(s.osc("noise", 29.0, n), 350.0)
    t = np.arange(n) / s.SR
    swell = 0.6 + 0.4 * np.sin(2.0 * np.pi * t / 7.0)
    track.add(wind * swell * 0.09, offset)
    for i, at in enumerate([0.9, 2.6, 4.1, 6.8, 8.3, 10.4]):
        f = 2600.0 + 400.0 * (i % 3)
        blips(track, offset, [at, at + 0.12, at + 0.24], f, 0.08, 0.06, pan=(-0.6 if i % 2 else 0.6))


ROOMS = {
    "Kitchen": kitchen, "Living": living, "Bedroom": bedroom, "Private": private,
    "HoH": hoh, "Nomination": nomination, "Games": games, "Yard": yard,
}

if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    out = os.path.abspath(args[0] if args else "Assets/Gamesim/Resources/Audio/Rooms")
    for name, render in ROOMS.items():
        stereo = s.loop_twice(render, LENGTH)
        stereo = s.normalize(stereo, -14.0)
        print(name, "->", s.write_wav(os.path.join(out, name + ".wav"), stereo))
