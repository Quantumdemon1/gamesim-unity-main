# All-Star strategic campaigns: independent design review

21 October 2026. Design review only; no implementation, current-project inspection, helper, compilation, native run, or asset operation. The original-pair native freeze was respected. The only source read was the explicitly named CLOSED snapshot below. The only output of this review is this Task document.

Reviewed brief: `ALL_STAR_STRATEGIC_CAMPAIGNS_CONTENT_BRIEF_20261021.md`, SHA-256 `29AC8691125291BE832574AC573BBEDE481F52327A9E9F938ABA98D0239CE8E9`.

Source root: `D:/GamesimAcceptance/Logs/validation/r2-dialogue-pilot-uma-graph-lifecycle-single-native01-20261021T141850180Z-fe525a47d946440cbbb366ecdfb228f3/snapshot/Assets/Gamesim`.

## Verdict

The twelve concepts are distinct, game-only, and worth retaining. The brief correctly separates uncertain claims, actual commands, consent, settlement, and recap. It explicitly acknowledges that the generic dialogue and exact commitment association are not qualified yet. Approve it as a content direction, **not** as immediately executable catalog work or a native/save acceptance result.

The following implementation gates must be made explicit before Cody is enabled. They are source-backed constraints in the CLOSED snapshot, not claims that unimplemented content has already introduced regressions.

## Gates before the first slice

### 1. Use a short interaction, not a season-long active Game-lane cycle

The current generic owner resolves a dialogue episode to exactly the story cycle with the same ID (`DialogueRules.cs:21-30`). It accepts only the pilot template, zero cycle vars, no scheduled anchor, and at most three path entries (`DialogueRules.cs:171-189`); an active episode cannot survive its week (`DialogueRules.cs:258-264`). Catalog registration alone cannot admit a new four-beat, multi-occasion campaign.

Separately, a running Game-lane cycle excludes every other new Game-lane cycle (`Story/EpisodeEngine.Story.cs:414-427`). The ordinary pool shares a finite ask budget (`:140-142`, `:209-214`). A campaign left active awaiting the finale would contradict the brief's promise not to starve ordinary stories. Its pulse can also expire after four inactive weeks (`:276-285`).

Minimal route: Cody's ask and explicit confirmation are a short bounded interaction. Close that interaction on consent, refusal, or loss of eligibility. Preserve its history; settlement and finale are read-only callbacks over that history and the exact canonical record, not a live lane reservation. Do not create a new thread manager or borrow ordinary thread slots. Leave the precise episode/history linkage and version design for the generic qualification work; this review does not authorize a new serialized relationship.

### 2. Pin the exact vote duty and accepted-record association

The brief correctly refuses to reuse `origin` or select the latest promise. That remains a hard prerequisite: the current evidence DTO contains power, memory, and statement references, not a generic canonical commitment reference (`DialogueState.cs:70-76`). The commitment origin vocabulary is closed (`UnifiedCommitments.cs:89-108`).

For Cody, “your vote” must unambiguously mean **promise Cody that the player will vote to evict the other final nominee**. The existing PromiseVote command takes an eviction target and refuses promising somebody their own eviction (`EpisodeEngine.cs:1251-1256`). Admission preserves the player/voter/Campaign/final-block prerequisites and current-week expiry (`UnifiedVoteAdmission.cs:39-70`). Fulfillment compares the maker's actual ballot with the promised target (`UnifiedVoteObligations.cs:73-77`); survival is not fulfillment.

The consent card must name both beneficiary and eviction target, capture the exact final nomination frame, expiry, and ordinary consequence. The accepted command must create one canonical row and one recoverable association atomically, through its owning Store path. Qualify duplicate/stale submission, changed frame, exhausted capacity, failed-save rollback, and reload before using the row in a callback. Do not add a second AP charge through a nested public promise command; preserve the generic single-debit contract. Do not use the native null-target StoryPromise as a shortcut for this targeted promise.

### 3. A real finale receipt is not automatically safe for a juror to speak about

This is the most important privacy trap. `FinaleQuestions.Receipts` selects the player's ballots directly (`FinaleQuestions.cs:290-295`); the receipt renderer explicitly documents that the engine question still reads the box, while the ballot line is redacted (`:422-429`). `KnownBallots.PromiseOutcomeKnown` is a **player-view** predicate: it returns true for the player's own promise (`KnownBallots.cs:485-496`). It must not be treated as proof Cody knows that ballot.

Use a recipient-scoped knowledge/proven-ballot rule for spoken Cody/house/juror text, including kept/breached labels and ending captions. `JuryHouseRead.CouldKnowYourBallot` queries `KnownBallots.ProvenFor` for the specific juror (`JuryHouseRead.cs:345-352`). The player may receive their own private exact receipt. A selected receipt alone is not permission to turn it into an accusation, thanks, spoken callback, caption, public recap, or public log. Do not create disclosure merely to make the campaign conclude neatly.

