# Role-aware dialogue and limited conversation time

Design extension, 2026-10-12. The owner chose **both relationship consequences and limited action-point changes**. This document is an implementation contract proposal; its new amounts are unmeasured starting defaults. It changes no gameplay, save, test, Dev documentation, or frozen source image.

## Integration boundary

The presentation lane can proceed independently: one category surface at a time, nested choices, actual speaker turns, existing phase-valid actions, existing costs, and existing committed receipts. Those changes must not silently add free actions, reveal hidden state, or replace an existing check with a different one.

The new role entitlements, consequential multi-turn branches, intention records, and conversation-time settlement join GameSim Dev's existing historical-reader/B9 lane. The current handoff requires the immutable schema-28 reader and storage companions to close before fresh-only rule/schema changes. Its retained Source19 baseline and failed workflow receipts remain unchanged; GameSim Dev's active native diagnostics and source images remain untouched.

After that gate closes, this already-authorized extension becomes a separately versioned candidate, followed by the same B9 cohorts and combined Unity validation. This is a dependency, not an additional owner approval requirement. Promotion follows the existing source-freeze/promotion boundary; no edits are made to a candidate while another chat is validating it.

Current dependency source: `C:/Users/kelli/Documents/Codex/2026-09-09/github-plugin-github-openai-curated-remote-2/work/port-completion-owner-decisions-20261012/REMAINING_WORK.md`, sections 1, 2, 5, and 7. Recheck its current state at implementation time.

The isolated presentation candidate now has six nested topic categories, published Lobby/AskVote speaker adapters, narrower person selection, and an invitation adapter for actual pending NPC offers. These are presentation implementations, with their validation recorded separately in the handoff. The role privileges, new consequential multi-turn trees, conditional-veto claim/promise contracts, and new +1/0/-1 time settlement below remain **unimplemented design proposals**. Do not infer gameplay delivery from a presentation test pass.

## Existing mechanics to reuse

| Existing mechanism | Reuse in dialogue |
|---|---|
| Window seats: two after HoH, one after nominations, two after veto, cast-scaled after eviction | Keep the normal conversation budget and its expiry semantics. Show the actual current window's time. |
| `StrategyRules.Pleas`, `CanLobby`, and four lobby approaches | Nomination, veto, replacement, and campaign persuasion paths; use existing valid targets and actual committed responses. |
| `AskVote` and `ReadPerson` | Existing free, limited probes. Preserve honesty/deflection, earned knowledge, and once-per-person-per-week restrictions. |
| `HoHPitches` | A courting NPC's real nomination pitch to the player HoH; an exact-card assessment followed by a final response. |
| `ReplyCards`, pending deals/counters, and pact plans | Incoming situations and offers are the NPC's move. Hearing/declining/answering them remains free under current rules. |
| `NomineeIntel`, known readings, player memories, and known pacts | Contextual questions with public target identities and earned answers. No roster or ballot inference from the preview. |
| `ArcTemplate`, `BeatTemplate`, `OptionTemplate`, `HouseEventState`, and storyline path records | Real branches, requirements, picked people, success/backfire effects, next beats, cooldowns, and reload continuity. |
| `ConversationExchange` and authored HUD | Speaker presentation after an accepted transaction; ordinary Continue and Skip have no additional gameplay effect. |
| Existing relationship/commitment writers and ledger receipts | Attribute each actual consequence once; avoid a parallel relationship or promise authority. |

Relevant source in `D:/GamesimPhase20261012/Assets/Gamesim/Simulation/`: `EpisodeEngine.Week.cs:24-123`, `StrategyRules.cs:94-240`, `EpisodeEngine.VoteRead.cs:63-148`, `HoHPitches.cs:13-48`, `Story/StoryTemplates.cs:136-239`, and `Story/EpisodeEngine.Story.cs:698-809`.

