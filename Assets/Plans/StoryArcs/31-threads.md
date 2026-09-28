# 31 · Threads: the season's stories (plan 30 P3)

Status: **built** (P3a and P3b, 2026-09-28; IMPLEMENTATION-LOG "Threads: P3"). This fills in plan 30
§3 (decision D5: seeded threads) for the milestone the owner chose after P4. Plan 30 still governs.

## 0. TL;DR

A **thread** is a season-long story the player is inside, such as a bond, a rivalry or a power line.
Each season seeds two or three from its cast at the first eviction night.

- **Chapters are plays and arcs aimed at the thread's person.** How one ends picks the next: a friend
  you got to know is asked for their word; a friend who gave it is asked for the end.
- **Each thread ends at a fixed point, and that point reads the season.** The power line ends at the
  final four, the bond and the rivalry at the jury: who is still with you, who voted for you.
- **The notebook and the recap tell it as a story:** "Your bond with Alex, chapter 3."

Threads add no mechanics of their own: the bonds, grudges, promises and alliances their chapters make
are what the votes and the jury already read. They add direction, sequence, and an ending worth
remembering.

## 1. The model

- **A thread is a story cycle in its own lane.** It uses the `StorylineState` saves already carry, in a
  new lane `StoryLanes.Thread` (three at most), so it adds no save field and needs no schema.
  - its cast is who the thread is about;
  - its path records each chapter, started and ended, with the chapter cycle's id;
  - its ending is how the season came out for it.
- **Rules version:** `StoryRules.Threads` = 9 = Current. Seasons stamped earlier never seed a thread.
- **Seeding** happens at the first eviction night, from the cast as it stands then:

  | Thread | Who | When there is nobody |
  |---|---|---|
  | The bond | Your warmest houseguest both ways | Not seeded |
  | The rivalry | Whoever holds the most against you, or else the coldest both ways, if cold at all | Not seeded |
  | The numbers | Your alliance, or else the two warmest who could be allies | Not seeded |

  The bond and the rivalry are never the same person. A season gets two or three threads. Production
  is not seeded: its ladder already is its own thread, and only conduct earns it.
- **Chapters.** At every anchor, a thread with no chapter running picks its next one from its table
  and tries to start it, aimed at its own people ("focus"):
  - the chapter's own cast must accept them, so every play and arc keeps its conditions;
  - the airtime, lanes and cooldowns apply as for any story.

  If the chapter cannot start yet, the thread waits and tries again at the next anchor. A chapter the
  pool casts for the same person counts too: a thread owns its people's stories.
- **Climaxes** come at fixed points, and read what happened:

  | Thread | Climax | Endings |
  |---|---|---|
  | The numbers | The final four, or the alliance's end | held (you and an ally in the four), broken (it came apart), betrayed (you cut them) |
  | The bond | The jury's vote, or the finale | to the end (both in the final), for you (their jury vote), against you, faded |
  | The rivalry | The jury's vote, or the rival's exit | made peace (settled before the end), won (they left first), lost (you left first), against you (their vote) |

- **Stated ends before the climax:**
  - you leave the house: the thread ends as you left it;
  - the chapters run dry: nothing castable for four weeks, and it fades;
  - the subject is removed by production.

## 2. The chapter tables

| Thread | Chapters, in order; how each ends picks the next |
|---|---|
| The bond | Know Them → Their Word → Ride or Die (or Late Nights, where both are open to a romance). A bond that turns sour gets Settle It. |
| The rivalry | Settle It. Won: made peace, and the thread waits for its climax. Lost: Stir the Pot (turn the house on them), then the rival's time on the block. |
| The numbers | Build the Numbers (if they are not yet one alliance) → The Secret Alliance (the bloc against you) → Stay Off the Block or The Favour, week to week |

## 3. The player sees it

- **The notebook's Story section:** the plays, then THREADS. Each thread shows its title, its person,
  the chapter it is on and how the earlier ones went.
- **The recap's "Previously on" and "Next time on"** already read story cycles; a thread's chapter
  lines are in them.
- **The receipts:** a climax writes its line and receipt like a play.
- **The Game Sense verdict** (other session) can read a thread's ending from its cycle, as it reads a play's.

## 4. Milestones

| | Done when |
|---|---|
| P3a | The model, seeding, chapters and climaxes, with tests. Every season in the sweep seeds 2-3 threads, and each reaches its climax or a stated end. |
| P3b | THREADS in the notebook, and the recap leads with the threads. |

## 5. Decisions taken (within D5)

- Threads orchestrate existing plays and arcs. They add no new vote or jury terms: the bonds, grudges
  and promises they produce are already read.
- Seeded at the first eviction night, as plan 30 §3 says, from the relationships as they stand then.
- A thread owns its people: the play pool does not cast a chapter play for a thread's person outside
  the thread.
