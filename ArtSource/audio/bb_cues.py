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
    # UI foley: a panel sliding open (a short upward sweep), closing (the same down), and a hover
    # (a tick softer and higher than the button, so a pass over a row is not a press).
    n = s.seconds(0.28)
    t = np.arange(n) / s.SR
    open_sweep = np.sin(2.0 * np.pi * np.cumsum(420.0 + 380.0 * t / 0.28) / s.SR) * s.adsr(n, 0.005, 0.08, 0.35, 0.12) * 0.4
    yield "PanelOpen", stereo(s.lowpass(open_sweep, 2600.0), rev=0.1)
    n = s.seconds(0.24)
    t = np.arange(n) / s.SR
    close_sweep = np.sin(2.0 * np.pi * np.cumsum(760.0 - 340.0 * t / 0.24) / s.SR) * s.adsr(n, 0.005, 0.07, 0.3, 0.1) * 0.4
    yield "PanelClose", stereo(s.lowpass(close_sweep, 2400.0), rev=0.1)
    hover = s.note("sine", 1800.0, 0.09, env=(0.001, 0.02, 0.0, 0.04), gain=0.35)
    yield "Hover", stereo(hover, rev=0.0)
    # The tour's step, as the reference build's tutorialStep (useGameSFX.ts): a sine gliding from
    # 500 to 700 Hz over 0.1 s, its gain falling exponentially from 0.15 to 0.001 - with a 2 ms
    # attack so it does not click and a 3 ms tail to silence, like the rest of the UI foley.
    dur = 0.1
    n = s.seconds(dur)
    t = np.arange(n) / s.SR
    glide = np.sin(2.0 * np.pi * np.cumsum(500.0 + 200.0 * t / dur) / s.SR)
    shape = np.minimum(1.0, t / 0.002) * np.exp(-t * np.log(150.0) / dur) * np.clip((dur - t) / 0.003, 0.0, 1.0)
    yield "TutorialStep", stereo(s.lowpass(glide * shape * 0.4, 3000.0), rev=0.0)
    # The front door (AU-06), appended: the clunk as its leaves give after the rattle, and the thud
    # as they land shut. Heavy and wooden, in a room's air, and short, so the next houseguest's
    # entrance is never under the last one's door.
    yield "DoorOpen", stereo(door_open(), rev=0.18, size=0.5)
    yield "DoorShut", stereo(door_shut(), rev=0.28, size=0.7)
    # The summons (AU-07), appended: the chime that calls the house to a staged ceremony as the strip
    # says so. Three struck bars, up a sixth and down a third, warm and unhurried - a call, not news
    # of an outcome, so nothing like the veto's or the win's runs.
    summons = s.Track(1.45)
    for index, name in enumerate(["G4", "E5", "C5"]):
        summons.add(bell(s.hz(name), 0.85 if index == 2 else 0.7, gain=0.42), index * 0.24, pan=-0.15 + 0.15 * index)
    yield "Summons", stereo(summons, rev=0.3, size=0.8)
    # A competition's count and an early press (AU-08), appended: news, so they play as recorded, and
    # each is recorded under the ballot's tick - a count comes every second and must not shout.
    yield "CountTick", level(stereo(count_tick(), rev=0.06, size=0.3), COUNT_TICK_DB)
    yield "FalseStart", level(stereo(false_start(), rev=0.08, size=0.3), FALSE_START_DB)
    # A staged competition watched in the yard (AU-13), appended: the start horn, a console locking an
    # answer in, and the reveal's right and wrong. Sounds of the apparatus on screen; their rows set
    # them under the news; the buzzer, all body, is recorded under the others' level.
    yield "ShowHorn", stereo(show_horn(), rev=0.25, size=0.8)
    yield "ShowLockIn", stereo(show_lock_in(), rev=0.08, size=0.3)
    yield "ShowRight", stereo(show_right(), rev=0.2, size=0.6)
    yield "ShowWrong", level(stereo(show_wrong(), rev=0.15, size=0.5), SHOW_WRONG_DB)
    # A conversation that moved trust (AU-09), appended: the reference build's relationshipUp and
    # relationshipDown (a sine swept between 300 and 600 Hz over 0.2 s, useGameSFX.ts), warmed into a
    # rise that lands on a soft bar and a fall that settles on a dull low one, so neither is a panel's
    # sweep. News, played as recorded, so their place in the mix is set here.
    yield "RelationshipUp", level(stereo(trust_up(), rev=0.18, size=0.5), TRUST_UP_DB)
    yield "RelationshipDown", level(stereo(trust_down(), rev=0.18, size=0.5), TRUST_DOWN_DB)