Current social-point banking is positive-only: `Story/EpisodeEngine.StoryEffects.cs:270-280` ignores non-positive phase bonuses, and `:418-423` converts ten-point crossings into Good Standing. It cannot be relabeled as a symmetric +1/-1 conversation system. Existing modifiers and historical templates retain their old meanings.

## Named role privileges: initial unmeasured defaults

Keep incoming offers, existing reads, and ordinary greeting/navigation free. Add **reserved consequential episodes**, not unrestricted points that can be spent on any action.

| Privilege | Initial allowance | Eligible use | Expiry |
|---|---|---|---|
| **HoH Consultation** | One episode per HoH per week | Ask an active houseguest for their case, weigh a publicly valid nomination, or discuss a legitimate safety proposal before the player's first nominations. | Original nominations committed. It is not regranted for replacement nominations. |
| **Veto Consultation** | One episode per veto holder per week | Discuss a nominee's request, weigh using/not using the actual veto, or clarify a legal reciprocal price before the holder's decision. | Veto decision committed, or the veto becomes unusable. |
| **Nominee Appeal** | One episode per nominated player per week | Make a phase-valid plea to the actual veto holder, or campaign to a currently eligible voter. | Player leaves the block, eviction closes, or the relevant decision is no longer reachable. |

A free incoming pitch or its existing assessment does not consume a role privilege. A privilege covers one newly initiated consequential episode that otherwise would have a normal cost; it never pays for unrelated practice, study, rumor-spreading, or another independent deal.

Overlapping roles grant **at most two complimentary consequential episodes per player per week**. Each role grants at most once, even if the player is removed from and later returned to the block, saves/reloads, visits another room, or sees a replacement nominee. A role's first accepted qualifying use records its consumed grant. Merely browsing does not consume it. No carry to another week and no banking expired privileges.

Grant identity is stable over the role's week, owner, and role kind, with the accepted public role-setting transaction retained as provenance. Do not key it only to the latest nomination list, menu instance, or newly minted request ID.

These initial allowances are small deliberately, but **their balance has not been measured**. They alter policy opportunities and must be evaluated through B9 rather than presented as a finished fix.

## Categories and honest intentions

The presentation candidate's top-level categories are **Get to know them**, **This week's game**, **Learn and share**, **Deals and promises**, **Pressure and rumours**, and **Do something together**. A category opens its own next choices. Only the current category or current speaker/choice node is displayed; a large options list never stays open behind the wheel. An actual incoming offer or story situation has its own speaker/choice view, rather than a seventh catch-all topic.

| Current category | Presentation scope |
|---|---|
| Get to know them | Small talk, personal chat, relationship building, sharing a secret, and plain time together. |
| This week's game | Existing nomination/veto/vote pleas and their nested approaches, plus existing strategic/game discussion. |
| Learn and share | Existing free reads and vote questions, earned intel, and sharing selected player-held information. |
| Deals and promises | Actual incoming offers/counters, valid existing proposals, promises, pacts, and negotiation. |
| Pressure and rumours | Existing vent, lie, whisper, public call-out, and quiet scheme choices; named subjects stay nested. |
| Do something together | Existing room- and phase-valid activities. |

Phase/role adapters enumerate available actions from existing authority readers. The final command revalidates phase, participant eligibility, budget, exact card/beat, and any selected subject. A preview can use public results and earned information, but cannot use hidden relationship scores or private agendas to expose why a button is available.

| Topic | Public preview | Consequential content |
|---|---|---|
| Vote | Current nominees and eligible voters | The speaker's actual `AskVote` claim or deflection. A leaning is not a guarantee and a claim may be a lie. |
| Possible nominations | Public HoH and legal nomination/replacement candidates | The player's argument and an actual lobby/pitch result. Do not display hidden nomination rankings as the NPC's spoken plan. |
| Who is working with whom | Known pacts, public observations, and player-held information | A disclosed claim, witnessed leak, or evidence-backed question. No complete hidden alliance roster. |
| Actual veto | Public holder, nominees, and current legal veto choices | A request, negotiation, stated inclination, or explicit binding commitment, each with its proper authority. |
| If you win veto | A conditional question before the result is known | A **new stated-intent claim**, distinct from a promise; never a reconstructed future decision. |

