# Live saves: the house's quiet seconds, saved in batches

Written 2026-11-01 for the owner's decision on the once-a-second freeze (investigation wf_956f4ab5-76c,
`livehitch2/hitch-report.md`). Option 1 of that report, with option 4(a) beside it. Option 4(b), skipping the
frozen schema-29 check on read-back, is not part of this plan: it belongs to the All-Star session and the owner.

## For the owner, in short

While the houseguests mingle on their own, the game saves the whole season once a second, on the thread that draws
the picture, so the screen freezes for about a quarter of a second every second. From this change on, the
houseguests' own activity - their clock and their conversations with each other - is kept in memory and saved:

- every 30 seconds of free time;
- the moment you make any choice (every choice is saved before its result is shown, as now);
- the moment any menu, panel, conversation, diary visit, notebook page or recap opens;
- when you switch away from the game, minimise it, start or import a season in another slot, or quit.

If the game crashes, is killed or the PC loses power, up to the last 30 seconds of the houseguests' own background
activity can be lost, and they then live those seconds again a little differently. Nothing you chose and nothing
you read in a menu is lost. The save file, its format, its backup and its crash safety do not change. A save that
fails still stops the house and tells you, as it does now.

Two rarer cases end the same way, and the question you answered did not name them: a save that another program (a
backup, sync or virus scanner) keeps hold of for longer than the game waits, which shows the recovery screen and
asks you to reload, and a save the game refuses because of a bug in what it holds, which also asks you to reload.
Either costs at most those same last 30 seconds of the houseguests' own activity, never a choice. And while saving
is failing, a menu you open can show houseguest activity that is not on disk yet; the notice on screen says the save
failed, and every menu you open tries the save again.

You can expect: no freeze in 29 of every 30 seconds of free time; one save's freeze every 30 seconds (shorter once
option 4(a) lands); a save as you open a menu in free time, which is where you pause anyway. Two things this does
not remove: the redraw after each houseguest second, if the timing log shows it is a large part of the freeze
(option 5, measured first), and the profile run's own small hitch every 10 seconds.

**Decision record.** Owner's call, 2026-11-01: "Yes, accept the 30 s loss, start the save-check fix", in answer to:
"If the game crashes, is killed, or the PC loses power, may it lose up to the last 30 seconds of the houseguests'
own background activity (their conversations with each other and the clock), which would then replay a little
differently? Nothing you chose and nothing you read in a menu would be lost." The 30 s constant carries this
attribution in code (`NpcSaveCadence.Seconds`). Not covered by that answer, and to be put to the owner with this plan
before commit 2 lands (section 12): the held-save and refused-save paths of section 4, and what a menu shows while
saving fails.

---

## 0. Base and line numbers

Branch `claude/live-save-batching` in `D:/GamesimLiveSave`, cut from origin/main 37416049. Every file:line below is
at 37416049. The investigation quoted 6dcd07ae (room scope), whose `EpisodeDirector.cs` has two more lines from line
378 on (`TickRoomScope`), so its `:457` and `:844` are this plan's `:455` and `:842`. `EpisodeSaveStore.cs` is read
from origin/main only; another agent is changing it for 4(a) and this plan does not touch it.

## 1. Everything that assumes "each whole NPC second is on disk before it is shown"

### 1.1 Code paths

| Where | What it does today | What changes |
|---|---|---|
| `EpisodeDirector.NpcSocial.cs:363-390` `CommitNpcOperation` | Prepares the operation, then `TryPersistCandidate` (:378) writes it, and only then installs (:381-383) and plans routes (:384-388). Ticks (:343-353), starts (:304-315) and cancels (:262-269) all come here. | Installs in memory; no write. See 2.1. |
| `EpisodeDirector.NpcSocial.cs:378-379` | A failed write shows the notice and stops the house. | Moves to the flush (2.3). |
| `EpisodeDirector.NpcSocial.cs:392-396` `PauseNpcSocialForPanel` | Commits pending whole ticks (each one a write). | Commits them in memory, then one write of everything unsaved (2.4). |
| `EpisodeDirector.Season.cs:148-158` `SaveNow` | `FlushNpcWholeTicks` writes each tick, then `TryAutosave` writes again; when a pending pair is not ready, `FlushNpcWholeTicks` returns false and **no save happens** (harmless only because every second was already on disk). | One write while the session is healthy, pair walking or not; a refusal inside its own flush still ends the call (2.4). |
| `EpisodeDirector.Season.cs:164` (`TryAutosave`'s failure) and `EpisodeDirector.cs:848` (`Submit`'s) | Call `PauseNpcSocialForPanel` after a failed write; its flush cannot run, because every failing return of `TryPersistCandidate` leaves `NpcCanAdvance` false. | Call `SuspendNpcWorldWithoutSaving` instead, which is what they do today, so the panel path's retry (2.3) never makes a second write in the action that just failed. |
| `EpisodeDirector.Season.cs:305-357` `StartSeason`, `:389-408` `ImportFile` | Leave the old slot without writing it (it was current to the second). | Write the outgoing slot's unsaved seconds first, retrying a suspended session; refuse at most once per failure (2.4). |
| `EpisodeDirector.Season.cs:167-183` `LoadNow`, `RecoverBackup` | Never write; the disk was current. | Still never write (2.4 explains why); reachable only from Settings, which saves on opening. |
| `EpisodeDirector.Season.cs:410-432` `Install` | Replaces the engine after `ResetNpcSocialForLoad` (:417-418). | Resets the cadence right after `engine = new EpisodeEngine(state)` (2.3). `ResetNpcSocialForLoad` itself is unchanged: the opening stage calls it without a load (`OpeningStage.cs:236` TryPlace, `:252` RestoreHome). |
| `EpisodeDirector.Durable.cs:82-97` reconciliation | Judges the disk against `before`, the in-memory session. | Judges it against the last durable state (3.1); words a refusal of the house's own state as "reload" (3.1). |
| `EpisodeDirector.Durable.cs:28-33`, `NpcSocial.cs:103-107` diagnostics | Suspend/freeze stop the clock; nothing was ever owed. | Suspend writes what is owed first; freeze sets its flag before suspending (2.4). |
| `EpisodeDirector.cs:1700-1714` `OnDisable`, `:1734-1756` `OnDestroy` | Dispose the world; nothing owed. | Unchanged; the quit hook writes first (2.4). |
| Runtime/Episode has no `OnApplicationFocus`, `OnApplicationPause`, `OnApplicationQuit` or `Application.wantsToQuit` (the only focus hook is `CompetitionGameScreen.cs:383`). | - | Three new hooks in `EpisodeDirector.Durable.cs` (2.4); focus and pause do nothing in the Editor under a save-root override, which is every test run. |
| `PortVerification.Autonomy.cs:81-100` | Opens Settings, saves, reloads and requires the reload to equal memory. | Still holds: Settings now writes on opening. Wording only at :37 ("durably start"). |
| `PortVerification.Season.cs:132-149, :237, :612-623` | Saves through Settings, then compares the old slot and the reload. | Still holds: the clock is held under Settings, so the outgoing-slot write in `StartSeason` finds nothing owed. |

### 1.2 Comments and strings that state the old rule