# The recorded level of a conversation's trust sounds, in decibels under the -1.5 dB peak: a conversation
# comes six times a week, so they sit under the ceremonies' news and over the interface's answers.
TRUST_UP_DB = -6.0
TRUST_DOWN_DB = -7.2


def trust_sweep(f_start, f_end, dur=0.2, cutoff=2600.0, gain=0.4):
    """The reference build's sweep: a glide from f_start to f_end over dur, exponential in pitch, its gain
    falling in a line to nothing at dur (a 3 ms attack so it does not click), on a triangle's softer edge."""
    n = s.seconds(dur)
    t = np.arange(n) / s.SR
    freq = f_start * (f_end / f_start) ** (t / dur)
    phase = 2.0 * np.pi * np.cumsum(freq) / s.SR
    triangle = 2.0 / np.pi * np.arcsin(np.sin(phase))
    shape = np.minimum(1.0, t / 0.003) * np.clip(1.0 - t / dur, 0.0, 1.0)
    return s.lowpass(triangle * shape, cutoff) * gain


def trust_up():
    """Trust gained: the sweep up from 300 to 600 Hz, landing on a soft bar at its top with the fifth over it."""
    track = s.Track(0.6)
    track.add(trust_sweep(300.0, 600.0), 0.0)
    track.add(bell(600.0, 0.32, gain=0.22), 0.15, pan=-0.08)
    track.add(bell(900.0, 0.28, gain=0.1), 0.15, pan=0.08)
    return track


def trust_down():
    """Trust lost: the sweep down from 600 to 300 Hz, duller, settling on a low bar a semitone under its foot."""
    track = s.Track(0.6)
    track.add(trust_sweep(600.0, 300.0, cutoff=1600.0), 0.0)
    track.add(bell(283.0, 0.32, gain=0.24), 0.15)
    return track


def show_horn():
    """The start horn and time called: a brassy fifth on two detuned saws with an octave under it,
    a quick swell and a held note - a game show's horn, not a fanfare."""
    track = s.Track(0.9)
    env = (0.025, 0.08, 0.85, 0.18)
    for index, name in enumerate(["Bb3", "F4"]):
        f = s.hz(name)
        horn = (s.note("saw", f, 0.7, env=env, cutoff=1700.0, gain=0.3)
                + s.note("saw", f, 0.7, env=env, detune=9.0, cutoff=1700.0, gain=0.22))
        track.add(horn, 0.0, pan=-0.12 + 0.24 * index)
    track.add(s.note("saw", s.hz("Bb2"), 0.7, env=env, cutoff=600.0, gain=0.28), 0.0)
    return track


def show_lock_in():
    """A console locking an answer in: a firm click and the catch a moment after it."""
    track = s.Track(0.12)
    n = s.seconds(0.02)
    for at, cut, gain in ((0.0, 2200.0, 0.5), (0.035, 3200.0, 0.32)):
        tick = s.highpass(s.osc("noise", 17.0 + at * 100.0, n), cut) * s.adsr(n, 0.0004, 0.006, 0.0, 0.008)
        track.add(tick * gain, at)
    track.add(s.note("sine", 1480.0, 0.06, env=(0.0005, 0.02, 0.0, 0.03), gain=0.22), 0.035)
    return track


def show_right():
    """A right answer lit: two bright struck bars up a fourth, quick and clean."""
    track = s.Track(0.7)
    track.add(bell(s.hz("A5"), 0.35, gain=0.4), 0.0, pan=-0.1)
    track.add(bell(s.hz("D6"), 0.5, gain=0.4), 0.11, pan=0.1)
    return track


def show_wrong():
    """A wrong answer lit: the buzzer, a low square beating against itself, cut off short."""
    track = s.Track(0.55)
    env = (0.006, 0.05, 0.8, 0.06)
    track.add(s.note("square", 118.0, 0.45, env=env, cutoff=900.0, gain=0.3)
              + s.note("square", 123.0, 0.45, env=env, cutoff=900.0, gain=0.24), 0.0)
    return track


