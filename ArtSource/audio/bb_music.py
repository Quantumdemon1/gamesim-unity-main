"""The theme and the season bed (MASTER-PLAN §3.C), rendered as seamless loops.

    blender --background --python ArtSource/audio/bb_music.py -- Assets/Gamesim/Resources/Audio

Theme.wav: C major at 120, a fanfare that opens on the tonic - a pad, a bass on the roots, an
arpeggio in eighths and a lead phrase - sixteen seconds, so the opening has something to follow.
Season.wav: A minor at 84, slower and it never resolves - the pad, a low drone, and plucked notes
from a fixed pattern - about forty-six seconds, because it plays for hours.

HouseAudio loads Audio/Theme and Audio/Season from Resources and falls back to its synthesised
chords when they are absent; these files are what makes HasRecordedMusic true.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bb_synth as s  # noqa: E402


def theme(track, offset):
    bpm = 120.0
    beat = 60.0 / bpm
    bar = 4 * beat
    chords = [  # two bars each: C - Am - F - G
        (["C3", "E3", "G3", "C4"], "C2", ["C4", "E4", "G4", "C5"]),
        (["A2", "C3", "E3", "A3"], "A1", ["A3", "C4", "E4", "A4"]),
        (["F2", "A2", "C3", "F3"], "F1", ["F3", "A3", "C4", "F4"]),
        (["G2", "B2", "D3", "G3"], "G1", ["G3", "B3", "D4", "G4"]),
    ]
    for index, (pad, bass, arp) in enumerate(chords):
        start = offset + index * 2 * bar
        track.add(s.chorus_pad([s.hz(n) for n in pad], 2 * bar, cutoff=1600.0, gain=0.16), start, pan=0.0)
        # Bass: root on beats one and three, an octave up on four.
        for b, name, dur in ((0, bass, 2), (2, bass, 1), (3, bass, 1)):
            for half in (0, 1):
                t = start + half * bar + b * beat
                f = s.hz(name) * (2.0 if b == 3 else 1.0)
                track.add(s.note("saw", f, dur * beat * 0.95, env=(0.005, 0.08, 0.6, 0.12), cutoff=420.0, gain=0.5), t)
        # Arpeggio: eighths, up and over.
        pattern = [0, 1, 2, 3, 2, 1, 0, 2]
        for eighth in range(16):
            t = start + eighth * beat / 2
            f = s.hz(arp[pattern[eighth % 8]])
            track.add(s.note("tri", f, beat * 0.5, env=(0.003, 0.05, 0.4, 0.1), gain=0.22), t, pan=(-0.4 if eighth % 2 else 0.4))
    # Lead: a phrase over the first four bars, answered over the last four.
    lead = [("E5", 0, 1.5), ("G5", 1.5, 0.5), ("C6", 2, 2), ("B5", 4, 1), ("A5", 5, 1), ("G5", 6, 2),
            ("F5", 8, 1.5), ("A5", 9.5, 0.5), ("C6", 10, 1), ("A5", 11, 1), ("G5", 12, 3), ("E5", 15, 1)]
    for name, at, dur in lead:
        m = s.note("sine", s.hz(name), dur * beat, env=(0.02, 0.15, 0.7, 0.25), gain=0.30) \
            + s.note("tri", s.hz(name), dur * beat, env=(0.02, 0.15, 0.7, 0.25), gain=0.10)
        track.add(m, offset + at * beat, pan=0.1)


def season(track, offset):
    bpm = 84.0
    beat = 60.0 / bpm
    bar = 4 * beat
    chords = [  # four bars each: Am - F - G - Em, and back to Am without resolving
        (["A2", "C3", "E3", "A3"], "A1"),
        (["F2", "A2", "C3", "F3"], "F1"),
        (["G2", "B2", "D3", "G3"], "G1"),
        (["E2", "G2", "B2", "E3"], "E1"),
    ]
    pluck_scale = ["A3", "C4", "D4", "E4", "G4", "A4", "C5", "D5", "E5"]
    # A fixed pattern (a small LCG) so the bed is the same file every render.
    seed = 20260919
    for index, (pad, drone) in enumerate(chords):
        start = offset + index * 4 * bar
        cutoff = 900.0 + 250.0 * (index % 2)
        track.add(s.chorus_pad([s.hz(n) for n in pad], 4 * bar, cutoff=cutoff, env=(1.2, 0.6, 0.85, 1.4), gain=0.15), start)
        track.add(s.note("sine", s.hz(drone), 4 * bar, env=(0.8, 0.4, 0.9, 1.0), gain=0.28), start)
        for eighth in range(32):
            seed = (seed * 1103515245 + 12345) & 0x7FFFFFFF
            if (seed >> 8) % 100 < 38:
                continue
            seed = (seed * 1103515245 + 12345) & 0x7FFFFFFF
            name = pluck_scale[(seed >> 8) % len(pluck_scale)]
            t = start + eighth * beat / 2
            pan = -0.5 + ((seed >> 16) % 100) / 100.0
            track.add(s.note("tri", s.hz(name), beat * 1.5, env=(0.002, 0.25, 0.0, 0.3), gain=0.16), t, pan=pan)


def render_theme():
    length = 16.0
    stereo = s.loop_twice(theme, length)
    stereo = np.apply_along_axis(lambda ch: s.reverb(ch, size=0.5, mix=0.22), 0, stereo)
    return s.normalize(stereo, -3.0)


def render_season():
    length = 4 * 4 * (60.0 / 84.0) * 4  # four chords of four bars
    stereo = s.loop_twice(season, length)
    stereo = np.apply_along_axis(lambda ch: s.reverb(ch, size=0.8, mix=0.3), 0, stereo)
    return s.normalize(stereo, -4.0)


if __name__ == "__main__":
    import numpy as np  # noqa: F811

    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    out = os.path.abspath(args[0] if args else "Assets/Gamesim/Resources/Audio")
    print("theme ->", s.write_wav(os.path.join(out, "Theme.wav"), render_theme()))
    print("season ->", s.write_wav(os.path.join(out, "Season.wav"), render_season()))
