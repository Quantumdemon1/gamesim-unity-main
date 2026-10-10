# Playtest protocol — U08 section E

Section E is the only part of the acceptance matrix that cannot be automated. That does not make it
vague. These five criteria are judgements, but they are judgements about specific things, and a
session run to this protocol produces evidence a later session can be compared against.

The schema-13 review candidate is undergoing fresh automated, rendered and performance
verification. Historical passes in `ACCEPTANCE_MATRIX.md` do not certify it. Run this protocol only
against the new candidate identified by its executable hash, build report and source manifest.

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

- **The cast measurement was noisy, and the cause is now fixed.** Two houseguests once reported
  1.3% and 0.2% while the rest roughly doubled. The cause was the U02 capsule placeholders: they
  shipped enabled in the saved scene and were switched off during the first frames of play, so the
  measurement sometimes encapsulated a body mid-swap. With the placeholders disabled in the scene
  itself the range is 1.5%–4.7% and the outliers are gone. Casey is the smallest at 1.5% because
  that character is authored shortest, not because anything is wrong.
- **Vertical surfaces stay foreshortened.** The rig pitches 55 degrees down (clamped 45–70), so
  wall-mounted fixtures — the memory wall, and anything like it — read at roughly a third of what
  a floor-standing object of the same size does. That is a pitch question, still open, and no
  amount of work on a fixture reaches it.

Run the session against whatever the scene actually ships, and write the measured distance into
the notes.

## Before the session

- Use one pinned build and record its path and build report. Do not patch between participants.
- Build on a machine with UMA imported. The committed scene carries `GamesimUmaCast`, and since
  2026-09-27 UMA is the only cast. A build made without UMA shows the primitive rig instead; E5
  answers differently for that, so say so in the notes.
- Start each participant from a fresh save. `Gamesim > Port > Start Isolated Preview` keeps the normal
  slot untouched.
- Have a timer and somewhere to write. Write during the session, not after.

## Session length (proposed by BALANCE plan B7 — awaiting the owner's approval)

How many episode-weeks a session must cover for E3 and E4 to be likely to be observable at all, projected from
the balance lab's first-timer model (the novice: plays about half its seats on plain talk, takes every offer,
pulls no lever, performs at .35) over 400 seasons a house, NPC budget 0, and 200 at the 300 ticks a week that stand
for a human's free roam. Re-projected on 2026-10-09 with every shipped rule on - D2's all-week NPC strategy, the war
rooms, the leaks - and the NPC world's arc fault fixed (B6c); the earlier projection (B6b) predated D2 and the fix.
Full tables: `BALANCE_BASELINE.md`, *The projections*. Testers are taken as independent; a human tester is not the
novice, so treat these as the order of magnitude.

**The week, not the minute, is the unit.** The lab's mechanical minutes a week in the house - ceremonies at the
suspenseful pace, the competitions played, the NPC free roam, and 30 seconds a decision - are about 8.5 at eight
(13.6 with five minutes of free roam), far below E1's own 30–45 minutes an episode. What a person spends over a
decision is the unknown (B8 measures it), so plan a session as a number of episode-weeks, at E1's 30–45 minutes
each until B8 says otherwise.

**E4 (a loss that reads as an ending) needs six weeks with three testers, four with four, three with five.** The
chance at least one tester is out by week k, in the shipped eight-house:

| testers | week 1 | week 2 | week 3 | week 4 | week 5 | week 6 |
|---|---|---|---|---|---|---|
| 3 | 29.5% | 63% | 79% | 84% | 87.7% (300 ticks: 88.9%) | 95.5% |
| 4 | 37% | 74% | 87.5% | 91.4% | 94% | 98% |
| 5 | 44% | 81% | 92.5% | 95% | 97% | 99% |

Under D2 fewer first-timers go out in week one (11% against B6b's 24%: the house comes to the newcomer before the
first nominations), so the first week rarely ends anyone's game; by week three the rates are close to B6b's.

Proposed, following the lead's decision for E4: a multi-session protocol on each tester's isolated save
(`Gamesim > Port > Start Isolated Preview`, resumed, never restarted), two episode-weeks a session (60–90
minutes at E1's pace), until one tester is out - with three testers expect it by the end of the third session
(95.5% by week six; 84% after the second). With four testers two sessions (four weeks, 120–180 minutes) reach 91%;
with five, a single session of three weeks (90–135 minutes) reaches 92.5%. A twelve-house is slower: 90% with three
testers at week 7, with four at week 6, with five at week 5.

**E3 as worded - each tester names a promise or betrayal that changed a later outcome - is out of reach by
chance for a first-timer.** The chance a novice saw a commitment of theirs settle while still in the house
(a deal or promise kept or broken whose ending they know, or an alliance plan they answered) rises from 22.5% in
week one to 50% by week two and 63.5% by week three, and stops near 69% at eight (85% at twelve): the rest leave,
or never make one that settles. So every one of three testers seeing one never passes 33% at eight (61% at
twelve), whatever the session's length. Proposed, for the owner to choose:

- **Judge E3 over the testers who saw one**, record the week each first did (or none), and run at least three
  weeks; with five testers at least three of them have seen one by week three about 74% of the time, by week
  four 81% (twelve-house: 89% and 95%).
- **Or keep "each tester" and accept that E3 will usually be unobserved** for some of them, recorded as such,
  not as a failure.

Either way a tester evicted before any commitment of theirs settled is "not observed" for E3, never "failed".

## E1 — Pacing

**Threshold: 30–45 minutes to finish one episode, without skipping.**

Start the clock when they take control and stop it at the eviction recap. Record the raw number even
when it falls outside the band — a 22-minute run and a 70-minute run fail the same criterion and mean
completely different things.

Also note where time actually went. The automated season commits 56 decisions in 58 seconds, so all
of the real duration is reading, deliberating and moving. If the number is wrong, the fix follows from
which of those three dominated.

The ceremony reveals are part of that time. The key ceremony and the vote reveal default to a
suspenseful pace. The two take 30 to 50 seconds in an early week, by house size, which is 20 to 30
seconds more than the quick pace Settings offers. A click, Enter or Esc jumps a reveal to its result, and a second press
closes it. Record the pace setting, and note whether the participant skipped reveals. A run where they
did is not "without skipping".

## E2 — First-time comprehension

**Threshold: at least three participants who have never seen the build reach the eviction unaided.**

"Unaided" means no hints, no answering questions about controls or what to do next. Sit where you can
see the screen and stay quiet.

**Decide the tutorial before the first participant, and record it.** The build carries the
reference build's seven-step tour - the welcome and controls, the top bar, the side rail, the episode
screen, the cast strip, the phase panels, and a closing card - with a dimmed spotlight on each. Like
the reference, it plays at every new season's opening, seen before or not. It is exactly the
intervention this criterion exists to detect the absence of, so it changes what E2 measures rather
than how well the build does on it:

- **Tour off** — the honest reading of "unaided", and the number comparable to the V6 record. Press
  "Skip Tutorial" (or Esc) the moment it appears, before the participant reads it. The
  `Gamesim.TutorialSeen` preference no longer holds it back at the opening; it only records a finished
  tour for the one offered to a season imported mid-way.
- **Tour on** — a fair question about the shipping product, but a different one. If you run it this
  way, write "tour on" beside the number.

Do not run some participants each way and report whichever set looks better.

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