| Where | Today | Becomes |
|---|---|---|
| `EpisodeDirector.NpcSocial.cs:216-217` | "A suspension/debugger/long blocked frame is not elapsed social play. Whole committed seconds persist; the remaining fraction is transient." | Whole seconds are installed as they come and saved with the house's next write, at most `NpcSaveCadence.Seconds` of free time later and before any decision, panel, pause, slot change or quit; the remaining fraction is transient. |
| `EpisodeDirector.NpcSocial.cs:336-337` | "...fresh proof for every durable pending pair." | "...every saved or installed pending pair." |
| `EpisodeDirector.NpcSocial.cs:383` | "Reconciles removed pending leases only after successful persistence." | "...only after installation." |
| `EpisodeDirector.NpcSocial.cs:384-385` | "Only a durably committed scan deadline can schedule NEW routes." | "Only an installed scan deadline can schedule NEW routes." (3.2) |
| `Simulation/EpisodeNpcSocial.cs:41-45` | "This NEVER installs current. The root coordinator must save successfully, then construct/install a new engine; a failed save or abandoned candidate consumes no authority or random draws." | "This NEVER installs current. The root coordinator installs a new engine from the candidate and saves the house's installed operations together (EpisodeDirector.Durable.cs); an abandoned candidate consumes no authority or random draws." |
| `Simulation/EpisodeNpcSocial.cs:97` | `result.reason = "Prepared; save before install."` | `"Prepared; install, then save with the house's next write."` No test or digest reads it (`git grep` finds only the source). |
| `EpisodeDirector.Durable.cs:38-42` | "Persist before publishing. All callers are synchronous..." | "Persist a decision before publishing it; the house's own operations are published in memory and persisted together by FlushUnsavedNpcTime. All callers are synchronous..." |
| `EpisodeDirector.Season.cs:152-153` | "An explicit retry can save an unchanged session after an I/O failure. A NEW failed tick must never fall through to a second, older write." | Rewritten with `SaveNow` (2.4). |
| `EpisodeDirector.cs:835-836` | "Player and NPC candidates share durable publication ordering..." | "A decision's candidate carries every operation the house has installed: its write is also the house's..." |
| `PortVerification.Autonomy.cs:37` | "...durably start a conversation within 65 seconds." | "...start a conversation within 65 seconds." (A RequireSeason message; RS0's readers do not match it.) |
| Test comments: `EpisodeNpcScanCadenceTests.cs:23` (name "...ThirdDurableSecond"), `EpisodeNpcCompletionTransactionTests.cs:18`, `UnifiedSafetyDurability.cs:297`, `DialogueIncomingOffer.cs:64-70, :247-249` | State the per-second write. | Rewritten with their tests (7.1). |

Checked and still true, so left alone: `NpcSocialState.cs:8` ("Durable native clock...": the saved state), `EpisodeDirector.cs:892` (the finale's career record), `Journal.cs:855-858` (the season report reads the committed season: the finale is never free time, so nothing is owed there), `NotebookVotes.cs:29-31` and `Chrome.cs:308-310` (they speak of revisions, which do not change).

## 2. The new rule

### 2.1 House operations install in memory

`CommitNpcOperation` keeps every guard it has (:365-375) and its order. After `PrepareNpcOperation` accepts:

1. `EpisodeSaveValidation.Validate(result.candidate)`: the store's own bounds (`EpisodeSaveValidation.cs:10-34`), run
   today at `Durable.cs:61` before every tick's write. Kept here so that nothing these bounds refuse is ever
   installed. **It is not the store's whole check.** The store's read-back of the file it writes (`Install`,
   `EpisodeSaveStore.cs:125` on origin/main, through `TryRead` :145-191) adds the 8 MiB limit (:153), the frozen
   schema-29 reader (`PrepareCurrentPayload`, `EpisodeSaveMigrations.cs:26-30`), `CheckDtoShape` and hydration
   (:166-169), and those run only at the write. So an installed operation can still make the house's later write
   fail on what memory holds; section 4 says what happens then (reload). Running the whole read-back here is the
   cost this change removes, so it is not done. On refusal, `RefuseUnsaveableNpcOperation(error)`: the operation is
   not installed; the operations already installed, which passed this same check, are written first (one
   `TryPersistCandidate(engine.Snapshot)`, skipped when nothing is owed, under recovery or when frozen); then
   `npcSaveSuspended = true` and the house stops with today's text ("SAVE NEEDS ATTENTION: ... The decision was not
   committed. Your current session is unchanged; retry Save or reload."), what today's catch at `Durable.cs:90-97`
   produces for the same candidate. If that first write failed, its own text stands instead. So a refusal never
   leaves the house standing still on seconds only memory holds. Cost: one more whole validation, about 2 ms a
   second.

   An Editor-only seam, `internal Action<EpisodeState> npcOperationCheckForDiagnostics` beside
   `saveCandidateForDiagnostics` (`Durable.cs:18`, same idiom: called only when `Application.isEditor` and set),
   runs after the bounds so that test 16 can reach this refusal. No fixture can reach it any other way, because the
   bounds it guards need a save over the store's 8 MiB limit.
2. If the session is clean, the cadence takes `engine.Snapshot` as the state on disk (2.2), then counts the
   operation.
3. `npcPendingWorld[sequence] = startLease` for a start, `engine = new EpisodeEngine(result.candidate)`, `Project()`,
   and the scan's `PlanNpcApproaches` (:386-388) exactly as today.

Nothing about which operation runs, when, in which order or with which draws changes: ticks still come from
`FlushNpcWholeTicks` (:343-353) once a whole second has built up, starts from `StartArrivedNpcApproaches` (:304-315)
after them, cancels from `PendingNpcWorldReady` (:262-269), the scan every third tick. `TryPersistCandidate` and
`EpisodeSaveValidation` draw nothing. Within a session the sequence of engine states is the one today's code
produces for the same world timing (memory removing-a-command-re-rolls-the-season: nothing is trimmed or reordered).

Sketch (the builder writes the real code in the file's idiom):

```csharp
var result = engine.PrepareNpcOperation(request);
if (!result.accepted) return false;
try
{
    EpisodeSaveValidation.Validate(result.candidate);
    if (Application.isEditor && npcOperationCheckForDiagnostics != null) npcOperationCheckForDiagnostics(result.candidate.Clone());
}
catch (Exception error) when (SaveJson.IsExpected(error))
{ RefuseUnsaveableNpcOperation(error); return false; }   // writes what is installed, then today's notice; house stopped
if (queued.Generation != loadGeneration) { StopNpcWorld("..."); return false; }   // kept
if (startLease != null) npcPendingWorld[request.sequence] = startLease;
// Installed now; saved with the house's next write (FlushUnsavedNpcTime).
if (npcSaveCadence.Clean) npcSaveCadence.Begin(engine.Snapshot);
npcSaveCadence.Installed();
engine = new EpisodeEngine(result.candidate);
Project();
// Only an installed scan deadline can schedule NEW routes. ...
if (request.kind == NpcOperationKind.Tick && result.scanDue && ...) PlanNpcApproaches();
return true;
```

```csharp
private void RefuseUnsaveableNpcOperation(Exception error)
{
    // What is installed passed this same check: it reaches disk before the house stops on the one refused.
    string failure = "SAVE NEEDS ATTENTION: " + SaveJson.Explain(error) + " The decision was not committed."
        + " Your current session is unchanged; retry Save or reload.";
    if (!npcSaveCadence.Clean && !blockedRecovery && !persistenceFrozenForDiagnostics
        && !TryPersistCandidate(engine.Snapshot, out _, out var unsaved)) failure = unsaved;
    npcSaveSuspended = true;
    message = failure; BeginPersistenceNotice(failure); SuspendNpcWorldWithoutSaving(); Render();
}
```

### 2.2 The cadence: 30 s of the house's free time from its first unsaved operation

A new pure class `Gamesim.Simulation.NpcSaveCadence` (Unity-free, so the dotnet subset tests it in seconds; no
engine code calls it, so no digest moves). One instance per director, never static (memory
statics-outlive-play-sessions).

```csharp
public sealed class NpcSaveCadence
{
    /// The owner's call, 2026-11-01: the most of the house's own life a crash, a kill or a power cut may
    /// cost - or a save held by another program past the store's wait, or refused on what it holds,
    /// both of which ask for a reload (LIVE-SAVE-PLAN section 4).
    public const double Seconds = 30;
    public bool Clean => Unsaved == 0;
    public int Unsaved { get; private set; }          // operations installed since the last save
    public double UnsavedSeconds { get; private set; } // free time since the first of them
    public bool Due => Unsaved > 0 && UnsavedSeconds >= Seconds;
    public void Begin(EpisodeState onDisk)   // the state on disk when the first unsaved operation arrives
    public void Installed()                  // Unsaved++
    public void Elapsed(double seconds)      // counts only while something is unsaved
    public void Saved(EpisodeState onDisk)   // clean; onDisk is what the store now holds
    public void Reset()                      // Install, after the new engine: a load, a recovery or a new slot
    public EpisodeState KnownOnDisk(EpisodeState current) => Clean ? current : onDisk;
}
```

Decisions, with the reasons:

- **Clock: the house's free time, not the NPC clock and not wall time.** `Elapsed` is fed the same `elapsed` that
  feeds `npcFreeSeconds` (`NpcSocial.cs:221`), counted on every 100 ms sample whether or not the clock can tick.
  NPC clock seconds would be wrong: the clock stops while a pending pair walks (:227-233), yet cancellations still
  commit after 20 s of free time (:264-268) and starts keep arriving, so a clock-counted window could hold minutes of
  conversations. Wall time is no better: every way the house stops saves first (2.4), so a window never spans a
  pause, and inside a window free time equals real time except frames over 1 s, which `:218` already refuses as
  "not elapsed social play" and in which the house did nothing.
- **Every kind counts.** Ticks, starts and cancels all open the window. The owner's words were "their conversations
  with each other and the clock": a conversation started or cancelled is that activity, so it is bounded by the
  same 30 s.
- **The window opens at the first unsaved operation**, not at the last save: with nothing owed there is nothing to
  lose. In practice they differ by under a second.
- **Bound.** The flush runs on the first sample at or past 30 s, so the oldest unsaved operation is at most 30.1 s
  of free time old; memory is at most about 31 ticks and the starts and cancels of that half-minute (about 30-35
  revisions) ahead of the disk.
- `npcFreeSeconds` itself is not used: `DisposeNpcSocialWorld` zeroes it (:543) on every disable, suspension and
  load, which would corrupt an age measured against it.

### 2.3 The write: `FlushUnsavedNpcTime(cause, leaving, quiet)` in `EpisodeDirector.Durable.cs`

It reuses the unchanged-session write: `TryPersistCandidate(engine.Snapshot, ...)`, the call `TryAutosave` makes
(`Season.cs:160-165`). That fits the durable layer as it stands: an unchanged snapshot passes the revision rule
(`Durable.cs:55-57`, `candidate.revision == before.revision`), the store's validation, the Editor diagnostic writer,
the slot and generation check (:69-74) and the reconciliation. It does **not** reuse `TryAutosave` itself: that
appends "  ·  Saved locally." to the status line (it would pile up every 30 s), calls `MarkSaveFeedback` on every
success (which clears every notice and shows the save chip, `ConversationExchange.cs:140-147`; the flush calls it only
after a retry, to clear the failure it replaces), and on failure calls `PauseNpcSocialForPanel` today, which would
recurse into this write (after this plan it calls `SuspendNpcWorldWithoutSaving`, below).

```csharp
private bool FlushUnsavedNpcTime(string cause, bool leaving = false, bool quiet = false)
{
    // Nothing owed, or nothing this director may write: not a failure.
    if (npcSaveCadence.Clean || blockedRecovery || durableCommitInProgress || persistenceFrozenForDiagnostics
        || engine == null || saves == null) return true;
    // A house stopped by a failed save is retried where the player leaves it, never on its own clock.
    if (npcSaveSuspended && !leaving) return true;
    bool retry = npcSaveSuspended;
    if (TryPersistCandidate(engine.Snapshot, out _, out var failure))  // Saved() is inside
    { if (retry) MarkSaveFeedback(); return true; }
    if (quiet) { Debug.LogWarning("The house's last seconds could not be saved: " + failure); return false; }
    message = failure; BeginPersistenceNotice(failure); SuspendNpcWorldWithoutSaving(); Render();
    return false;
}
private bool FlushForLeaving(string cause, bool quiet = false) => FlushUnsavedNpcTime(cause, leaving: true, quiet: quiet);
```

It returns false only when it tried now and failed. There are two kinds of caller (2.4):

- **Automatic** (periodic, the stop backstop, hold): skip while suspended. A failure always ends with
  `npcSaveSuspended` or `blockedRecovery` set (`Durable.cs:90`, :71, :93), so nothing on the house's own clock
  retries in a loop.
- **Leaving** (`FlushForLeaving`: a panel opening, focus, pause, quit, a slot change): retry a suspended session
  once per call, the way `SaveNow` already does (`Season.cs:154`, `TryAutosave` writes `engine.Snapshot` when
  suspended). Each is one user action or one window event, and a failed retry ends suspended or locked again, so
  the next such moment tries once more and nothing loops. Without this, every leave point after one failed write
  would skip, memory would stay ahead of the disk for the rest of the session, the menus would show unsaved
  activity and an orderly quit would lose it, where today memory equals the disk in that state (LS-DUR-1). A
  successful retry clears the stale notice with `MarkSaveFeedback`, as `TryAutosave` does (`Season.cs:163`;
  `BeginPersistenceNotice` set `systemNotice`, which only `MarkSaveFeedback` clears,
  `ConversationExchange.cs:140-153`), and, like any successful write (`Durable.cs:75`), lifts the suspension.

The two failure callers, `TryAutosave`'s failure (`Season.cs:164`) and `Submit`'s (`EpisodeDirector.cs:848`), call
`SuspendNpcWorldWithoutSaving()` instead of `PauseNpcSocialForPanel()`. Otherwise the panel path would retry straight
away, and if a hold had just cleared, the retry's `MarkSaveFeedback` would erase the notice of a decision that was
not committed. This is no change today: `PauseNpcSocialForPanel` there runs `FlushNpcWholeTicks` only if
`NpcCanAdvance`, and every failing return of `TryPersistCandidate` leaves that false. The catch sets
`npcSaveSuspended`; the frozen refusal comes after `SuspendNpcAutonomyForDiagnostics` (`Durable.cs:31`); the
reentrancy refusal means `durableCommitInProgress` or no episode; and neither caller can break the revision rule.

`TryPersistCandidate` gains: `npcSaveCadence.Saved(candidate); npcSlotRefused = false;` beside
`npcSaveSuspended = false` at :75 and :87; the reconciliation's comparison target (3.1); and the content wording
(3.1). **The cadence resets in `Install`, right after `engine = new EpisodeEngine(state)` (`Season.cs:418`), with
`npcSlotRefused = false`; never in `ResetNpcSocialForLoad`.** The opening stage calls that method without a load
(`OpeningStage.cs:236` TryPlace, `:252` RestoreHome, after `StartOpening`'s `PauseNpcSocialForPanel`,
`Opening.cs:98`), and it clears `npcSaveSuspended` (`NpcSocial.cs:521`). Resetting there would mark the cadence clean
while memory is ahead of the disk. Panel openers would then skip the write, and the next failure would be judged
against memory instead of the last save, which turns a plain I/O failure into a recovery lock (F4). TryPlace still
clears the suspension, as today. The cadence keeps its owed count and its known state, so the next write retries
them and a failure is judged correctly. `Start` (`EpisodeDirector.cs:151-157`) needs no change: a director begins
clean, and memory equals the loaded file (or no file exists, which the reconciliation already treats as "no
primary").

### 2.4 When the house's seconds are written

| Moment | Where | How | Notes |
|---|---|---|---|
| 30 s of free time | `TickNpcSocialRuntime`, after the ready block (`NpcSocial.cs:228-233`), before `UpdateNpcConversationPresentation` | `if (npcSaveCadence.Due && NpcCanAdvance) FlushUnsavedNpcTime("periodic")` | Automatic. Once per sample, whether or not the pending pairs are ready, so a tick and a start in one sample make one write; the 459/522 ms double-save frames cannot recur. |
| Any decision | `Submit`, `EpisodeDirector.cs:837-851` | Unchanged | `var candidate = new EpisodeEngine(engine.Snapshot)` (:837) is built from memory, deferred seconds included, and `TryPersistCandidate(result.state, ...)` (:842) writes it whole before the result is shown (:853 on). Success marks the cadence clean. Every phase change is a command (PrepareNpcOperation refuses a phase it did not expect, `EpisodeNpcSocial.cs:75-76`), so phase changes are covered here; automatic commands (`AllWeek.cs:144` witnessing, the opening's beats) write the same way. |
| SaveNow (F5, the rail, Settings) | `Season.cs:148-158` | Inside today's `if (!blockedRecovery && !durableCommitInProgress)`: `if (npcSaveSuspended) TryAutosave(); else { FlushNpcWholeTicks(); if (!blockedRecovery && !npcSaveSuspended) TryAutosave(); }` | One write instead of up to two. It saves even when a pending pair is still walking: `FlushNpcWholeTicks`'s false is no longer a reason to skip (today it silently did not save). After a failure this is the retry, and it writes exactly the session memory holds. Today's rule "a NEW failed tick must never fall through to a second, older write" keeps a case: a refusal inside this call's own flush (2.1 step 1). That refusal has already written what was installed and left its notice. Falling through would append "  ·  Saved locally." to the notice, clear it (`MarkSaveFeedback`), lift the suspension (`Durable.cs:75`), and resume a house that refuses again at its next sample (F3). |
| A panel opens | `PauseNpcSocialForPanel`, `NpcSocial.cs:392-396` | `if (NpcCanAdvance) FlushNpcWholeTicks();` (installs), then `FlushForLeaving("panel")`, then `SuspendNpcWorldWithoutSaving()` | Covers every opener: `EpisodeDirector.cs:654` (phase panel), `:766` (settings), `Conversation.cs:240`, `DiaryRoom.cs:97`, `HouseActivities.cs:104`, `Journal.cs:715` (notebook), `Journal.cs:824` (recaps), `JuryHouse.cs:74`, `Opening.cs:98`, `Story.cs:744` (scene card), `Season.cs:196` (main menu); on the offers branch also `ComesToYou.cs:679` (the ask card). A session suspended by an earlier failure is retried here, once per opening. If the retry fails, the panel opens as it does today after a failed flush, with the notice, and it can show house activity that is not on disk (the stated limit, section 4). A retry that enters the recovery lock still refuses the conversation and the phase panel (`Conversation.cs:241`, `EpisodeDirector.cs:655`). The two failure callers no longer come here (2.3). |
| The house stops for any other reason | `TickNpcSocialRuntime`'s paused branch, `NpcSocial.cs:210-214` | `if (!eligible) { if (!npcSaveCadence.Clean) FlushUnsavedNpcTime("stop"); ... }` | Automatic. The backstop: while saves succeed, the house never stands still on seconds only memory holds. Catches what no opener calls: `ShowSeasonReport` (`Journal.cs:860-875`), a failed world (`StopNpcWorld`, :507-517), a lost binding, eligibility lost, and any future panel. At most one frame late; a clean or suspended session costs one bool. After a failed write the owed seconds wait for the next leave point or Save. No more house activity accrues meanwhile, because the house is stopped. |
| Leaving a slot: new season, import | `StartSeason` (`Season.cs:305-308`, after the `durableCommitInProgress` check), `ImportFile` inside its success branch before `importedSlot.Save` (`:398-400`) | `if (!FlushForLeaving("slot") && !npcSlotRefused) { npcSlotRefused = true; failed?.Invoke(message); Render(); return; }` (ImportFile: the same without `failed`, before its `message = result`) | The old slot keeps every second the house lived in it whenever a write can reach the slot, because a session suspended by an earlier periodic, focus or panel failure is retried here too (LS-DUR-1). The house keeps living behind the front door (main menu, cast screen, creator are not panels: `Controls.cs:194-196`, `AllWeek.cs:65-67`), so this can owe up to 30 s. A failed write refuses the start **once per failure**, through the screen's own failure path with the notice. `npcSlotRefused` is cleared by any successful write and by `Install`. The next start tries once more and, if that write fails too, goes ahead with the old slot at its last save, so a persistent failure never traps the player. Under recovery (`blockedRecovery`) nothing is written and the start goes ahead, as today: a new slot is one way out of the lock (section 4). A malformed or missing import writes nothing. The pending fraction is never committed (`NpcRuntime_NewSeasonRetainsOldSlotWithoutFlushingItsPendingTime`'s rule). `StressHouseTests.cs:288-315` reads `StartSeason` from its signature to `nextStore.Save(fresh)`: the new line names no `choice`, `fresh.` or `.Enable`, so it passes. |
| Reload or recover the same slot | `LoadNow`, `RecoverBackup` (`Season.cs:167-183`) | **No write** | Both mean "take what is on disk". A write before `RecoverBackup` would rotate the very backup being recovered (`File.Replace` moves the primary over `.backup`, `EpisodeSaveStore.cs:126`). Both are reachable only from Settings (`Season.cs:91-92`), which saved on opening and holds the clock, so in play they lose nothing; tests that write a fixture and call `LoadNow` stay safe (e.g. `EpisodeNpcScanCadenceTests.cs:109-110`). Pinned by `NpcRuntime_LoadIsNonwritingEvenWithAWholeUncommittedSecond` and `NpcRuntime_RecoveryReadsOriginalBackupBeforeAnyTimingWrite`. |
| Switching away | new `OnApplicationFocus(bool focused)` in `Durable.cs` | `if (Application.isEditor && SaveRootOverride != null) return; if (!focused && IsReady) FlushForLeaving("focus");` | The player ships with `runInBackground: 0`, so nothing advances while unfocused; this is the last moment before a Task Manager kill or sleep. **Not in Editor test runs.** In the Editor, focus moves with the Game view, the Inspector and other apps. In the owner's editor over the MCP, with `runInBackground` on, this would add writes at moments nobody controls, inside tests that count writes or compare bytes on an unheld house (7.1's `writes == 1` rows, the Hear-press bytes check, tests 4, 8 and 10), through the test's own diagnostic writer (`Durable.cs:62-66`) (LS-DUR-4). Every test and every Editor verification sets `SaveRootOverride` (`EpisodePlayModeTests.cs:41`, `EpisodeDurableTransactionTests.cs:43`); the verification player sets it too but is not the Editor; the owner's own Editor play has none, so it keeps the hook. The Editor gains little from it anyway: an Editor crash loses the window either way, and play-mode exit is `OnApplicationQuit`. Test 6 calls `FlushForLeaving("focus")` directly; test 17 pins the gate. |
| Minimise / OS pause | new `OnApplicationPause(bool paused)` | The same gate, then `if (paused && IsReady) FlushForLeaving("pause");` | Usually follows the focus call and finds nothing owed. |
| Quit | new `OnApplicationQuit()` | `if (IsReady) FlushForLeaving("quit", quiet: true)` | Player and Editor alike. It retries a suspended session too, so an orderly quit after an earlier failed write still writes what the menus showed (LS-DUR-1). See below. |
| Editor play-mode exit | `OnApplicationQuit` | Same | Unity sends `OnApplicationQuit` when play stops in the Editor, before `OnDisable`, whatever the domain-reload setting. |
| Diagnostics | `SuspendNpcAutonomyForDiagnostics` (`NpcSocial.cs:103-107`), `FreezeForReloadForDiagnostics` (`Durable.cs:28-33`) | Suspend: `FlushUnsavedNpcTime("hold")` (automatic: it skips a suspended session, so a failure test's hook is not called again by its own hold) before disposing. Freeze: set `persistenceFrozenForDiagnostics = true` **before** calling Suspend. | Suspend stops the house, so it writes what is owed, which keeps the 180 `HoldTheHouseForTheFixture` sites (all `SuspendNpcAutonomyForDiagnostics`, `FinaleReveal.cs:26`) seeing disk equal to memory as today. Freeze must write nothing: tests write a fixture to the save path, then freeze (`EpisodePlayModeTests.cs:382-392`, `UnifiedSafetyDurability.cs:471-473`). |

**Quit: `OnApplicationQuit`, not `Application.wantsToQuit`.** `OnApplicationQuit` is a per-object message: it runs
on every orderly quit (the menu's Quit, `Season.cs:232-240`; the window's close; Alt+F4) and when the Editor stops
playing, while the scene is still intact, and Unity waits for the synchronous write to finish. `wantsToQuit` is a
static event for vetoing a quit: it needs registering and unregistering across play sessions with domain reload off
(memory statics-outlive-play-sessions), and this write never needs to cancel the quit. A failed write at quit only
logs a warning to the player log: the player chose to leave, and blocking the quit would trap them in a failing
game. A kill or a power cut cannot run any of this; that is the 30 s the owner accepted.

**No write in `OnDisable`/`OnDestroy`.** In the shipped game the director is disabled only at quit (the bootstrap
loads the house once, `GamesimBootstrap.cs:39-44`), after `OnApplicationQuit` has written. In a scene unload,
sibling roots may already be gone ("Never rebuild UI here", `EpisodeDirector.cs:1703`), and a failed write's path
(`EnterRecoveryLock`, `Durable.cs:103-110`) touches the player and camera, whose fake-null `?.` does not protect
(memory unity-fake-null-breaks-null-coalescing). Test teardowns would add a write per test, and `ReloadEpisode` is
frozen anyway. The Editor's script reload during play leaves the director a zombie whatever we do (memory
editing-scripts-during-play-mode-zombies-the-director).

**What the player can read without a panel.** The house's own HUD reads memory: the witnessed caption and the
Nearby card (`NpcSocial.cs:415-498`, `:85-90`), a story's Pull, an incoming offer's bubble, the public displays
`Project()` paints (`EpisodeDirector.cs:1207-1235`). These can show up to 30 s of house activity before it is on
disk; on a crash it may replay differently. That is the accepted loss: it is the houseguests' own activity, and
acting on any of it opens a panel or submits a command, which writes first.

**Considered and rejected:** writing on panel open only when the unsaved operations include a start, end or cancel
(cheaper, but it breaks the one-sentence promise "what a menu shows is on disk"); a separate thread (option 2);
refusing every panel while a retry fails (it would keep Settings, the way to Save or reload, from opening, and today
a failed flush does not refuse panels either). The promise therefore holds while saves succeed. While they fail, the
notice says so, and each opening tries again.

## 3. The durable layer when memory is ahead

### 3.1 The reconciliation (rewritten)

`Durable.cs:91`: `if (hasPrimary && (!readable || !EquivalentState(disk, before)))` enters the recovery lock. With
seconds owed, `before` (memory) is ahead of the disk, so every failed write - a periodic flush or a decision - would
find the disk "different from the session" and lock the slot. The comparison becomes
`EquivalentState(disk, known)` with `known = npcSaveCadence.KnownOnDisk(before)`, taken at :53:

- clean session: `known == before`, today's rule exactly (`DifferentValidatedPrimary_*`, `UnreadablePrimary_*` and
  `ReentrantSubmit...` unchanged);
- seconds owed: `known` is the state this director last wrote or loaded, so a write that failed before touching the
  file is a plain failure (notice, house stopped, retry or reload), and only a disk that is unreadable or differs
  from both the candidate and the last save is a recovery case.

`known` is a clone taken by `engine.Snapshot` when the window opened (once per 30 s, a few ms) or the candidate of
the last successful write; nothing mutates either afterwards (`EpisodeEngine.cs:14, :29` clone on read and
construct). The first branch (:84-89, the write landed and an exception came after) is unchanged and also marks the
cadence clean.

Two more rules in the catch:

- **A refusal of what memory holds says "reload".** Suppose the candidate is the session itself
  (`candidate.revision == before.revision`: a flush or `SaveNow`), seconds are owed, and the error is about content
  (`SaveJson.IsExpected(error) && !SaveJson.IsFilesystemError(error)`: the read-back's `InvalidDataException`, or a
  `JsonException` and the like). Then memory itself was refused, and "retry Save" can never succeed. The plain-failure
  text becomes "SAVE NEEDS ATTENTION: " + `Explain(error)` + " The house's latest seconds could not be saved. Reload to
  go back to your last save; the houseguests will live those seconds again." A decision's failure keeps today's
  text, because its own result may be what was refused. The retry Save it suggests then gets the reload text if
  memory is the problem (LS-DUR-3, F5). No test matches the old text's tail (`git grep "retry Save"` in Tests finds
  nothing), and the `SAVE NEEDS ATTENTION` prefix is kept.
- **A held primary stays a recovery case.** If the primary is held past the store's wait, `Save` throws "could not
  be opened to check it" (`EpisodeSaveStore.cs:51-57` on origin/main). The catch's `TryLoad` fails on the same hold
  (`TryRead` sets `unopened`, :185-189), so `hasPrimary && !readable` enters the lock. That is pinned on purpose
  (`ConversationInformation_FailedSavePublishesNeitherOpinionsNorMeetingReads`, `ConversationInformation.cs:66-77`:
  `SeasonInProgress` false, `LoadNow` the way out) and kept. With seconds owed, every way out of the lock (LoadNow,
  RecoverBackup, a new slot) drops them, so a backup, sync or antivirus hold during a house write now costs up to
  30 s of house activity with no crash, where today the same lock costs nothing (LS-DUR-2). Sections 4 and 5, the
  owner summary and `NpcSaveCadence.Seconds` name this, and test 14 pins it. Keeping the seconds instead, by treating
  `SaveJson.IsFilesystemError(error) && !readable` as a plain failure for a dirty session, relaxes a pinned rule. It
  needs the owner's call first and is not in this plan.

### 3.2 The scan rule (rewritten)

"Only a durably committed scan deadline can schedule NEW routes" (`NpcSocial.cs:384-385`) becomes "an installed
one". The code already plans after installing (:386-388), so only the comment changes. Routes are transient world
reservations: if the window is lost in a crash, a reload has no approaches and the scan deadline on disk, and plans
again from there.

### 3.3 Everything else the layer compares

| Check | Behaviour with memory up to ~35 revisions ahead |
|---|---|
| Reentrancy and null (:46-47), frozen (:48-49) | Unchanged; the flush checks them first and reports nothing. |
| Session and revision (:55-57) | Compares the candidate with memory (`authority.Snapshot`), never with the disk: a flush is `revision == before.revision`, a decision `before.revision + 1`. |
| The store (`EpisodeSaveStore.Save`, :35-62) | Reads the primary only to prove it readable (:51); no revision, clock or timestamp is compared. A write jumps the file by up to ~35 revisions at once. |
| Backup rotation (:126, `File.Replace`) | `.backup` becomes the previous save: up to ~30 s or one decision older, not ~1 s. `RecoverBackup` restores that. |
| Slot and generation after the write (:69-74) | Synchronous; unchanged. |
| `PrepareNpcOperation`'s `expectedRevision`/`expectedClockTick` (`EpisodeNpcSocial.cs:75-76`) and the request's `Generation` (`NpcSocial.cs:365, :380`) | Memory against memory; unchanged. |
| `loadGeneration` bumps in `DisposeNpcSocialWorld` (:532) | Suspend writes before it disposes; `Install` resets the cadence after its new engine; the opening stage's `ResetNpcSocialForLoad` (no load) leaves the cadence alone (2.3). |

## 4. Failure

- **A failed write on the disk** (periodic, panel, stop, focus, pause, slot, quit): `TryPersistCandidate`'s catch
  sets `npcSaveSuspended` (:90) and returns today's text (:96-97, "SAVE NEEDS ATTENTION: ... retry Save or
  reload."). The flush shows it with `BeginPersistenceNotice`, stops the world (`SuspendNpcWorldWithoutSaving`) and
  renders; at quit it only logs. `NpcCanAdvance` is false while suspended (:98-100), so no clock, starts, cancels,
  routes or automatic writes run.
- **Retries.** Memory keeps every installed operation. The explicit retry (`SaveNow` -> `TryAutosave`), the next
  decision's write and every leave point (a panel opening, focus, pause, quit, a slot change: 2.3) write exactly
  that state, one attempt per action. On success `npcSaveSuspended = false` (:75), the notice clears and the house
  resumes. Nothing re-applies: each operation was installed once, a write only copies memory to disk, and
  `PrepareNpcOperation` refuses a second tick to the same target or a second start of the same sequence as a
  duplicate (`EpisodeNpcSocial.cs:54-72`).
- **While writes keep failing**, a menu opened shows house activity that is not on disk (at most the owed window: the
  house is stopped, so nothing more accrues), under the failure notice. A slot change is refused once per failure,
  then allowed.
- **A refusal of what memory holds** (3.1): the house stops and the text says reload. Every decision fails too,
  because each is built from memory (`EpisodeDirector.cs:837`), until the player reloads, which drops the owed
  seconds. Today the offending operation is never installed and decisions keep saving. This is the cost of not
  running the store's whole read-back before installing (2.1 step 1), it needs a bug to happen at all, and test 15
  pins it. An automatic rollback to the last save was considered and rejected (section 13, F5).
- **A held primary** (3.1): the recovery lock, as today; the way out drops the owed seconds.
- Reloading instead of retrying discards the owed seconds, exactly as a crash would.
- Recovery behaviour is unchanged: `DifferentValidatedPrimary` and `UnreadablePrimary` still lock (3.1), the
  recovery modal still blocks input, `LoadNow` and `RecoverBackup` still read without writing.
- A refused installation (2.1 step 1) writes what is installed, then stops the house before installing the refused
  one, with today's text.

## 5. Load and crash

- **Transient, never saved:** `npcWorldFraction` (< 0.1 s), `npcFrameFraction` (< 1 s), the cadence (unsaved count,
  age, last-saved state), route approaches, obstruction timers, `npcFreeSeconds`, the pending leases (rebuilt from
  the saved pending rows by `RestorePendingNpcMeetings`, :237-248). The rule "the remaining fraction is transient"
  widens from under a second to "the house's installed operations of up to 30 s of free time".
- **A reload** (`LoadNow`, or a new launch after a crash) installs the last save: everything up to the last
  decision, panel, pause, slot change or periodic write, plus at most nothing else. A crash in free time costs at
  most 30.1 s of house activity and never a decision (each decision's write carries everything before it). The same
  bound, and the same "never a decision", holds for the two non-crash losses of section 4: a held primary's recovery
  lock and a refusal of what memory holds, both left by a reload.
- **Replay** from the reloaded state is a fresh run, which the owner accepted: the NPC random stream
  (`npcSocial.randomState`) is restored exactly, so the same operations would draw the same values, but which pair
  meets when depends on real-time walking and arrival, so the seconds play out a little differently (memory
  season-walks-do-not-fix-outcomes). Saved pending conversations walk back to their venues without new draws, as
  today.

## 6. The verify-only commit timing log

Purpose: the in-player measurement the investigation still lacks (the split of each commit and the untimed
54-78 ms), before and after this change. Land it as the first commit, with no behaviour change, so the lead can run
r-profile16 once on it if the lane allows.

- **Switch.** A field on the director, `[NonSerialized] internal NpcCommitTimings commitTimingForVerification;`,
  null by default. It is internal, like `saveCandidateForDiagnostics` (`Durable.cs:18`): `PortVerification` is in the
  same Runtime assembly, and tests reach it by reflection. `NpcCommitTimings` is **not** `[Serializable]`, whatever
  this file's report classes do (`[Serializable] private sealed class VerificationReport`, `PortVerification.cs:362`):
  Unity would deserialize a serializable class in a MonoBehaviour field into a non-null instance in the scene's
  director, and every ordinary session would record rows. `PortVerification.Start` sets the field once the ready
  director is found, right after `director.SetFrameCap(ProfileFrameCap)` (`PortVerification.cs:100`).
  `PortVerification` exists only in a player under `--gamesim-verify` with an absolute `--gamesim-save-root`
  (`PortVerification.cs:42-77`, `:45` returns in the Editor), so ordinary play and every Editor session leave it
  null. The off path costs one null check per operation and per write:
  `var t = commitTimingForVerification; long began = t != null ? Stopwatch.GetTimestamp() : 0;`. No lambdas, no
  `params` arrays and no boxing on that path.
- **Pure class.** Its record methods take `frame`, `seconds` and `revision` from the director, which reads
  `Time.frameCount` and `Time.realtimeSinceStartupAsDouble` itself. `ToCsv()` and the row list touch no UnityEngine
  API; the file write and its warning are the director's. That lets test 3 run under the Mono harness.
- **What it records (commit 2, after option 1).** `Stopwatch.GetTimestamp()` pairs, in ms:
  - per house operation (`CommitNpcOperation`): `prepareMs` (PrepareNpcOperation), `validateMs` (the bounds check
    of 2.1), `engineMs` (new EpisodeEngine), `projectMs` (Project), `planMs` (PlanNpcApproaches, or empty), with
    `kind` = tick, start or cancel. Timed around the call, never inside `PlanNpcApproaches` (whose body
    `NpcPairingTests.cs:291-304` reads as text);
  - per write: `persistMs` around `TryPersistCandidate`, with `cause` = periodic, panel, stop, focus, pause, quit,
    slot, hold (the flush), refuse (2.1's first write), save (`TryAutosave`) or command (`Submit`,
    `EpisodeDirector.cs:842`), and `saved` true/false;
  - on every row: `frame`, `seconds`, `revision`.
- **What it records in commit 1 (today's code, the "before").** Per house operation: `kind`, `prepareMs`,
  `persistMs` (with `cause` = the kind, timed inside `CommitNpcOperation` around today's `TryPersistCandidate` at
  :378), `engineMs`, `projectMs`, `planMs`, with `validateMs` empty. Per `TryAutosave` and `Submit` write: one row
  with `cause` = save or command. Commit 1 adds an `OnApplicationQuit` that only writes the CSV; commit 2 puts the
  quit write (2.4) before it. The header is the same in both, so one reader serves the before and after runs.
- **Where it goes.** Rows stay in a pre-sized list (no I/O during the sample). The director writes
  `npc-commit-timing-ms.csv` into the run's save root from `OnApplicationQuit`, after the quit write, so both RS0
  modes (the profile, which quits from `Finish`, `PortVerification.cs:271-279`, and room scope's views/live mode,
  which also quits) produce it beside `verification.json` and `profile-frame-times-ms.csv` without touching either
  writer. Header: `frame,seconds,kind,cause,revision,prepareMs,validateMs,engineMs,projectMs,planMs,persistMs,saved`,
  invariant culture, `"R"` for numbers as `WriteRawFrameTimes` does (`PortVerification.Performance.cs:145-166`). An
  IO failure logs one warning.
- **Lining rows up with frames.** A commit in frame F's Update shows in the frame time recorded at F+1 (the profile
  loop records `Time.unscaledDeltaTime` in a coroutine after Update, `PortVerification.cs:214`). Add
  `steadySampleFirstFrame = Time.frameCount` in `BeginSteadySample` (`PortVerification.Performance.cs:86-89`) and a
  `steadySampleFirstFrame` field on the report (`PortVerification.cs` VerificationReport, near :376): the matching
  frame time is the frames CSV's data row `frame + 1 - steadySampleFirstFrame`, counted from 0 under its `ms`
  header, which is its line `frame + 3 - steadySampleFirstFrame` counted from 1 (review LSB-2). Readers can also use
  the report's existing `startupSampleBeganSeconds` with `seconds`.
- **Class.** `public sealed class NpcCommitTimings` in `Runtime/Episode` (public so the EditMode test can build one:
  the tests' assembly has no InternalsVisibleTo, `ProspectiveVoteFacade.cs:12`), not `[Serializable]`, pure C#,
  with `ToCsv()` testable under the Mono harness. Only the director's field is internal.

## 7. Tests

PlayMode tests cannot run until the Unity lane frees. Names are kept where their claim still holds, renamed where
it no longer does (the floors count tests, so renames do not move them).

### 7.1 Rewrites (PlayMode)

| Test (file:line) | Old expectation | New expectation |
|---|---|---|
| Helper `BindNpcScanWorld`, `EpisodeNpcScanCadenceTests.cs:187-204` | The default fixture has no save file until the first ticks write one (`EpisodeDirector.cs:151-155` writes nothing on a fresh root); today the first two ticks create the primary and the `.backup`. | After :193's `OpenSettings` and before :194, `director.SaveNow(); director.SaveNow();` creates the primary and the `.backup` while Settings holds the house, and leaves the cadence clean. SaveNow changes no state, so :199 holds (F2). |
| Helper `CommitNpcScanSecond`, `:206-215` | After each `FlushNpcWholeTicks` the disk equals memory (:213-214). | Memory gains one tick and one revision; the disk's revision, read before and after through `TryLoad` (the file exists since bind), is unchanged. Tests that need the disk call `SaveNow`. Used by all five cadence tests. |
| `NpcScan_FreshClockDoesNotPlanBeforeThirdDurableSecond`, `:22-52` | Routes appear at the third durable second. | Same, at the third installed second; rename `...BeforeThirdInstalledSecond`. |
| `NpcScan_FailedThirdSecondDoesNotReserveRoutesBeforeExplicitRetry`, `:54-94` | `SaveNow` with a whole second owed: the tick's write fails, memory stays at 2 (:76), no routes (:80); the retry later commits the tick. | The new body, line by line (F1). Under the new `SaveNow`, `FlushNpcWholeTicks` installs tick 3 and plans its routes (`npcApproachDiagnosticsSuppressed` is false after bind) **before** `TryAutosave` calls the hook. So the hook's :68 `AssertNoNpcScanApproaches()` would throw an NUnit `AssertionException`, which `SaveJson.IsExpected` does not catch, out of `SaveNow`. In the new body: :61-62 read the files bind made. The hook keeps `writes++` and :67, replaces :68 with `AssertNpcScanCreatedRealApproaches()` (installed and planned before the write), and throws the `IOException`. :75 `writes == 1`. :76 becomes `clockTick == 3` and `revision == before.revision + 1`. :77-78 (files unchanged) and :79 (suspended) hold. :80 becomes `AssertNpcScanCreatedRealApproaches()`. Then `long counter = npcLeaseCounter;` before :81's `TickNpcSocialRuntime(.5f)`. :82 holds (`writes == 1`: the backstop skips a suspended session). :83 becomes `AssertNpcScanCreatedRealApproaches()` plus `npcLeaseCounter == counter` (no second scan). After the `finally`: :87's `SaveNow` writes clock 3 through the suspended branch, and `TryLoad` then equals the snapshot. :88 holds. :89's `TickNpcSocialRuntime(.1f)` leaves clock 3 (:90), `nextScanTick` 6 (:91), the same approaches (:92), `npcLeaseCounter == counter`, and the NPC stream unchanged (:93). If the paused frame's wandering (`TickWandering`, `NpcSocial.cs:214`) moves a reserved pair, record it and do not loosen the check (measure, don't reason). Rename `NpcScan_AFailedWriteAtTheThirdSecondKeepsItsRoutesPausedUntilTheRetryWritesIt`. |
| `NpcScan_LoadedFirstAndThirdSecondsKeepTheirRemainingSavedCadence`, `:96-125` | Through the helper. | Helper change only; `LoadNow` still writes nothing over the checkpoint (:109-110). |
| `NpcScan_ApproachesAreNpcPairingsHeldTriesInOrder` `:133-162`, `NpcScan_ModalObservationAndExplicitFlushDoNotAdvanceClockOrPlanRoutes` `:164-185` | Through the helper. | Helper change only (`OpenSettings` now writes the two owed seconds; nothing there reads the disk). |
| `NpcRuntime_RealArrivedCompletionFailureKeepsPendingAndRetryUsesIdenticalRandomEffects`, `EpisodeNpcCompletionTransactionTests.cs:15-81` | The completion's write fails; memory keeps the pair pending; the retry recompletes with identical draws. | First, after :34's `FlushNpcWholeTicks` (the fraction is then below 1 and `npcWorldFraction` is 0), call `director.SaveNow(); director.SaveNow();` before :35-37 captures `before`, `primary` and `backup`. The wait at :19-22 can end before the first periodic write (about 31 s), which would leave no file, or before the second (about 61 s), which would leave no `.backup` (F2). Then the completion installs with no write (`writes == 0`, `FlushNpcWholeTicks` true, pending fewer, NPC stream moved, main stream unchanged, disk unchanged); a `SaveNow` with the failing hook: `writes == 1`, `attempted` equals memory, files unchanged, suspended, caption empty; the retry writes exactly `attempted`; later ticks never repeat the completion. Rename `...CompletionInstallsOnceAndAFailedWriteRetriesTheSameState`. |
| `UnifiedSafety_RealArrivedNpcCompletionSavesBeforeInstallationAndRetryDoesNotRepeatEffects`, `UnifiedSafetyDurability.cs:277-351` | :296 the start is on disk (`UsAssertDisk(started)`); :316-330 as the row above; :344 disk after the tick. | :296's `UsAssertDisk(started)` pinned the old rule and goes. After :304's flush, `director.SaveNow()`; then :305's `before`, a new `UsAssertDisk(before)`, and :306's `retained = UsImage()`. `UsStartFresh` wrote the primary, and `UsImage` images the whole directory, so a missing `.backup` is harmless. :316-330 as the row above; :344 after the retry `SaveNow`; :347-350 hold (Suspend now writes first). Rename `...CompletionInstallsOnceAndAFailedWriteRetriesTheSameState`. |
| `SaveNowFailedWholeNpcTick_DoesNotFallBackToWritingOldSnapshotOrResumeAutomatically`, `EpisodeDurableTransactionTests.cs:297-325` | Memory equals `before` (:320). | Memory is `before` plus one installed tick (clock +1, revision +1, both streams unchanged); :309-310, :319, :321-323 hold; add: a second `SaveNow` writes exactly that state through the hook's second branch and clears the suspension, with no further tick. The name's claim holds. |
| `SaveNowAmbiguousWholeNpcTick_CannotBypassNewRecoveryLockWithSecondWrite`, `:327-356` | Memory equals `before` (:351). | Memory is `before` plus one installed tick; the foreign primary still locks recovery; one write. |
| `NpcRuntime_QueuedRequestCannotCrossSameSessionIdenticalRevisionReload`, `EpisodeNpcRuntimeRegressionTests.cs:102-138` | :136-137 disk equals memory after the fresh commit. | Disk still equals `before`; `SaveNow`, then disk equals memory. |
| `NpcRuntime_ExplicitFractionalFlushCommitsPointNineFivePlusPointZeroSixExactlyOnce`, `:140-168` | :162-163 disk equals `after`. | Disk equals `before`; :164-167 hold; `SaveNow`, then disk equals `after`. |
| `NpcRuntime_NewSeasonRetainsOldSlotWithoutFlushingItsPendingTime`, `:228-255` | Old primary and backup bytes unchanged (:249-250). | Made deterministic: install one tick in memory first, then set the 1.01 s fraction; after `StartSeason` the old slot holds the installed tick and not the fraction's (clock = installed, not +1), its backup is the test's previous primary; :251-254 hold. Rename `NpcRuntime_NewSeasonWritesTheOldSlotsInstalledSecondsButNotItsPendingTime`. |
| `NpcRuntime_LoadIsNonwritingEvenWithAWholeUncommittedSecond` `:170-190`, `NpcRuntime_RecoveryReadsOriginalBackupBeforeAnyTimingWrite` `:192-226` | No write. | Unchanged expectations, strengthened: install one second in memory first, so the session owes a write, and still nothing is written. |
| `DialogueIncomingOfferRoute`, `EpisodePlayModeTests.DialogueIncomingOffer.cs:63-90` | The Hear press writes nothing: bytes at :86 equal after :89 (:90). | The press writes the house's owed seconds and nothing of its own: memory unchanged (JSON equals `waiting`, revision unchanged) and the disk equals `waiting` as a state. The `NpcWholeSecondDue` wait (:71-73) stays (it keeps the revision still); comments :64-70 and :247-249 rewritten. One of the three known flakes (memory npc-autonomy-is-the-playmode-flake): measure, don't reason. |

**Audit rule for the rest.** A test that passes today with "bytes unchanged" across an interval had no house
operation in it (every operation wrote). Under the new rule it can break only when the house ran **before** the
capture with no write since, and a write point (panel open, Suspend, `StartSeason`, focus, the paused-branch
backstop) falls inside the interval; and a "disk equals memory" check breaks when the house ran since the last
write with no write point before the read. A third case: **a test that reads `SavePath` or its `.backup` after
house-only activity in the default fixture must first create them with `SaveNow`** (twice if it reads the
`.backup`). On a fresh root nothing writes until the first ticks do today (`EpisodeDirector.cs:151-155`), and under
the new rule until 30 s of free time, a panel or a Save (F2). The builder greps every `ReadAllBytes(director.SavePath`,
`.backup"` and `TryLoad` in Tests/PlayMode (84 files and 364 lines name `director.SavePath` or `.backup"` at
37416049, writes included) for a read with no fixture write, `StartSeason`,
`SaveNow` or held-house fixture before it. Most disk-reading fixtures hold the house first
(`HoldTheHouseForTheFixture`, `InstallTalkingHouse`, `UsStartFresh`, the durable fixture's SetUp), so they are
unaffected; the surveyed exceptions are the rows above. Recheck after the merges: the offers branch's tests ("every
install writes the NPC clock it met", c5cdb972) and anything new since 37416049.

### 7.2 New tests

EditMode, dotnet subset (`Tools/SimulationTests`; Simulation is compiled by its glob, the test file needs a
`<Compile Include>` line in `SimulationTests.csproj`):

1. `NpcSaveCadenceTests` (new file): clean at start and after `Reset`; `Elapsed` before any operation counts
   nothing; the window opens at the first `Installed` and `Begin` is taken once per window; the boundary with exact
   binary values, `Elapsed(29.75)` not `Due`, then `Elapsed(0.25)` `Due` (never a sum of 0.1s, whose rounding decides
   the boundary); ticks, starts and cancels all count (it is kind-blind); `Saved` makes it clean and moves the known
   state; `KnownOnDisk` returns the current state when clean and the begun state when owed.
2. *Dropped* (F6): an engine-only replay passes on main and cannot see a director reordering, because the engine's
   only edits are a comment and an unread reason string. The director-level check is acceptance's
   `--gamesim-verify-autonomy` run (10.3), whose `ObserveAutonomyStep` (`PortVerification.Autonomy.cs:146-183`)
   replays every observed revision step against a detached engine and requires exact equality. Its Settings
   save-and-reload (:81-100) also crosses the new write.

EditMode, Mono harness (pure):

3. `NpcCommitTimingsTests`: the header, one row per call in order, invariant numbers, empty fields for parts that
   did not run, nothing recorded until asked.

PlayMode (new partial `EpisodePlayModeTests.LiveSave.cs`; unscaled time is not pinned by `Time.captureDeltaTime`,
`EpisodePlayModeTests.Pause.cs:18-21`, so waits are real seconds and bounded):

4. `LiveSave_EveryInstalledRevisionIsOnDiskWithinThirtySecondsOfFreeTime` - a counting pass-through writer (it saves
   the candidate through a fresh `EpisodeSaveStore` and records `candidate.revision` and
   `Time.realtimeSinceStartupAsDouble`); no disk read per frame (a `TryLoad` a frame would stretch every frame).
   Each frame for 60 s the test records `director.Snapshot.revision` when it changes, with the time, and sums as free
   time the unscaled deltas of frames up to 1 s (the house's own rule, `NpcSocial.cs:218`). It asserts that every
   installed revision reaches a write within 30 s of free time plus one sample (31.2 s), and that there are at
   least one and at most three writes. The clock's lead over the disk is not the measure: the clock stops while a
   pending pair walks (`NpcSocial.cs:227-233`), so it does not age the unsaved activity, and a cadence fed from the
   clock would pass. The variant that would catch exactly that runs on the unarrived-pair fixture of
   `NpcRuntime_MaximumSessionId...` (`EpisodeNpcRuntimeRegressionTests.cs:63-100`): clock stopped, nothing
   installable. It primes the director's cadence through its own public methods (`Begin(director.Snapshot)`,
   `Installed()`, `Elapsed(29.5)`, on the instance read by reflection) and requires one write within about a
   second anyway, with the clock still stopped (F6).
5. `LiveSave_ADecisionAfterTenUnsavedSecondsWritesThemWithIt` - after ticks owed, a `Submit` (Talk); the disk's clock
   equals memory's.
6. `LiveSave_EveryWayTheHouseStopsWritesItsSecondsFirst` - one case each, on a session that owes seconds: notebook,
   settings, a conversation (`TryOpenNpc`), the phase panel, `FlushForLeaving("focus")` and `FlushForLeaving("pause")`
   (called directly by reflection; the Unity messages are gated off in Editor test runs, test 17),
   `OnApplicationQuit` (quiet: no notice), `SuspendNpcAutonomyForDiagnostics`, a stopped world (the backstop, via
   `TickNpcSocialRuntime(0f)`, which reaches the paused branch before the delta check), and a phase change
   (`Submit(Advance)`): disk equals memory right after, revision unchanged by the write itself.
7. `LiveSave_ACrashLosesAtMostThirtySecondsOfTheHouseAndNoDecision` - a decision, then about 40 s of free time,
   **crossing** the 30 s write, with the install times recorded as in test 4. Then `FreezeForReloadForDiagnostics`
   (the outgoing director writes nothing, like a kill) and `ReloadEpisode`. The loaded revision is at least the
   decision's and covers every operation installed more than 31.2 s of free time before the freeze. Killing short of
   30 s would make "lost at most 30" trivially true.
8. `LiveSave_AFailedPeriodicWriteStopsTheHouseAsAPlainFailureAndTheRetryWritesTheSameState` - with seconds owed and a
   throwing writer: suspended, notice shown, **not** recovery-locked (pins 3.1), files unchanged, the clock frozen
   over later frames, and no further write from the periodic flush or the backstop; the retry `SaveNow` writes the
   exact state memory held at the failure, revision unchanged.
9. `LiveSave_SaveNowWritesWhileAPairIsStillWalking` - on the unarrived-pair fixture of `NpcRuntime_MaximumSessionId...`
   (`:63-100`), with `npcFrameFraction` .95 and `npcWorldFraction` .06 as at :90-91, and no seconds owed (nothing can
   be installed there: a tick is refused while a lease is unarrived, `NpcSocial.cs:375`; a start needs an arrival; a
   cancel needs 20 s of obstruction, :264): `SaveNow` makes one write (`writes == 1`) although `FlushNpcWholeTicks`
   returns false (today it made none), and both fractions are unchanged.
10. `LiveSave_ANewSeasonWhoseOldSlotCannotBeWrittenRefusesOncePerFailure` - owed seconds and a throwing writer:
    `StartSeason` refuses with the notice and the old slot unchanged; a second start writes once more, fails and
    goes ahead with the old slot at its last save. Then, in a fresh case, a failed periodic write (suspended) and a
    working writer: `StartSeason` retries, writes the old slot equal to memory, and starts.
11. `LiveSave_TheCommitTimingsRecordEachPartOnlyWhenAsked` - null by default and nothing recorded; set by reflection,
    a tick row has prepare, validate, engine and project times, a flush row a persist time and its cause.
12. Extend `Reload_ADirectorFrozenForAReloadWritesNothingToItsSavePath` (`EpisodePlayModeTests.cs:364-380`): a frozen
    director with seconds owed writes nothing at `SuspendNpcAutonomyForDiagnostics` or `OnApplicationQuit` either.
13. `LiveSave_AfterAFailedWriteTheNextWayOutWritesWhatTheMenusShow` (LS-DUR-1) - owed seconds, a periodic flush whose
    writer throws `IOException` (a plain failure, suspended), then the writer replaced by a working pass-through.
    Three cases: (a) `OpenJournal`: one write, disk equals memory, the notice cleared (`StatusMessage` no longer
    says "SAVE NEEDS ATTENTION"), the house resumes when the panel closes; (b) `OnApplicationQuit`: one write, disk
    equals memory; (c) `StartSeason`: the old slot (its path read before the start) equals the memory captured before it. A fourth case with the
    writer still throwing: `OpenJournal` makes one attempt, opens, and the notice stands.
14. `LiveSave_AHeldSaveWithSecondsOwedLocksAndTheReloadReturnsToTheLastSave` (LS-DUR-2) - owed seconds; capture
    `known` (the disk's state); inside `using (new FileStream(SavePath, FileMode.Open, FileAccess.Read,
    FileShare.None))`, invoke `FlushUnsavedNpcTime("periodic")` by reflection: recovery-locked, `SeasonInProgress`
    false, file bytes unchanged. After the hold, `LoadNow`: the loaded state equals `known` and the house resumes.
    Under Mono every share mode gives the same plain IOException (memory a-held-save-file-is-a-plain-ioexception),
    so `FileShare.None` is the simple hold.
15. `LiveSave_ASaveRefusedOnWhatTheHouseHoldsAsksForAReload` (LS-DUR-3, F5) - a writer that throws
    `InvalidDataException` for every candidate at or past clock N, then owed seconds past N and a periodic flush:
    suspended, not locked, the text says "Reload"; a `Submit` (Talk) is refused, not committed, with today's
    decision text; `SaveNow` fails with the reload text; `LoadNow` restores `known`, the house resumes, and with the
    writer removed the next decision commits.
16. `LiveSave_ARefusalInsideSaveNowWritesWhatIsInstalledAndKeepsItsNotice` (F3) - `npcOperationCheckForDiagnostics`
    throws `InvalidDataException` for the next tick; with seconds owed and a whole second due, `SaveNow`: exactly
    one write, of the installed state (not the refused tick); suspended; `StatusMessage` shows the refusal and no
    "Saved locally"; memory unchanged by the refused tick. With nothing owed: no write at all. A second `SaveNow`
    (explicit retry) writes through the suspended branch, as today.
17. `LiveSave_FocusAndPauseDoNothingInEditorTestRuns` (LS-DUR-4) - owed seconds and a counting writer;
    `OnApplicationFocus(false)` and `OnApplicationPause(true)` invoked by reflection: no write, cadence still owed.

Must still pass unchanged: `DifferentValidatedPrimary_EntersRecoveryAndCannotOverwriteUntilExplicitLoad`,
`UnreadablePrimary_EntersRecoveryAndPreservesDamagedBytesUntilExplicitBackupRecovery`,
`ReentrantSubmitLoadRecoveryAndSlotChangesCannotRunInsideCommit`, the PersistenceTests hold and damage cases, the
`Pause_*`, `OpeningDirector` and `NotebookVotes` clock and revision checks, `StressHouseTests`'s StartSeason scan and
`NpcPairingTests.TheDirectorsRoutePlannerPairsThroughNpcPairing`.

### 7.3 What can be proven offline, in order

1. Prune `%LOCALAPPDATA%/Gamesim/offline-compile` folders older than 180 min, then `Tools/offline-compile.ps1
   -WithoutUma` with `GAMESIM_ACCEPTANCE=D:\GamesimNoUma`, under `cpu-lock2.sh`: proves Runtime, EditMode and
   PlayMode assemblies (the rewritten tests included) compile.
2. The dotnet subset under the lock: test 1 above, and the subset floor plus the new count.
3. The Mono harness with the offline DLLs: test 3, and a seam check that every private field and method the PlayMode
   tests reflect on (`npcSaveCadence`, `FlushUnsavedNpcTime`, `FlushForLeaving`, `npcSlotRefused`,
   `npcOperationCheckForDiagnostics`, `commitTimingForVerification`, `npcFrameFraction`,
   `saveCandidateForDiagnostics`, `npcSaveSuspended`, `OnApplicationQuit`, `OnApplicationFocus`,
   `OnApplicationPause`, ...) exists by name with the expected signature, since reflection fails only at run time;
   and that `NpcCommitTimings` carries no `SerializableAttribute` and the field carries `NonSerializedAttribute`.

These prove the rule (window, due, baseline, reset), the log's format, the seams and that everything compiles. The
engine's determinism across a save boundary is the autonomy verification's (10.3). Everything that involves the director, the scene and the store together is
PlayMode only.

## 8. Conflicts to keep clear of

- `claude/offers-come-to-you` changes `EpisodeDirector.NpcSocial.cs` at :84-93 (`TickNearby` and `NearbyOfferUp`),
  :281-283 (the ask's busy set in `PlanNpcApproaches`) and :473 (`AskHoldsFacing` in the presentation loop), and
  `EpisodeDirector.cs` at :101-108 (`AsksEnabled`, `askCardOpen` in `IsPanelOpen`), :454 (`TickAsks` replaces
  `TickNpcInvitations`, the line before `TickNpcSocialRuntime`), :709-735 (`ClosePanelsInternal`), :1383 (Render's
  `hud.Begin`) and :1530 (the ask card). It also adds lines at `PortVerification.cs:93` and
  `EpisodeDurableTransactionTests.cs:51`, and `SimulationTests.csproj:52`.
- `claude/room-scope` changes `EpisodeDirector.cs` at :378 (`TickRoomScope`) and `PortVerification.cs` at :56,
  :76, :150 and :360.
- This plan's edits, all clear of those hunks: in `NpcSocial.cs` :103-107, :210-235, :336-337, :363-396 (not :518-522
  any more: the cadence resets in `Install`); in `EpisodeDirector.cs` only the comment at :835-836, the timing wrap at
  :842 and the failure call at :848; the hooks, the flush, the refusal, the seams and the cadence field in
  `EpisodeDirector.Durable.cs` (no other branch touches it); `Season.cs` :148-165, :305-308, :398-400 and :418 (none
  touches it); `OpeningStage.cs` not at all;
  `PortVerification.cs` one line after :100 and one field near :376; `PortVerification.Performance.cs` (none
  touches it). Do not move `TickNpcSocialRuntime`'s call at :455 or edit `IsPanelOpen`.
- `SimulationTests.csproj`: add the new test's line away from :52 (offers' insertion), e.g. beside
  `EpisodeNpcSocialTests.cs` at :295.
- `EpisodeDurableTransactionTests.cs`: the rewrites are at :297-356 and :387-405, far from offers' :51.
- The ask card on the offers branch opens through `PauseNpcSocialForPanel` (`ComesToYou.cs:679`), so it writes on
  opening with no extra change; the backstop covers it anyway.
- Preview each merge with `git merge-tree` before landing (memory two-sessions-share-the-branch).

## 9. Save format, digests and goldens

- **No save format or schema change.** No field is added to `EpisodeState`, `NpcSocialState` or any saved type; the
  store, its envelope, its checksum, its backup and its migrations are untouched; no command kind is added. The
  cadence and the timing log are in-memory objects. So no schema number, no AddedSince list, no dispatch key and no
  upgrade chain (memory adding-a-saved-field-needs-a-schema-version).
- **Season digests and Sim sweeps:** unchanged. They drive the engine without the director or its NPC world (memory
  sim-sweeps-have-no-npc-world); the engine's only edits are a comment and an unread reason string, and
  `NpcSaveCadence` has no engine caller.
- **BalanceLab goldens:** unchanged; `BalanceLabNpcWorld` pairs and ticks through the engine and `NpcPairing`, not
  the director.
- **Old-save acceptance:** unchanged by this option (it reads, migrates and writes through the store, which option 1
  does not touch). Option 4(a) changes the store's pre-check and carries its own acceptance.
- **The floors** rise by the new tests only; the lead moves them.

## 10. Acceptance

1. **Offline:** 7.3, with exit codes and counts.
2. **The Unity set, when the lane frees:** EditMode in full against its floor; PlayMode first filtered to the
   touched fixtures (`EpisodeNpcScanCadenceTests`, `EpisodeNpcCompletionTransactionTests`,
   `EpisodeNpcRuntimeRegressionTests`, `EpisodeDurableTransactionTests`, `UnifiedSafetyDurability`,
   `DialogueIncomingOffer`, `NpcInvitationFlow`, `CastSelect`, `TalkSpots`, `LiveSave`), then the full no-UMA set
   against its floor; any non-Passed case listed by name (memory assume-hides-regressions-as-inconclusive).
3. **RS0 on a release player built by the lead from this branch.** r-live16 runs room scope's views/live mode
   (`PortVerification.Views.cs`, `--gamesim-views-live`), which exists only on `claude/room-scope`, so that player
   must be built from this branch merged with room scope (or from the line that carries both). Run r-profile16
   (300 s, 16 houseguests) and r-live16 (~1700 s), each with the timing log, and read:
   - **Writes:** from the log's persist rows. r-profile16's workload (8 rooms + notebook, settings, station every
     10 s, `VerificationProfilePlan.cs:18-31`; `SaveNow` every 60 s, `PortVerification.cs:240`) should make about 6
     panel writes, 4-5 explicit saves and at most 8 periodic writes: about 20 in 300 s, against 285 stalls today.
   - **Frames of 200 ms or more not matched to a player-caused write** (cause panel, save, command, focus, quit,
     slot): about one per 30 s of free time - at most ~8 in r-profile16, at most ~57 in r-live16 - and none of the
     double-save frames.
   - **p99 back near the ordinary ~9 ms frame.** The condition: the log's per-tick total (prepare + validate + engine
     + project) is small. At ~110 fps one tick frame a second is about 0.9% of frames, so if `projectMs` carries the
     54-78 ms remainder, p99 sits near that cost and option 5 is the next step, sized by this log.
   - The final save's revision matches memory's at quit (the quit write), no "SAVE NEEDS ATTENTION" or "House world
     stopped" in the player log, and the 52-65 ms hitch every 10 s still there (the workload's own).
   - **`--gamesim-verify-autonomy`** on the same player (the director-level determinism check that replaces test 2):
     `ObserveAutonomyStep` replays every observed revision step against a detached engine and requires exact
     equality, and its Settings save-and-reload requires the reload to equal memory. Status "Passed", no
     `RequireSeason` failure.

## 11. Commits

1. The timing log alone (section 6 with commit 1's row layout and its CSV-only `OnApplicationQuit`, test 3 and test
   11's "off" half): "The verification player records how long each part of a house commit takes and writes it
   beside its frames when it quits."
2. Option 1 with its tests, comments and this plan as `Assets/Plans/LIVE-SAVE-PLAN.md` (two-line meta, fresh guid):
   "The house's own seconds stay in memory and reach disk every thirty seconds of free time and before any decision,
   panel, pause, slot change or quit, and a failed save is judged against the last one."

New files and their hand-written two-line metas (memory new-scripts-need-hand-written-metas-before-a-commit):
`Assets/Gamesim/Simulation/NpcSaveCadence.cs`, `Assets/Gamesim/Runtime/Episode/NpcCommitTimings.cs`,
`Assets/Gamesim/Tests/EditMode/NpcSaveCadenceTests.cs`, `Assets/Gamesim/Tests/EditMode/NpcCommitTimingsTests.cs`,
`Assets/Gamesim/Tests/PlayMode/EpisodePlayModeTests.LiveSave.cs`, `Assets/Plans/LIVE-SAVE-PLAN.md`. LF endings
throughout (memory python-writes-crlf-on-windows).

## 12. Risks and what is not settled here

- **The panel's opening frame now carries a save** whenever free time has passed since the last one, where today it
  does so only when a whole second is due. That is one save per panel opened in free time, at a moment the player is
  pausing; 4(a) shortens it.
- **The audit is a survey, not a proof.** The tests in 7.1 are the ones found; the Unity run is the proof, and its
  failures should be read against the audit rule before anything is loosened.
- **`OnApplicationQuit` in the Editor at play-mode exit** is relied on for the Editor's last 30 s; test 6 calls it
  directly, so the Editor's own sending of it is the one behaviour taken from Unity's documentation rather than
  measured.
- **Focus and pause** do nothing in the Editor under a save-root override (every test run, batch or interactive,
  including the owner's editor over the MCP with `runInBackground` on); in a player and in the owner's own Editor
  play they act only on `false`/`true` and are idempotent. Test 17 pins the gate.
- **For the owner, before commit 2 lands** (the lead asks once; a "go" is recorded as the owner's call with its
  date). The answer of 2026-11-01 named a crash, a kill and a power cut. This plan adds three things that answer
  did not cover, each bounded by the same 30 s of house activity and none touching a choice: (1) a save held by
  another program past the store's 250 ms wait enters the recovery screen, and its reload drops the owed seconds
  (3.1; the alternative, keeping them, relaxes a pinned rule and is the owner's to choose); (2) a save refused on
  what the house holds, which needs a bug, blocks decisions until a reload (4); (3) while saving fails, a menu can
  show house activity that is not on disk, under the failure notice (2.4). If the owner declines (1), the change
  is the catch's `IsFilesystemError(error) && !readable` case for a dirty session, with
  `ConversationInformation_FailedSavePublishesNeitherOpinionsNorMeetingReads` kept for clean sessions.

## 13. Design review

Two reviews (durability, contracts), 2026-11-01. Each finding was checked against the code at 37416049
(`EpisodeSaveStore.cs` read from origin/main, since 4(a) is editing it in the worktree). Eleven findings: eleven
adopted, none rejected. Three are adopted with a qualification, stated in their rows.

| Id | Verdict | Why, with the evidence checked |
|---|---|---|
| LS-DUR-1 (major) | **Adopted** | Real. `TryPersistCandidate`'s catch sets `npcSaveSuspended` on every plain failure (`Durable.cs:90`). The old guard skipped a suspended session at every point. The openers check only `blockedRecovery` (`Conversation.cs:241`, `EpisodeDirector.cs:655`; `OpenSettings` at :766 checks nothing), so after one failed write the menus showed unsaved state, quit skipped, `StartSeason` went ahead without its refusal, and the refusal path stopped on owed seconds. `SaveNow` already retries a suspended session (`Season.cs:154`). Changes: automatic and leaving callers (2.3), one retry per leave point with `MarkSaveFeedback` on success (`BeginPersistenceNotice`'s `systemNotice` is cleared only there, `ConversationExchange.cs:140-153`); the slot refusal once per failure (`npcSlotRefused`, 2.4); the refusal writes what is installed first (2.1); test 13 and the second half of test 10. One addition beyond the finding: the two failure callers (`Season.cs:164`, `EpisodeDirector.cs:848`) switch to `SuspendNpcWorldWithoutSaving`, so a retry never runs inside the action that just failed and can never erase an uncommitted decision's notice. That is no change today, as shown in 2.3. While writes keep failing, a menu can still show unsaved activity: refusing panels would also lock out Settings, so this is stated as a limit and put to the owner (12). |
| LS-DUR-2 (minor) | **Adopted** (naming, test, owner question; the relaxation is deferred) | Real. A held primary makes `Save` throw "could not be opened to check it" (`EpisodeSaveStore.cs:51-57`). The catch's `TryLoad` fails on the hold (`TryRead` sets `unopened`, :185-189), so `hasPrimary && !readable` (`Durable.cs:91`) locks. `ConversationInformation.cs:66-77` pins it. With seconds owed, every way out drops them. The lock is kept. The loss is named in the owner summary, the decision record, 3.1, 4, 5, `NpcSaveCadence.Seconds` and 12, and test 14 pins it. Keeping the seconds would relax a pinned rule, so it stays the owner's call. |
| LS-DUR-3 (minor) | **Adopted** | Real. The store's read-back (`Install` :125 -> `TryRead`) runs the 8 MiB bound (:153), the frozen schema-29 reader (`EpisodeSaveMigrations.cs:26-30`), `CheckDtoShape` and hydration (:166-169); `EpisodeSaveValidation.Validate` (`EpisodeSaveValidation.cs:10-34`) runs none of them. `Submit` builds from memory (`new EpisodeEngine(engine.Snapshot)`, `EpisodeDirector.cs:837`). The claim is corrected in 2.1. A content refusal of the session itself says "reload" (3.1). The cost (decisions blocked until a reload) is stated in 4, and test 15 pins it. The pre-install check is not widened. |
| LS-DUR-4 (minor) | **Adopted** | Real for interactive Editor runs: the owner's editor drives PlayMode over the MCP with `runInBackground` on. The tests count writes through the diagnostic writer (`Durable.cs:62-66`). Every fixture sets `SaveRootOverride` (`EpisodePlayModeTests.cs:41`, `EpisodeDurableTransactionTests.cs:43`). Focus and pause return early when `Application.isEditor && SaveRootOverride != null`, through `FlushForLeaving`; test 6 calls that directly and test 17 pins the gate. `OnApplicationQuit` is kept in Editor and player. |
| F1 (major) | **Adopted** | Real. Under the new `SaveNow` the tick installs and plans its routes (`NpcSocial.cs:384-388`; `npcApproachDiagnosticsSuppressed` is false after bind, `EpisodeNpcScanCadenceTests.cs:203`) before the hook runs. The hook's :68 `AssertNoNpcScanApproaches()` then throws an `AssertionException`, which `SaveJson.IsExpected` (`EpisodeSaveStore.cs:364-366`) does not catch. :76 still compares with clock 2, and :80 and :83 assert no routes. The row now gives the whole body (7.1). |
| F2 (major) | **Adopted** | Real. `Start` writes nothing on a fresh root (`EpisodeDirector.cs:151-155`; SetUp `EpisodePlayModeTests.cs:36-51`). The scan test reads both files at :61-62, the helper reads the disk at :213, and the completion test reads both at :36-37 after a wait of up to 65 s (:19-22). `BindNpcScanWorld` and the completion test now `SaveNow` twice, and the audit rule gains the third case. The same applies to `UnifiedSafety...Completion`'s :296, which is also rewritten. |
| F3 (minor) | **Adopted** | Real. Today's guard (`Season.cs:152-156`) exists for exactly this case. `TryAutosave`'s success appends "Saved locally." and calls `MarkSaveFeedback` (:162-163), and success lifts the suspension (`Durable.cs:75`). `SaveNow` is the finding's form (2.4). The refusal is otherwise unreachable from a test (its bounds need a save over 8 MiB), so test 16 uses a new Editor-only `npcOperationCheckForDiagnostics` seam in the `saveCandidateForDiagnostics` idiom. |
| F4 (minor) | **Adopted** | Real. `ResetNpcSocialForLoad` runs from `OpeningStage.TryPlace` (:236) and `RestoreHome` (:252) without a load, after `StartOpening`'s `PauseNpcSocialForPanel` (`Opening.cs:98`), and clears `npcSaveSuspended` (`NpcSocial.cs:521`). The cadence now resets in `Install` right after `engine = new EpisodeEngine(state)` (`Season.cs:418`) (2.3, 1.1, 3.3, 8). |
| F5 (minor) | **Adopted in its minimum form**; its preferred rollback is **rejected** | The facts are the same as LS-DUR-3's, and the claim is corrected. The failure says "reload", and test 15 covers a decision after it. The automatic rollback to the last save is rejected for four reasons. It is an automatic reload: `Install` (`Season.cs:410-432`) disposes the world, bumps `loadGeneration`, closes panels and puts every housemate back at the season's initial positions. It would run inside a panel opening and close the panel being opened. If the bug is deterministic, the house can live its way back to the refused state and roll back every window. And it discards house activity the player has seen without asking, which the owner was never asked about. The explicit reload costs the same seconds, by the player's own action. |
| F6 (major) | **Adopted**, with one factual correction | Real for tests 4, 7, 9 and 2. The clock stops while a pair walks (`NpcSocial.cs:227-233`), so the clock's lead did not measure age. Test 7 killed short of 30 s. Test 2's engine is unchanged. Test 9's fixture can install nothing (`NpcSocial.cs:375`; start needs arrival; cancel needs 20 s, :264). Tests 4, 7 and 9 are rewritten, and test 2 is replaced by `--gamesim-verify-autonomy` (`PortVerification.Autonomy.cs:146-183`) in 10.3. Correction: 300 IEEE-double additions of 0.1 give 30.000000000000156, not 29.99999999999984, and 299 give 29.900000000000155, so that construction would have passed. Test 1 uses exact binary values anyway, because rounding, not the rule, would decide the boundary. |
| F7 (minor) | **Adopted** | Real. The file's report idiom is `[Serializable]` (`PortVerification.cs:362`), and a serializable class in a MonoBehaviour field is deserialized non-null by Unity. `Time.*` cannot run under the Mono harness, and commit 1's rows were undefined. The field is `[NonSerialized] internal`, the class is not `[Serializable]` and is pure (frame, seconds and revision passed in), the off path is guarded, and commit 1's rows and its CSV-only `OnApplicationQuit` are defined (6, 11). |

## 14. As built (2026-11-01)

Built on `claude/live-save-batching` after the 4(a) commit, in three commits: this plan; the timing log alone
(section 6 with commit 1's rows); option 1 with its tests. Proved offline only (7.3); the PlayMode cases wait for the
Unity lane. Where the code differed from the plan, the code won, as follows.

1. **The check before installing is a helper.** `RefusalOfNpcOperation(candidate)` in `EpisodeDirector.Durable.cs`
   runs `EpisodeSaveValidation.Validate` and then the Editor-only `npcOperationCheckForDiagnostics`, and returns the
   refusal or null; `CommitNpcOperation` calls it and hands a refusal to `RefuseUnsaveableNpcOperation`. Same
   behaviour as 2.1's sketch, and it keeps `Gamesim.Persistence` out of `EpisodeDirector.NpcSocial.cs`.
2. **The engine is built before the operation is counted.** `CommitNpcOperation` constructs
   `new EpisodeEngine(result.candidate)` first, then `Begin`/`Installed`, then installs it, so the cadence can never
   count an operation that was not installed.
3. **One timed write.** `TryPersistTimed(prepared, cause, ...)` wraps `TryPersistCandidate` with the timing log's row;
   the flush, the refusal's write, `TryAutosave` (cause `save`) and `Submit` (cause `command`) all use it. Commit 1
   had the same timing inline in `TryAutosave` and `Submit`.
4. **`Elapsed` is fed wherever `npcFreeSeconds` is.** Besides the 100 ms sample (2.2), the explicit remainder
   `FlushNpcWholeTicks` counts at a Save or a panel (under 0.1 s) feeds `npcSaveCadence.Elapsed` too.
5. **`NpcSaveCadence.Begin` replaces while clean.** Two calls before the first `Installed` keep the second; after it
   they are ignored (test 1 pins both). `Saved(state)` records what the store holds, which `KnownOnDisk` returns only
   if a window opened without a `Begin` (the director always calls one).
6. **Test 13 checks the notice, not `StatusMessage`.** `StatusMessage` is the director's `message`, which keeps a
   failure's text after a retry that lands, as `SaveNow`'s retry does today (it appends "Saved locally."). What the HUD
   shows is `persistenceNotice` while suspended and the phase's guidance once not (`HudStatusMessage`,
   `EpisodeDirector.ConversationExchange.cs:181-199`), so the case asserts that `persistenceNotice` is null and the
   suspension lifted.
7. **Test 4 reads the `projected` field each frame**, not `director.Snapshot`, which clones the whole season every
   call. Its clock-stopped variant is its own case, `LiveSave_TheThirtySecondsAreFreeTimeWhileTheHousesClockIsStopped`.
8. **Test 6's backstop case** sets `npcWorldFailed` by reflection for one `TickNpcSocialRuntime(0f)` and clears it:
   `StopNpcWorld` would log a warning and leave the world failed for the cases after it.
9. **"Later ticks never repeat the completion"** stays as the old tests had it: after the retry, a flush with no
   whole second due leaves memory equal to the written state. A later real tick would need every remaining pair to
   arrive again first.
10. **The audit found two more checks in `DialogueIncomingOfferRoute`.** The stale paid choice after leaving the
    conversation (`justLeft`) and in the offer view (`offerView`) used `AssertDialogueUiUnchanged`, whose
    `AssertDialogueUiDisk` needs the disk to equal memory after frames in which the house runs. They now check memory
    and the file's bytes only (`AssertDialogueIncomingUntouched`), which is what "a stale click commits nothing"
    means. The other hits of a sequential audit (each test's body with its helpers inlined, flagging a disk read after
    an unheld yield with no write point between) were read one by one: non-free-time phases, where the house installs
    nothing and every phase change is a written decision; a house held, or paused under a conversation or a panel;
    or a profile store, not the episode's.
11. **Test 12 also calls `FlushForLeaving("focus")`** on the frozen director and checks the seconds stay owed.

Still open: the owner's answer to section 12's three cases (the lead asks before this lands); the Unity set and the
RS0 runs of 10.2 and 10.3.
