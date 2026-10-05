# Wave D1 - unified commitments: implementation contract

2026-10-05 UTC. Owner requires all four Wave D systems before completion. This
is the source-reviewed D1 design, not an implemented/accepted unified model.
The existing Your Word page aggregates four mutable stores; it does not itself
establish one authority. D1 remains open until every family is integrated.

## Dependency order

1. Freeze the actual schema-23 contract, including valid pitch cards, campaign
   vote lobbying and reserved pitch/block-speech receipts. Do not widen any
   frozen version1-22 or pretend removing two economy fields makes23 identical
   to22. The new contract is a prerequisite, initially unused by live dispatch.
2. Introduce schema24 with a separate unified-commitment rules version and a
   bounded canonical record list. Upgrade1-23 by detached validation/clone and
   explicit disabled/empty additions only. No conversion, new IDs, settlements,
   history, budget or RNG changes. Even a pristine fresh-looking23 save remains
   legacy. Enable only through new-game creation, never the importer.
3. Integrate safety end-to-end through one canonical authority, then final-two,
   vote/plea, oath and alliance-call families through the same record/service.
   Partial safety integration is D1a, not completion of D1/B1.
4. Move shared warnings, page, reputation, historical summaries and mechanical
   readers to canonical views, with one effect per incident. Legacy seasons
   retain their complete existing writers/readers/outcomes behind version0.

## Canonical authority, not a second ledger

In an active new-model season, canonical safety records are the sole writable
safety authority. Do not also write safety mirrors to promises/deals. Other
families remain in their existing stores until their coherent transition. A
read-only identity resolver must support saved source IDs and mixed-family
links; a copied record is not a second settlement authority.

One record retains stable ID, parties, directional/reciprocal duties, creation
week and expiry, source policy and authoring origin, proposal/active/kept/broken/
declined/lapsed status, settlement actor/week, trust/accepted-offer provenance,
linked consideration and shared settlement-effect identity. Vocabulary must be
bounded and strict. Canonical creation remains internal to validated commands
and their existing save-before-publication transaction, never a UI callback.

Suggested service responsibilities: pure offer validation, accepted creation,
pending response, read-only reference lookup, binding/strongest protection,
pure decision evaluation, sole settlement/effects gateway, expiry/departure.
Command costs/acceptance RNG stay with their existing owners. Validate duplicate,
capacity, role and term constraints before spending or rolling where possible.

## Preserve source policies explicitly

- A safety promise is one-way maker to beneficiary, normally through next week.
  Actual nomination by its maker breaks it; otherwise it expires. Existing
  safety promises are not fulfilled just because somebody was spared.
- An ordinary safety deal is reciprocal and normally this week. It can finish
  fulfilled at the final veto meeting when the partner was spared, but a person
  nominated earlier that week is not eligible even after being veto-saved.
- Lobby safety is reciprocal with next-week expiry but still follows that deal
  fulfillment rule. Never extend protection or grant reverse consent implicitly.
- Call-in-promise and existing source refs/knowledge retain resolvable IDs.
  Negotiated consideration can be safety while the bought commitment is veto
  or another legacy family; reciprocal links must remain atomic and valid.
- Keep existing authoring capacities: canonical promise-origin records count
  with legacy promises toward200; canonical deal-origin records count with
  legacy deals toward200 and the player-proposal threshold40. No eviction of
  active records to make room, and no budget bypass by moving stores.

These policies are sourced from EpisodeEngine.MakePromise/nomination/expiry,
DealResolution.Spared, EpisodeEngine.Strategy lobbying, Negotiation call-in/
price resolution, PlayerDeals capacities and NpcDeals production. They are not
interchangeable just because the current captions both say safety.

## Overlap policy for new-rule games

Reject an identical active directed duty/target/term before cost/RNG. A reverse
duty, genuine extension or accepted reciprocal agreement is not automatically
a duplicate. Different terms retain explicit provenance. Apply the strongest
applicable protection once, never sum overlapping safety records.

Group breaches by decision, actor and wronged party. Mark every affected record,
retain every evidence ID, but produce one relationship/reputation consequence
and one witness procedure. Select the effect owner deterministically: strongest
applicable source consequence, then stable ID. Historical/jury/broken-word
counts must count the grouped incident. Voided consideration is not another
betrayal. This collision policy is a deliberate native rule extension: isolated
source commitments retain their own consequences, while migrated legacy games
retain their existing overlap behavior. Do not claim deduplication and full
overlapping-effect parity simultaneously.

## Integration coverage required

Every creation origin: player promise and HoH-pitch reply, NPC promise/deal,
player proposal, lobby safety, negotiation/price and story effect. Every
resolution: initial/replacement nominations using only that action's nominees,
veto completion, expiry and departure. Sole settlement gateway replaces all
safety-specific calls to SettlePromise/SettleDeals.

Reader coverage includes CommitmentsRead warnings/page; StrategyRules, voting,
jury and ThreatAssessment/Breaches; Negotiation call-in and bundle links;
Knowledge/YourWord; HouseguestNotes, YourWeek, FinalistRead, FinalArgument,
GameSense, story refs and the director. Do not move authority while leaving
hidden legacy readers or broken link resolution.

## Required evidence

Pure cases: every origin/policy/cap, duplicate-before-cost/RNG, genuine extension,
strongest-only protection and grouped consequences, nomination/replacement/
spared/expiry/departure, call-in by stable ID, mixed consideration, warnings equal
verdicts and unchanged legacy season digests. Persistence cases: complete strict
frozen23 acceptance/rejection; disabled empty24 migration of1-23 without byte
rewrites; current strict authority/no competing stores; exactly-once settlement,
corrupt-state refusal, checksum/backup/recovery and original-save preservation.

Native UI cases: actual commands/buttons, stale callbacks, cancelled drafts,
save failure before publication and reload. Then full NoUMA/UMA suites at the
same combined pin, a separately verified shipping build and actual graphical
checks. None of this replaces windowed1080p60 GTX1060 or the three required
first-time human playtests. D2 all-week strategy, D3 negotiated meetings and D4
secret leaks/double-dealing remain separately required and not implemented here.