There is also a saved-text contract: `EpisodeFinaleValidation.cs:78` accepts only `FinaleQuestions.Questions(category)`, and canonical Safety validation checks `QuestionsFor` as well (`EpisodeValidation.UnifiedSafetyReferences.cs:116-118`). Personalized saved question wording therefore needs an explicit compatible content/rules path. Until that is qualified, use the private noninteractive recap and preserve existing saved question wording/count/scoring/RNG behavior. No rewriting old `juryExchanges`.

### 4. Define closure reason separately from settlement and recap facts

The existing general cycle departure handler ends a required-cast departure as `left-house` (`Story/EpisodeEngine.Story.cs:931-948`), and `EndCycle` resolves open cards and clears scheduling (`:899-911`). Those defaults do not automatically produce the brief's more specific `kept-departed`, `player-departed`, or `occasion-closed` observations.

Specify the order: stop interactive authority immediately when eligibility ends; preserve accepted commitments; let their existing owner settle or expire them; derive the later read-only observation from the exact retained receipt and public result. Do not reopen a completed interaction, erase its decline, or overwrite it with a fabricated final choice. Test eviction/departure in the same transition as vote settlement, including Cody evicted despite the player's kept word and Cody surviving despite its breach. A short interaction's terminal decision and a later outcome are different facts, not competing versions of one mutable ending.

### 5. Measure ordinary-story displacement and qualify each later action adapter

The proposed caps (two distinct campaigns per season, one active, one opening per week) are useful ceilings, not evidence of healthy reach or fairness. Existing Game-lane occupancy, shared asks, headliner/pair cooldowns, delayed anchors, and hard shelf limits still apply. Measure ordinary-story offers alongside each campaign's eligible/offered/accepted/declined/completed/expired counts. Refusals consume the once-per-template opportunity; the same accepted chapter does not become a new entry benefit. Do not force eligibility, bypass budgets with `urgent`, or perturb the main RNG to improve reach statistics.

The later concepts rely on materially different owners, not interchangeable dialogue effects: Danielle needs a genuine observable fact and legal evidence request; Janelle needs paid study, actual competition participation/result, and actual power; Xavier/Vanessa need the pact command's membership, one-counter, consent, and settlement authority; Dan needs an NPC-owned lawful action with possible refusal; Chelsie/Jun need actual audience-aware disclosure. Before each vertical slice, name the exact existing command, receipt, visibility reader, stale/decline path, and ordinary cost. If the generic layer cannot express that owner safely, keep the branch unavailable rather than narrating success. Do not retune NPC coefficients or B9 as part of story authoring.

## Real-person scope and saved-state review

No concept requires invented private biography, romance, misconduct, or off-screen factual claims about the real alumni. Keep every participating role and player sensitivity Game-only; the current policy separately protects identifiable alumni and alumni-based player cards (`Story/StoryPeople.cs:10-27`, `:43-66`). Use stable template identity for eligibility and the actual saved display identity in rendered text. Personalized means a different strategic dilemma, not fictional private allegations. Retain the simulated-season framing when final text is authored.

The brief's four-role/four-flag bounds fit the general cycle limits, but general DTO fit is not dialogue admission. Do not weaken pilot validation or infer activation from loaded template IDs. Preserve old schema/content/rules outputs and explicit disabled-dialogue storage. Include malformed cross-links, wrong audience, foreign commitment IDs, capacity edges, and saved old content in the later validator matrix. No schema number or extra save collection is allocated by this review.

## Minimal executable sequence after preceding gates close

1. Qualify the generic dialogue/historical-reader/save/consent/role-AP work first. Resolve exact receipt association, version dispatch, and receiver-scoped visibility there.
2. Implement only Cody: legal opener; two meaningful decisions with neutral exits; explicit other-nominee promise; one ordinary debit; exact canonical row association; immediate interaction closure. Add neither a new mini-game nor a season-long active lane.
3. Add a pure settlement reader and private recap from the retained accepted command and real outcome. Add spoken/personalized finale content only after its knowledge and saved-wording gates pass; otherwise retain the truthful private recap.
4. Exercise refusal, consent, stale/duplicate, rollback, reload, wrong-frame, capacity, departure, private/public visibility, and old-version fixtures. Then run the native route and inspect consent/recap at ordinary and Largest text with input focus and captions. Automated assertions do not replace that route or human comprehension.
5. Add Danielle, Janelle, and Xavier separately only after Cody's gates pass; then the other eight. Measure regular/All-Star/mixed 8/12/16-cast reach and ordinary-story displacement, followed by the already required native/build/performance/human acceptance work. No new permission or native freeze release is implied.

Disposition: **content direction accepted with the five implementation gates above; not yet an implementation-ready patch.**
