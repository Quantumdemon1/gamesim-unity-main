"""The theme, the season bed, the competitions' bed and the main menu's title loop (MASTER-PLAN §3.C),
rendered as seamless loops.

    blender --background --python ArtSource/audio/bb_music.py -- Assets/Gamesim/Resources/Audio

or under plain Python with numpy, naming the loops to render (all four when none is named):

    python ArtSource/audio/bb_music.py Assets/Gamesim/Resources/Audio Competition

Theme.wav: C major at 120, a fanfare that opens on the tonic - a pad, a bass on the roots, an
arpeggio in eighths and a lead phrase - sixteen seconds, so the opening has something to follow.
Season.wav: A minor at 84, slower and it never resolves - the pad, a low drone, and plucked notes
from a fixed pattern - about forty-six seconds, because it plays for hours.
Competition.wav: E minor at 128, sixteen bars, exactly thirty seconds - a kick on every beat, a clap
on two and four, hats, a bass pulsing in eighths, a pad and an ostinato in sixteenths. Eight bars
circle E minor; the next eight lift (sixteenth hats, stabs, a brighter ostinato), climb and end on B
with a clap fill, which leads back into the top of the loop. It plays under a competition, from the
attempt opening or the show starting to the result card.
Title.wav: the theme slowed to 80 and softened - its chords on a pad, its arpeggio in quiet eighths and
its phrase on a soft lead, answered - twelve bars, exactly thirty-six seconds, about the theme's
loudness. It plays on the main menu only, at 60 percent of the theme's gain.

HouseAudio loads Audio/Theme, Audio/Season, Audio/Competition and Audio/Title from Resources and falls
back to its synthesized chords when they are absent; these files are what makes HasRecordedMusic,
HasRecordedCompetition and HasRecordedTitle true.
"""
import math
import os
import sys

import numpy as np

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


# The competitions' bed: its tempo; its length in bars (sixteen at 128 is exactly thirty seconds); its
# loudness, RMS over both channels in dBFS - about the season bed's (-13.6), a little over it for its drive,
# since it plays at the bed's gain (MusicFade.CompetitionPeak); and the highest its peak may reach, in dBFS,
# the cues' own.
COMPETITION_BPM = 128.0
COMPETITION_BARS = 16
COMPETITION_RMS_DB = -13.0
COMPETITION_PEAK_DB = -1.5

# One chord a bar, (pad, bass): eight bars that circle E minor, then eight that lift, climb and end on B.
COMPETITION_CHORDS = (
    (("E3", "G3", "B3"), "E2"), (("E3", "G3", "B3"), "E2"), (("C3", "E3", "G3"), "C2"), (("D3", "F#3", "A3"), "D2"),
    (("E3", "G3", "B3"), "E2"), (("E3", "G3", "B3"), "E2"), (("C3", "E3", "G3"), "C2"), (("D3", "F#3", "A3"), "D2"),
    (("A2", "C3", "E3"), "A1"), (("A2", "C3", "E3"), "A1"), (("C3", "E3", "G3"), "C2"), (("D3", "F#3", "A3"), "D2"),
    (("E3", "G3", "B3"), "E2"), (("C3", "E3", "G3"), "C2"), (("B2", "D#3", "F#3"), "B1"), (("B2", "D#3", "F#3"), "B1"),
)


def kick(dur=0.32):
    """A kick: a sine falling from 150 Hz to 45 Hz in a few hundredths of a second, and dying away."""
    n = s.seconds(dur)
    t = np.arange(n) / s.SR
    freq = 45.0 + 105.0 * np.exp(-t / 0.035)
    phase = 2.0 * math.pi * np.cumsum(freq) / s.SR
    return np.sin(phase) * np.exp(-t / 0.11) * np.minimum(1.0, t / 0.002)


def noise_hit(seed, dur, low, high, tau):
    """A burst of noise between low and high Hz (either may be None), dying away over tau seconds.
    Its own generator, seeded here, so every render is the same file."""
    rng = np.random.default_rng(seed)
    n = s.seconds(dur)
    t = np.arange(n) / s.SR
    x = rng.standard_normal(n)
    if high:
        x = s.lowpass(x, high)
    if low:
        x = s.highpass(x, low)
    x = x / max(float(np.max(np.abs(x))), 1e-9)
    return x * np.exp(-t / tau) * np.minimum(1.0, t / 0.0005)


def clap():
    """Three quick bursts with a short body under them: the clap on two and four."""
    out = np.zeros(s.seconds(0.3))
    for j in range(3):
        hit = noise_hit(1283 + j, 0.25, 900.0, 6500.0, 0.03 if j < 2 else 0.09)
        start = s.seconds(j * 0.011)
        out[start:start + len(hit)] += hit[:len(out) - start]
    n = s.seconds(0.12)
    t = np.arange(n) / s.SR
    out[:n] += np.sin(2.0 * math.pi * 185.0 * t) * np.exp(-t / 0.04) * 0.5
    return out


