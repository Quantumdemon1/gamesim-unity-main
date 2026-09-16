# Playtest protocol — U08 section E

Section E is the only part of the acceptance matrix that cannot be automated. That does not make it
vague. These five criteria are judgements, but they are judgements about specific things, and a
session run to this protocol produces evidence a later session can be compared against.

Everything else in `ACCEPTANCE_MATRIX.md` passes. This is what remains.

## The camera default is NOT 24 — the shipped scene opens at 48

This section previously said the rig "starts at 24 and allows 10 to 34". That is the C# field
default, and `EpisodeHouse.unity` overrides both:

| | Source default | Serialized in the shipped scene |
|---|---|---|
| `distance` | 24 | **48** |
| `maximumDistance` | 34 | **56** |

`Camera_ReportsStartupFramingOverTime` measures the running game: the camera sits at 48 units from
the pivot at frame 0 and is still at 48 at frame 480, with the rig's own field reading 48. It does
not converge on 24 — nothing in an untouched episode ever puts it there.

Three things follow, and they matter before anyone runs a session:

- **The camera decision was taken against a number the build does not use.** "Leave it at 24" was
  answered about a default that is actually 48. The decision needs retaking against 48, or the scene
  needs changing to match it. Either is fine; shipping a documented 24 that renders as 48 is not.
- **The existing legibility measurement was taken at 48**, which is why it recorded houseguests
  "42–54 metres from camera" — a figure a 24-unit rig cannot produce. The 1.4%–2.2% of frame height
  is real, but it is the 48 number, so a cast that reads small at the default reads roughly twice
  that size at the documented one.
- **`Accessibility_ComparesCameraFramings` renders 24, 17 and 10 — and none of them is the shipped
  default.** Add 48 before using those frames to choose a framing.

**Run the session against whatever the scene actually ships**, and write the measured distance into
the notes. Do not hand-edit the rig to 24 to match the old text.

Watch for it during E2: if participants stall because they cannot tell who is who, note whether they
discover the zoom on their own. At 48 that is a much bigger bet than the decision assumed.

## Before the session

- Use one pinned build and record its path and build report. Do not patch between participants.
- Decide the cast first: the committed scene ships the authored prefabs. If you are evaluating the
  UMA cast instead, run `Gamesim > UMA > Use UMA bodies in the episode` **before** building, and say
  so in the notes — E5 answers differently for each.
- Start each participant from a fresh save. `Gamesim > Port > Start Isolated Preview` keeps the normal
  slot untouched.
- Have a timer and somewhere to write. Write during the session, not after.

## E1 — Pacing

**Threshold: 30–45 minutes to finish one episode, without skipping.**

Start the clock when they take control and stop it at the eviction recap. Record the raw number even
when it falls outside the band — a 22-minute run and a 70-minute run fail the same criterion and mean
completely different things.

Also note where time actually went. The automated season commits 56 decisions in 58 seconds, so all
of the real duration is reading, deliberating and moving. If the number is wrong, the fix follows from
which of those three dominated.

## E2 — First-time comprehension

**Threshold: at least three participants who have never seen the build reach the eviction unaided.**

"Unaided" means no hints, no answering questions about controls or what to do next. Sit where you can
see the screen and stay quiet.

Record each place they stall, what they tried, and what unstuck them. A participant who reaches the
eviction after being stuck twice still passes E2, and those two stalls are the most valuable thing the
session produces.

## E3 — A decision feels consequential

**Threshold: each participant can name a promise or betrayal that changed a later outcome, and say
why.**

Ask afterwards, open-ended: *what happened in there?* Do not prompt with names or events. The
criterion is met when they volunteer a causal link — someone did something, so something else
followed. "I think the game decided" fails it even if the simulation genuinely did model the link,
because the model was not legible.

Compare their account against the committed events in the notebook. Where they diverge is a
presentation problem, not a simulation one.

## E4 — The loss is intentional

**Threshold: a losing run reads as an ending, not as a failure state.**

Needs a participant who is actually evicted; do not stage it. Ask whether it felt like the end of
their story or like the game stopping. Note whether they wanted to keep watching the house.

## E5 — Visual bar

**Threshold: house and cast read as one production, not placeholder plus asset pack.**

An art review, not a playtest, and better done with fresh eyes on a still frame than in motion.
Capture at 1280x720, 1600x900 and 2560x1440; `Accessibility_CapturesTheHudOverTheSetForReview` writes
these automatically during a headless run.

Two known issues to judge rather than rediscover:

- **The cast reads small at the default camera, and it is measured, not felt.** Houseguests project to
  1.4%–2.2% of frame height from 42–54 metres. That is a camera-framing decision, not an art one, and
  no character change reaches it. Judge the framing; do not re-litigate the bodies.
- **Bloom is absent from the automated captures.** They come from `camera.Render()` into a
  RenderTexture, which skips the URP post-processing pass. Judging the HUD against the lit set needs a
  windowed run, which is the open half of D6b.

## Recording the result

Write the outcome into `ACCEPTANCE_MATRIX.md` section E with the build path, the date, the number of
participants, and the raw numbers — including the failures. The V6 record keeps its failed runs on
purpose and that practice is worth continuing: a criterion recorded as "passed" with no number behind
it is indistinguishable from one nobody checked.
