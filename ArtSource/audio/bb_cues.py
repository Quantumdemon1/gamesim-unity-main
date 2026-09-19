"""The cue set keyed to HouseAudio.Cue (MASTER-PLAN §3.C): one file per cue, named for it.

    blender --background --python ArtSource/audio/bb_cues.py -- Assets/Gamesim/Resources/Audio/Cues

HouseAudio loads Audio/Cues/<Cue> from Resources and falls back to its two-tone composition when
a file is absent, so a cue can be re-rendered or replaced one at a time. The shapes follow the
compositions they replace - the same intervals and lengths, the button still a click - because the
cues are already tuned to the beats they mark; what changes is that each is a designed sound.
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bb_synth as s  # noqa: E402


def click(freq=1200.0, dur=0.07):
    n = s.seconds(dur)
    body = s.note("sine", freq, dur, env=(0.001, 0.02, 0.0, 0.03), gain=0.6)
    tick = s.highpass(s.osc("noise", 7.0, n), 2500.0) * s.adsr(n, 0.0005, 0.012, 0.0, 0.01) * 0.35
    return body + tick


def chime(names, step, dur, kind="sine", env=(0.004, 0.12, 0.35, 0.18), gain=0.5, shimmer=0.25):
    total = step * (len(names) - 1) + dur
    track = s.Track(total + 0.4)
    for index, name in enumerate(names):
        f = s.hz(name)
        m = s.note(kind, f, dur, env=env, gain=gain) + s.note("sine", f * 2.0, dur, env=env, gain=gain * shimmer)
        track.add(m, index * step, pan=-0.2 + 0.4 * (index / max(1, len(names) - 1)))
    return track


def hit(f_start, f_end, dur, noise=0.3, gain=0.9):
    """A drum-like sweep down with a noise transient."""
    n = s.seconds(dur)
    t = np.arange(n) / s.SR
    sweep = f_end + (f_start - f_end) * np.exp(-t * 18.0)
    phase = 2.0 * np.pi * np.cumsum(sweep) / s.SR
    body = np.sin(phase) * s.adsr(n, 0.001, dur * 0.5, 0.0, dur * 0.4)
    burst = s.lowpass(s.osc("noise", 3.0, n), 1800.0) * s.adsr(n, 0.0005, 0.03, 0.0, 0.02) * noise
    return (body + burst) * gain


def stereo(track_or_mono, rev=0.2, size=0.5):
    if isinstance(track_or_mono, s.Track):
        out = track_or_mono.stereo()
    else:
        out = np.stack([track_or_mono, track_or_mono], axis=1)
    if rev > 0:
        out = np.apply_along_axis(lambda ch: s.reverb(ch, size=size, mix=rev), 0, out)
    return s.normalize(out, -1.5)


def cues():
    yield "Button", stereo(click(), rev=0.0)
    yield "Save", stereo(chime(["C6", "E6"], 0.10, 0.16, gain=0.45), rev=0.15)
    yield "SocialUp", stereo(chime(["E4", "C5"], 0.10, 0.26, kind="tri", gain=0.45), rev=0.25)
    yield "SocialDown", stereo(chime(["C5", "E4"], 0.10, 0.26, kind="sine", gain=0.45, shimmer=0.1), rev=0.25)
    # Competition begins: a low hit, then a riser that lands on the fifth.
    start = s.Track(0.9)
    start.add(hit(180.0, 55.0, 0.5), 0.0)
    n = s.seconds(0.6)
    t = np.arange(n) / s.SR
    riser = np.sin(2.0 * np.pi * np.cumsum(220.0 * (1.0 + 0.5 * t / 0.6)) / s.SR) * s.adsr(n, 0.05, 0.1, 0.8, 0.2) * 0.35
    start.add(riser, 0.12, pan=0.2)
    yield "CompetitionStart", stereo(start, rev=0.2, size=0.7)
    yield "CompetitionWin", stereo(chime(["C5", "E5", "G5", "C6"], 0.14, 0.7, kind="tri", env=(0.004, 0.2, 0.5, 0.3), gain=0.4), rev=0.3, size=0.7)
    # Nomination: a low two-note under a slow attack; ominous, not loud.
    nom = s.Track(1.1)
    nom.add(s.note("saw", s.hz("A2"), 0.8, env=(0.15, 0.2, 0.7, 0.3), cutoff=500.0, gain=0.5), 0.0)
    nom.add(s.note("saw", s.hz("Bb2"), 0.55, env=(0.1, 0.15, 0.7, 0.25), cutoff=450.0, gain=0.45), 0.35, pan=0.15)
    yield "Nomination", stereo(nom, rev=0.3, size=0.8)
    yield "Veto", stereo(chime(["G4", "B4", "D5", "G5"], 0.09, 0.45, kind="tri", env=(0.003, 0.1, 0.6, 0.25), gain=0.4), rev=0.22, size=0.6)
    vote = s.Track(0.3)
    vote.add(hit(500.0, 240.0, 0.18, noise=0.5, gain=0.6), 0.0)
    vote.add(s.note("sine", 880.0, 0.16, env=(0.002, 0.06, 0.3, 0.08), gain=0.35), 0.01)
    yield "Vote", stereo(vote, rev=0.12)
    ev = s.Track(1.3)
    ev.add(hit(110.0, 41.0, 0.9, noise=0.35, gain=1.0), 0.0)
    ev.add(s.note("sine", s.hz("E2"), 0.9, env=(0.01, 0.3, 0.5, 0.5), gain=0.5), 0.05)
    ev.add(s.note("sine", s.hz("E3"), 0.6, env=(0.12, 0.2, 0.4, 0.25), gain=0.25), 0.12, pan=0.2)
    yield "Eviction", stereo(ev, rev=0.35, size=0.9)
    fin = s.Track(1.9)
    for index, name in enumerate(["C4", "E4", "G4", "C5", "E5"]):
        fin.add(s.chorus_pad([s.hz(name)], 1.5 - index * 0.1, cutoff=2200.0, env=(0.02, 0.2, 0.7, 0.5), gain=0.5), index * 0.08, pan=-0.3 + 0.15 * index)
    fin.add(s.note("sine", s.hz("C6"), 0.8, env=(0.01, 0.2, 0.6, 0.4), gain=0.25), 0.55)
    yield "Finale", stereo(fin, rev=0.35, size=0.9)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    out = os.path.abspath(args[0] if args else "Assets/Gamesim/Resources/Audio/Cues")
    for name, data in cues():
        print(name, "->", s.write_wav(os.path.join(out, name + ".wav"), data), round(len(data) / s.SR, 2), "s")