def competition(track, offset):
    beat = 60.0 / COMPETITION_BPM
    eighth, sixteenth, bar = beat / 2.0, beat / 4.0, beat * 4.0
    thump, snap = kick(), clap()
    hat = noise_hit(1281, 0.08, 7000.0, None, 0.022)
    crash = noise_hit(1282, 1.6, 4500.0, None, 0.45)
    for index, (pad, root) in enumerate(COMPETITION_CHORDS):
        b0 = offset + index * bar
        lift = index >= COMPETITION_BARS // 2
        freqs = [s.hz(name) for name in pad]
        track.add(s.chorus_pad(freqs, bar, cutoff=1500.0 if lift else 1100.0, env=(0.03, 0.3, 0.75, 0.2), gain=0.08), b0)
        if index % 8 == 0:                                                   # a crash at the top of each half
            track.add(crash * 0.07, b0, pan=-0.2)
        low = s.hz(root)
        for k in range(8):                                                   # the bass, pulsing in eighths
            f = low * (2.0 if k in (3, 7) else 1.0)
            track.add(s.note("saw", f, eighth * 0.85, env=(0.004, 0.05, 0.6, 0.04), cutoff=650.0, gain=0.26), b0 + k * eighth)
        for k in range(4):
            at = b0 + k * beat
            track.add(thump * 0.8, at)                                       # the kick, every beat
            if k in (1, 3):
                track.add(snap * 0.3, at, pan=0.05)                          # the clap, on two and four
            track.add(hat * 0.11, at + eighth, pan=0.3)                      # the hat, off the beat
            if lift:                                                         # and in sixteenths in the lift
                track.add(hat * 0.05, at + sixteenth, pan=-0.3)
                track.add(hat * 0.05, at + 3.0 * sixteenth, pan=-0.3)
        tones = freqs + [freqs[0] * 2.0]
        for k in range(16):                                                  # the ostinato, in sixteenths
            f = tones[(0, 1, 2, 3, 2, 1, 2, 3)[k % 8]] * 2.0
            track.add(s.note("square", f, sixteenth * 0.9, env=(0.002, 0.04, 0.3, 0.03),
                             cutoff=2600.0 if lift else 1700.0, gain=0.045), b0 + k * sixteenth, pan=(-0.35 if k % 2 else 0.35))
        if lift:
            for stab in (1.5, 3.5):                                          # stabs on the and of two and four
                for i, f in enumerate(freqs):
                    track.add(s.note("saw", f * 2.0, 0.2, env=(0.004, 0.06, 0.4, 0.1), cutoff=1800.0, gain=0.05),
                              b0 + stab * beat, pan=-0.25 + 0.25 * i)
        if index == COMPETITION_BARS - 1:                                    # the fill back to the top
            for k in range(8):
                track.add(snap * (0.10 + 0.025 * k), b0 + 2.0 * beat + k * sixteenth, pan=0.05)


# The main menu's title loop: the theme's chords and its lead, slowed to 80 and softened - a pad, a
# sine bass on the roots, the theme's arpeggio pattern in quiet eighths and its phrase on a soft lead.
# Three bars a chord, twelve bars, exactly thirty-six seconds. Its loudness is about the theme's (-17.2,
# measured whole), since the game plays it at 60 percent of the theme's gain (MusicFade.TitlePeak).
TITLE_BPM = 80.0
TITLE_BARS_PER_CHORD = 3
TITLE_RMS_DB = -17.2
TITLE_PEAK_DB = -3.0

# (pad, bass, arpeggio): the theme's own voicings, C - Am - F - G.
TITLE_CHORDS = (
    (("C3", "E3", "G3", "C4"), "C2", ("C4", "E4", "G4", "C5")),
    (("A2", "C3", "E3", "A3"), "A1", ("A3", "C4", "E4", "A4")),
    (("F2", "A2", "C3", "F3"), "F1", ("F3", "A3", "C4", "F4")),
    (("G2", "B2", "D3", "G3"), "G1", ("G3", "B3", "D4", "G4")),
)

