# Playtest protocol — U08 section E

Section E is the only part of the acceptance matrix that cannot be automated. That does not make it
vague. These five criteria are judgements, but they are judgements about specific things, and a
session run to this protocol produces evidence a later session can be compared against.

Everything else in `ACCEPTANCE_MATRIX.md` passes. This is what remains.

## The camera default is 24, and now actually is

This section previously recorded a discrepancy: `HouseCameraRig` declared `distance = 24` with a
34 maximum, while `EpisodeHouse.unity` serialised **48 and 56**, and the running game measured 48
at frame 0 and still 48 at frame 480. It never converged. Every framing judgement made about this
project — the "leave it at 24" decision, the cast-reads-small finding, the three comparison
renders — was made against a number the build did not use.

**Fixed on 2026-09-16.** The scene now serialises 24 / 34, matching the source defaults and the
decision already taken. `Camera_ReportsStartupFramingOverTime` measures 24.0 at frame 0 and holds.

What that changed, measured the same way both times:

| | at 48 | at 24 |
|---|---|---|
| Distance to cast | 42–54 m | 20–30 m |
| Cast height, typical | 1.8–2.2% of frame | 2.7–4.6% |
| Memory wall | 1.0% | 1.4% |

Two caveats worth carrying into a session rather than rediscovering:

- **The cast measurement is noisy between runs.** In the 24-unit run two houseguests reported
  1.3% and 0.2% while the rest roughly doubled. Apparent size is camera-independent arithmetic on
  world bounds, so a figure that moves when only the camera moved means the measurement caught a
  body mid-assembly, not that the character shrank. Treat single outliers as measurement noise and
  the typical range as the signal.
- **Vertical surfaces stay foreshortened.** The rig pitches 55 degrees down (clamped 45–70), so
  wall-mounted fixtures — the memory wall, and anything like it — read at roughly a third of what
  a floor-standing object of the same size does. That is a pitch question, still open, and no
  amount of work on a fixture reaches it.

Run the session against whatever the scene actually ships, and write the measured distance into
the notes.

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

- **The cast still reads small, and it is measured, not felt.** At the corrected 24-unit default
  houseguests project to roughly 2.7%–4.6% of frame height from 20–30 metres — about double the
  42–54 metre figure this section used to quote, which was taken at the 48 the scene wrongly
  shipped. Whether that is now enough is the judgement E5 is for. It remains a camera decision
  rather than an art one, so judge the framing and do not re-litigate the bodies.
- **Bloom is absent from the automated captures.** They come from `camera.Render()` into a
  RenderTexture, which skips the URP post-processing pass. Judging the HUD against the lit set needs a
  windowed run, which is the open half of D6b.

## Recording the result

Write the outcome into `ACCEPTANCE_MATRIX.md` section E with the build path, the date, the number of
participants, and the raw numbers — including the failures. The V6 record keeps its failed runs on
purpose and that practice is worth continuing: a criterion recorded as "passed" with no number behind
it is indistinguishable from one nobody checked.