For **“If you win the veto, will you use it on me?”**, the NPC may say yes, decline, stay noncommittal, or give a condition. Persist the statement as a claim with speaker, recipient audience, current occasion, condition, target, and expiry. It reports what was said then. It is not proof of the NPC's hidden plan, and it may cease to apply when the situation changes.

An explicit follow-up **“Can we make that a promise?”** creates a binding conditional obligation only through the canonical commitment system. Before adding that contract, define its canonical kind, legal single-veto capacity, conflicting commitments, verification readers, and settlement. Never reuse an unconditional `VetoUse` row by pretending the speaker already holds the veto. If the speaker does not win, the target is no longer eligible, or the veto is legally locked, the condition is unmet and the obligation lapses neutrally. If the condition is met, the actual veto decision keeps or breaks it. Disclosure and attribution follow what each participant really knows.

## A genuine multi-turn branch

Example: a nominee talks to a strategic voter during campaigning.

1. **Player chooses an opening:** “Where is your head at?” invokes the existing free vote question once; “Can I make a case for staying?” opens the phase-valid persuasion episode. Its cost or remaining Nominee Appeal is visible before commitment.
2. **NPC gives a real response:** their committed vote claim, a deflection, or an authored statement of a concern. A newly authored concern is selected inside the accepted gameplay command, persisted, and disclosed to the player; it is not guessed by the HUD from private scores.
3. **Player chooses how to answer:** address the concern using a specific known fact; offer a real reciprocal commitment; appeal to the established relationship; respectfully decline the request; or apply pressure. Display only valid facts and commitments. Include a neutral exit.
4. **NPC reacts to that answer:** a supported argument can earn warmth or move the proposal; an unsupported assertion can be challenged; pressure can backfire; a respectful refusal can leave the outcome unchanged. The exact reaction and its branch are persisted once.
5. **Optional branch continuation:** the NPC may ask for clarification or name a price. The player responds to that specific node, using actual offered terms. A deal, claim, or earned fact is recorded by its canonical writer. The conversation closes with a compact receipt and the decision still pending where appropriate.

This is a sequence of **NPC lines and player decisions**, not selecting an approach and then automatically showing a paragraph of success. Initial content limits: no more than **three consequential choices** in one episode, no more than **four offered choices** at a node, and no automatically repeating success branch. These limits are initial authoring defaults, not historical-template edits.

Normal text Continue/Skip remains presentation. A consequential branch selection is a distinct accepted command or an existing `ProgressStoryline` choice. Skipping text must not secretly select an unanswered branch; it reaches the current choices. Leaving an unfinished episode pauses it or invokes its explicit, neutral lapse at a real decision boundary. Reload resumes the persisted node and previously stated terms.

Charge the episode's ordinary one-action cost at its first accepted consequential node, or consume its qualifying role grant. Further nodes within that bounded episode do not charge another action. Independent actions cannot be smuggled into continuations to bypass their ordinary cost. Source-backed existing command adapters retain their present cost until this versioned episode authority is implemented.

## Choice, evidence, and NPC response

The resolver uses the chosen approach, that NPC's current situation and personality, existing directional relationship, valid evidence, actual obligations, and already-established story context. Private inputs stay inside accepted gameplay resolution. Previews use a player-known estimate, as `KnownOdds` already does; they never show a hidden exact success meter or unrevealed psychological dossier.

Evidence is a stable reference to a player-known fact, claim, witnessed event, or earned memory, with its subject, source, time, and audience. Revalidate it at selection. Distinguish a public fact from hearsay and from the NPC's own earlier statement. Hearsay cannot prove a ballot or alliance, and an old intention cannot prove today's choice.