# The lead, (note, beat, beats): the theme's phrase half as fast again over C and Am, answered over F
# and G, ending on D to lead back into the E at the top.
TITLE_LEAD = (
    ("E5", 0.0, 2.25), ("G5", 2.25, 0.75), ("C6", 3.0, 3.0), ("B5", 6.0, 1.5), ("A5", 7.5, 1.5), ("G5", 9.0, 3.0),
    ("F5", 12.0, 2.25), ("A5", 14.25, 0.75), ("C6", 15.0, 1.5), ("A5", 16.5, 1.5), ("G5", 18.0, 4.5), ("E5", 22.5, 1.5),
    ("A5", 24.0, 3.0), ("C6", 27.0, 1.5), ("A5", 28.5, 1.5), ("G5", 30.0, 6.0),
    ("B5", 36.0, 3.0), ("D6", 39.0, 1.5), ("B5", 40.5, 1.5), ("G5", 42.0, 4.5), ("D5", 46.5, 1.5),
)


def title(track, offset):
    beat = 60.0 / TITLE_BPM
    bar = 4 * beat
    span = TITLE_BARS_PER_CHORD * bar
    pattern = (0, 1, 2, 3, 2, 1, 0, 2)                                       # the theme's arpeggio
    for index, (pad, bass, arp) in enumerate(TITLE_CHORDS):
        start = offset + index * span
        # The pad held a little past its chord, so its release crosses the next one's rise.
        track.add(s.chorus_pad([s.hz(n) for n in pad], span + 1.2, cutoff=1000.0, env=(1.2, 0.8, 0.85, 1.6), gain=0.14), start)
        for b in range(TITLE_BARS_PER_CHORD):                                # the bass: the root, once a bar
            track.add(s.note("sine", s.hz(bass) * 2.0, bar * 0.95, env=(0.08, 0.4, 0.8, 0.6), gain=0.22), start + b * bar)
        for eighth in range(TITLE_BARS_PER_CHORD * 8):                       # the arpeggio, in quiet eighths
            f = s.hz(arp[pattern[eighth % 8]])
            track.add(s.note("tri", f, beat, env=(0.006, 0.3, 0.15, 0.35), cutoff=2000.0, gain=0.07),
                      start + eighth * beat / 2.0, pan=(-0.3 if eighth % 2 else 0.3))
    for name, at, dur in TITLE_LEAD:
        f = s.hz(name)
        m = s.note("sine", f, dur * beat, env=(0.08, 0.3, 0.75, 0.6), gain=0.20) \
            + s.note("tri", f, dur * beat, env=(0.08, 0.3, 0.75, 0.6), cutoff=1800.0, gain=0.05)
        track.add(m, offset + at * beat, pan=0.1)


def at_level(stereo, rms_db, ceiling_db):
    """The loop scaled so its RMS is rms_db, unless that would put its peak over ceiling_db."""
    rms = float(np.sqrt(np.mean(stereo ** 2)))
    peak = float(np.max(np.abs(stereo)))
    if rms <= 0.0 or peak <= 0.0:
        return stereo
    return stereo * min(10.0 ** (rms_db / 20.0) / rms, 10.0 ** (ceiling_db / 20.0) / peak)


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


def render_competition():
    length = COMPETITION_BARS * 4 * 60.0 / COMPETITION_BPM
    n = s.seconds(length)
    # Rendered twice end to end, with the room put on both passes before the second is kept: the end's
    # tails and its echo sit under the start, so the loop point is not a click.
    track = s.Track(length * 2.0)
    competition(track, 0.0)
    competition(track, length)
    track.process(lambda ch: s.reverb(ch, size=0.35, mix=0.14))
    stereo = track.stereo()[n:n * 2]
    # A soft saturation glues the drums to the rest, so the kick's peaks do not set the level.
    stereo = np.tanh(2.0 * stereo / float(np.max(np.abs(stereo)))) / math.tanh(2.0)
    return at_level(stereo, COMPETITION_RMS_DB, COMPETITION_PEAK_DB)


def render_title():
    length = len(TITLE_CHORDS) * TITLE_BARS_PER_CHORD * 4 * 60.0 / TITLE_BPM
    n = s.seconds(length)
    # Twice end to end with the room on both passes, the second kept, as the competitions' loop is.
    track = s.Track(length * 2.0)
    title(track, 0.0)
    title(track, length)
    track.process(lambda ch: s.reverb(ch, size=0.8, mix=0.3))
    stereo = track.stereo()[n:n * 2]
    return at_level(stereo, TITLE_RMS_DB, TITLE_PEAK_DB)


RENDERS = (("Theme", render_theme), ("Season", render_season), ("Competition", render_competition), ("Title", render_title))

if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    out = os.path.abspath(args[0] if args else "Assets/Gamesim/Resources/Audio")
    wanted = args[1:] or [name for name, _ in RENDERS]
    unknown = [name for name in wanted if name not in dict(RENDERS)]
    if unknown:
        sys.exit("unknown loop: " + ", ".join(unknown) + " (Theme, Season, Competition or Title)")
    for name, render in RENDERS:
        if name in wanted:
            print(name.lower(), "->", s.write_wav(os.path.join(out, name + ".wav"), render()))
