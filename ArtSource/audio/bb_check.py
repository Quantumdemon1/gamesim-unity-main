"""Sanity checks on a rendered WAV: peak, RMS, DC offset, clipping, silence, and the loop seam.

    blender --background --python ArtSource/audio/bb_check.py -- <wav> [<wav> ...]
"""
import os
import sys
import wave

import numpy as np


def check(path):
    with wave.open(path, "rb") as f:
        channels, width, rate, frames = f.getnchannels(), f.getsampwidth(), f.getframerate(), f.getnframes()
        raw = f.readframes(frames)
    data = np.frombuffer(raw, dtype="<i2").astype(np.float64) / 32768.0
    data = data.reshape(-1, channels)
    peak = float(np.max(np.abs(data)))
    rms = float(np.sqrt(np.mean(data ** 2)))
    dc = float(np.mean(data))
    clipped = int(np.sum(np.abs(data) >= 0.999))
    seam = float(np.max(np.abs(data[0] - data[-1])))
    # Silence: any 0.5 s window whose RMS is below -60 dB, excluding the ends of a one-shot.
    window = rate // 2
    quiet = 0
    for start in range(0, len(data) - window, window):
        if np.sqrt(np.mean(data[start:start + window] ** 2)) < 0.001:
            quiet += 1
    print("%-22s %d ch %d Hz %6.2f s peak %5.2f dBFS rms %6.2f dBFS dc %+.4f clipped %d seam %.3f quiet-halves %d"
          % (os.path.basename(path), channels, rate, frames / rate, 20 * np.log10(max(peak, 1e-9)),
             20 * np.log10(max(rms, 1e-9)), dc, clipped, seam, quiet))


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    for path in args:
        check(path)