# The recorded level of the count's tick and the early press, in decibels under the -1.5 dB peak every
# cue is normalized to: they are news and play as recorded, so their place in the mix is set here.
COUNT_TICK_DB = -2.0
FALSE_START_DB = -7.5
# The staged show's buzzer: a held square is far denser than a horn or a bar at the same peak.
SHOW_WRONG_DB = -6.0


def level(stereo_data, db):
    """A normalized recording set down by <db> decibels."""
    return stereo_data * (10.0 ** (db / 20.0))


def count_tick():
    """The count's tick: the reference build's compProgress (a 1000 Hz sine for 50 ms), with a small
    wooden knock under its front so it reads as a clock's tick, not the pointer's."""
    track = s.Track(0.14)
    track.add(s.note("sine", 1000.0, 0.09, env=(0.001, 0.03, 0.0, 0.05), gain=0.5), 0.0)
    track.add(s.note("sine", 2000.0, 0.05, env=(0.0005, 0.015, 0.0, 0.03), gain=0.1), 0.0)
    n = s.seconds(0.03)
    knock = s.lowpass(s.osc("noise", 13.0, n), 3000.0) * s.adsr(n, 0.0005, 0.01, 0.0, 0.015) * 0.25
    track.add(knock, 0.0)
    return track


def false_start():
    """An early press: a dull double buzz, low and short - the game's 'not yet', not a wrong answer."""
    track = s.Track(0.3)
    for index in range(2):
        track.add(s.note("square", 150.0, 0.09, env=(0.003, 0.03, 0.6, 0.03), cutoff=1100.0, gain=0.35), index * 0.12)
    return track


def latch(gain=0.3):
    """The latch: a bright tick of filtered noise and a short metal ping over it."""
    n = s.seconds(0.06)
    tick = s.highpass(s.osc("noise", 11.0, n), 2800.0) * s.adsr(n, 0.0005, 0.008, 0.0, 0.006)
    ping = s.note("sine", 2350.0, 0.06, env=(0.0005, 0.03, 0.0, 0.02), gain=0.25)
    return (tick * 0.6 + ping) * gain


def door_open():
    """The latch gives with a wooden knock, then the leaves swing: a low, breathy sweep of air and wood."""
    track = s.Track(0.95)
    track.add(latch(0.12), 0.0, pan=0.1)
    track.add(hit(220.0, 95.0, 0.16, noise=0.35, gain=0.9), 0.004)
    n = s.seconds(0.65)
    t = np.arange(n) / s.SR
    air = s.lowpass(s.highpass(s.osc("noise", 5.0, n), 180.0), 900.0)
    swell = np.sin(np.pi * np.clip(t / 0.65, 0.0, 1.0)) ** 1.5
    track.add(air * swell * 0.3, 0.08, pan=-0.15)
    # The wood's own groan under the air: a low tone rising a little as the leaves gather speed.
    groan = np.sin(2.0 * np.pi * np.cumsum(95.0 + 35.0 * t / 0.65) / s.SR) * swell * 0.2
    track.add(s.lowpass(groan, 600.0), 0.08)
    return track


def bell(freq, dur, gain=0.4):
    """A soft struck bar: the fundamental with its octave and a faint twelfth, dying away as a bar does."""
    n = s.seconds(dur)
    t = np.arange(n) / s.SR
    body = (np.sin(2.0 * np.pi * freq * t)
            + 0.3 * np.sin(2.0 * np.pi * 2.0 * freq * t) * np.exp(-t * 3.0)
            + 0.08 * np.sin(2.0 * np.pi * 3.0 * freq * t) * np.exp(-t * 6.0))
    shape = np.minimum(1.0, t / 0.003) * np.exp(-t * 6.5) * np.clip((dur - t) / 0.03, 0.0, 1.0)
    return s.lowpass(body * shape, 4000.0) * gain


def door_shut():
    """The leaves land: a heavy low thump with the latch catching a breath after it."""
    track = s.Track(0.9)
    track.add(hit(150.0, 46.0, 0.55, noise=0.45, gain=1.0), 0.0)
    track.add(s.note("sine", s.hz("A1"), 0.4, env=(0.004, 0.15, 0.3, 0.2), gain=0.35), 0.0)
    track.add(latch(0.12), 0.035, pan=0.12)
    return track


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    out = os.path.abspath(args[0] if args else "Assets/Gamesim/Resources/Audio/Cues")
    for name, data in cues():
        print(name, "->", s.write_wav(os.path.join(out, name + ".wav"), data), round(len(data) / s.SR, 2), "s")