An NPC may challenge weak evidence, contradict a disputed claim, accept an honest uncertainty, or ask for a concrete condition. This choice leads to an authored continuation suited to the resolved response. The choice does not invent supporting evidence, infer a private ballot, or silently turn a friendly reply into a binding promise.

Persist the actual selected result and supporting references before presenting it. Reopening, advancing a text line, or replaying an animation must not draw another check. New checks use the existing seeded gameplay/story stream under explicit versioned rules, never render-time randomness.

## Relationship consequences and limited time settlement

Treat three currencies separately: **NPC attitude toward the player**, **binding promises/deals**, and **conversation time**. “Trust” in this design means the first relationship consequence; it does not overwrite the separate legacy trust mark used by threat assessment.

For new non-commitment dialogue branches, use these **unmeasured nominal starting defaults**:

| Resolved branch | NPC-to-player relationship proposal | Conversation-time proposal |
|---|---|---|
| Earned constructive result | +3, or zero if the actual branch only informs | +1 only for a designated earned-time ending; otherwise zero |
| Neutral, inconclusive, or respectful exit | 0 | 0 |
| Clear conversational backfire | -3 | -1 only for a designated lost-time ending; otherwise zero |

These are authoring defaults, not retroactive replacements for existing action, trait, reciprocal, promise-breach, or deal-settlement numbers. Existing adapters report their actual unchanged results. Canonical relationship rules apply their existing clamping and ledger attribution; no extra reciprocal warmth is fabricated by the presenter. A real broken obligation uses its own commitment settlement, not this generic -3 leaf.

For new conversation-time outcomes:

- Each completed episode settles **exactly once**, with a final time outcome of **+1, 0, or -1**. Individual text steps do not award time.
- At most **one +1 award and one -1 forfeiture per player per week** from this new subsystem. Gaining and losing are capped separately, so alternating branches cannot repeatedly refund paid actions. Role grants have their own two-episode ceiling and are not counted as earned points.
- A +1 is usable only in the current open window and expires when that window closes. No award during a competition, ceremony, unresolved transition, or after the player's exit. It does not multiply a competition bonus or become a weekly carry balance.
- A -1 removes one currently available conversation opportunity only when, **after the ordinary episode cost**, at least two opportunities remain. Otherwise its time result becomes zero, while its real relationship/obligation consequence still applies. No negative spent counters, future debt, reversal of accepted actions, or removal of the last playable opportunity.
- An episode that exits neutrally, expires, or is abandoned before a designated terminal result gives no time award. Its already-committed normal cost is not refunded.
- Existing story points and purchases keep their own unchanged semantics. Do not route this +1 through `phaseEventSocialBonus`, or let a new -1 debit that positive-only bank.

The outcome label should say the actual consequence, such as “One more conversation in this window” or “The argument cost you an opportunity.” Avoid generic floating points disconnected from what either person said. Reaching the same terminal node again is replayed presentation, with no second settlement.

## Persistence, compatibility, and repeat prevention

Select the new dialogue/economy rule only for a freshly created, unplayed season after the historical reader gate. Persist its exact rule/content version; a missing field means the old rules, never “latest.” Do not infer the new mode from week, cast size, opening phase, or role. Add a schema migration only after the frozen schema-28 reader contract remains independently covered.

Existing `economyRulesVersion == 1` behavior must retain exactly its present meaning. Prefer a separately named dialogue rule version so the old equality checks and positive point bank continue working unchanged. New save fields are strict, bounded, validated records; their final field names and limits belong to the reviewed schema increment, not to an accepting facade around current DTOs.

Required persisted contracts:

- **Episode:** stable occasion ID, template/content version, player/NPC, week/window/phase, originating pending offer or public occasion, current node, selected path, admitted evidence references, recorded responses, entry cost/grant receipt, and final settlement state.
- **Role grant:** stable player/week/role identity, role-setting provenance, permitted decision window, consumed episode, and expiry. Repeated nomination or repeated rendering cannot mint another grant.
- **Time receipt:** episode identity, actual +1/0/-1, rule version, window, reason, and the week's consumed reward/loss cap. Record zero outcomes when relevant so a rejected reward is not retried later.
- **Conditional statement/obligation:** exact speaker, audience, condition, subject, statement time, expiry, canonical reference where binding, and actual later settlement. Do not derive it by matching arbitrary prose.

At most one active consequential dialogue episode owns the current interaction at a time. Concurrent NPC offers queue by actual occasion; they do not each hold an independent reward claim over the same player. UI callbacks carry session/revision/phase/episode/node identity; the engine independently checks them. Duplicate command IDs, stale revision, an already answered node, failed persistence, or an invalid evidence reference produce no second debit, reward, knowledge, relationship effect, or obligation.

One ordinary free probe per existing allowed person/week remains unchanged. A new free role episode can earn its relationship outcome only once per exact role grant/occasion. Other new repeatable branches require a paid episode or a genuinely new persisted house occasion, never a new menu instance, room, wording variant, or generated UUID. Content-specific cooldowns and max-one settlement apply through reload. Define record retention and pruning so expired records cannot recreate an old reward; reconcile those bounds with safe-loader depth/size limits before schema adoption.

## Delivery order and required measurements

1. **Presentation candidate now:** compact nested categories; current phase/role-valid existing actions; existing free/paid labels; receipt-to-speaker adapters for Lobby, AskVote, ReadPerson, incoming replies, and storyline choices. Verify unchanged state/RNG/costs from navigation and Continue/Skip, and unchanged command outcomes. No new gameplay reward.
2. **Versioned content contract:** after Dev's reader gate, implement one small campaign tree and one conditional-veto statement tree. Reuse persisted story paths where they express the required authority; do not create a second generic story resolver. Extend canonical commitment storage only for the explicit conditional promise branch.
3. **Role/time pilot:** implement the named grants and the separate capped time receipts. Exercise all role combinations and closing boundaries before tuning any defaults.
4. **B9 successor measurement:** run paired seeds against the retained untuned population using the same agreed policies/casts, followed by the agreed full cohorts. Preserve old raw/golden records and record the new version as a separate candidate.
5. **Combined native validation:** isolated NoUMA and actual UMA paths, actual pending-offer approach and interruption, persistence/recovery, compact/wide/large-text layouts, and screenshot evidence with both characters visible. Keep source changes frozen during each run. Then use existing combined acceptance/promotion gates.

B9's owner-selected targets remain the current ones: accepted Countered transactions carried at 35-50%; fixed-0.50 Studier eligible weekly HoH/Veto wins at 60-70%; actual season Winner rate at 25-35% for eight and 18-28% for twelve. The new opportunities may change decision policies and resulting populations. Do not call this tuned or substitute a new denominator for an existing target.

Add separate diagnostics for grants offered/used/expired by role, overlapping grants, ordinary actions spent, unique completed episodes, paid versus free episodes, +1/0/-1 outcomes, cap suppressions, actual usable time, role/cast/phase subgroups, NPC initiated offers, approach completions, lies/deflections, bindings created/kept/broken, and player-known versus private evidence. Record relationship and time outcomes separately, including losses that were capped to zero. Include native-world NPC ticks so command-only lab behavior is not mistaken for the shipping house.

Repeat/abuse checks must include free-loop warmth farming, alternating +1/-1 refund farming, dual-role stacking, renomination, repeated veto questions, stale choices after phase advance, load mid-branch, double click, save failure, interrupted NPC approach, blocked routes, unmet conditional promises, hidden-alliance/ballot leaks, and neutral exits at every decision node. Historical seasons must retain byte-stable outputs under their existing versions, including old open templates and old extra-action purchases.

No new test launch or product integration was performed for this design. The presentation implementation and its own evidence are separate from the unimplemented gameplay extension described here.
