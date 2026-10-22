# Twelve All-Star strategic campaigns: content brief

21 October 2026. Design for user step4; NOT integrated content, a native test result or a save-format change. This brief was written only in the existing task directory during the native freeze. Its only project-source input is the named CLOSED snapshot:

`D:/GamesimAcceptance/Logs/validation/r2-dialogue-pilot-uma-graph-lifecycle-single-native01-20261021T141850180Z-fe525a47d946440cbbb366ecdfb228f3/snapshot/Assets/Gamesim`

All situations and suggested dialogue below are fictional events in the player's simulated season. They are not claims that the real alumni experienced these events or said these lines. Characterization follows the existing twelve `LoreLegacy` cards' game-facing tendencies; no private biography, romance, invented personal secret or misconduct is added.

## Creative and implementation direction

Make the alumni recognizable through the decisions they ask the player to make: Cody asks for a precise word; Dan negotiates agency and credit; Derrick exposes inconsistent criteria; Danielle tests a read; Vanessa prices a deal; Jun controls when useful information travels. A different name and portrait on the same persuasion tree would not meet this brief.

Each campaign has four beats below. The first two or three can contain player decisions; the final beat is a payoff from the actual season record. There are at most three consequential choices per dialogue episode and four offered choices at a node. Every node has a truthful neutral exit. A campaign can fail, be declined or end early without pretending that the player completed it.

The completion target is twelve authored and tested campaigns, not twelve campaigns forced into every season. Eligibility depends on the actual cast and season. Initially offer at most two distinct alumni campaigns per season, at most one active bespoke campaign at a time, and at most one opening per week. These are proposed content pacing limits, to be measured alongside the existing story budget. Do not steal existing thread slots or starve the ordinary story pool. Each alumnus/template opens once per season; a declined opener is not silently offered again. The player can continue an accepted chapter without repaying an already settled entry cost, under the qualified generic dialogue rules.

## First playable slice: Cody Calafiore — One Vote at a Time

`sourceTemplateId: cody-calafiore`; proposed immutable content ID `allstar-cody-one-vote-v1`.

Why first: one existing voting occasion, one NPC and the player, one explicit canonical Vote obligation, one real settlement. It proves personalized writing, consent, save/resume, private information handling and a finale callback without needing a new minigame or long-term counterparty scheduler. Implement only after the generic dialogue/historical-save and canonical commitment gates are qualified.

Entry predicate: Cody is an active NPC and current nominee; the player is an eligible voter; the Campaign window is open and unresolved; the generic cost/entry rules permit entry; no duplicate or contradictory word exists; no other bespoke episode owns the interaction. Use the actual canonical eligibility reader, not a parallel copy. If another nominee is the player's prior beneficiary, the UI shows that conflict and does not offer a duplicate promise. Start from an actual legal campaign, never force Cody onto the block for shipping behavior.

1. **A precise ask.** Cody asks whether he can count on the player's vote this week. Suggested fictional line: “I don't need you to promise the whole season. Tell me what you're willing to do tonight.” Choices: hear the terms; keep the vote undecided; decline. Hearing terms is not consent and creates no word, warmth reward or time refund.
2. **Your word, in full.** The confirmation shows maker=player, beneficiary=Cody, the current nomination frame, exactly what the vote obligation means, its expiry and the normal breach consequence. Choices: give that word; ask to keep the vote open; leave. This first slice uses a voluntary unilateral word; it does not add a beneficiary-acceptance requirement or silently turn it into a reciprocal deal. No relationship test can substitute for explicit player consent. A recorded promise produces the existing canonical record and Your Word display once. No FinalTwo, Safety or indefinite loyalty obligation is attached.
3. **What the ballot changed.** At the existing vote settlement, classify the actual canonical result: kept; breached; lapsed/impossible; or no word given. A kept word does not imply Cody survived, and Cody leaving does not imply the player broke it. Read the real settlement; never reconstruct a promise's fulfillment from survival alone. Show only information the player is entitled to know; a secret ballot is not magically exposed to Cody or the house by this campaign. Where Cody cannot know the player's ballot, his response remains uncertain and the private player receipt says what actually happened.
4. **The cost of the choice.** Finale recap references the recorded promise and known result, or the recorded decision to stay uncommitted. If Cody is a juror and the existing question-selection rules select an eligible actual receipt, personalized wording asks why the player made that choice. It does not add a jury vote, inject another question, or stack another loyalty penalty. If he is a finalist or pre-jury evictee, the same facts appear as a recap rather than an invented juror exchange.

Endings: `kept-survived`, `kept-departed`, `breached`, `lapsed`, `undecided`, `declined`, `player-departed`. Names describe actual receipts and status; they are proposed bounded ending IDs, not new score modifiers. No reward for selecting a flattering line. The consequence is the existing word, its cost and its settlement.

Departure rule: once Cody or the player becomes ineligible, close the interactive episode. Retain its cast/history. Existing commitment policy decides whether an already accepted word settles or lapses; the campaign does not erase it. No post-eviction conversation with an absent body is scheduled.

Acceptance for this slice: every branch including refusal; exact single debit and single obligation; duplicate/stale click refusal; save/resume at both choices and after settlement; failed-save rollback; changed nominees/phase/voter eligibility; Cody evicted despite a kept word; survival despite a breach; absence of hidden-ballot disclosure; actual finale/recap status variants; unchanged old-season outputs. Review the rendered consent card and the full native route, not only pure engine assertions.

## Remaining eleven campaigns

### Dan Gheesling — Whose Move Is It?

`dan-gheesling`; `allstar-dan-whose-move-v1`. Tension: influence versus ownership, drawn from the existing card's calculated approach and tendency to work through other players. Opens when Dan and the player can legally discuss the live nominations; it does not require inventing Dan's secret plan.

1. **An outline, not an order.** Dan offers a plan based on an actual player-known target and asks the player to carry it. The player can request the reasoning, propose their own target, or decline. Candidate targets are real eligible contestants, not leaked hidden preferences.
2. **Who takes the risk?** Choose to make the existing legal lobby yourself; ask Dan to make his own lawful approach; or keep it as discussion. A request to Dan is not a successful lobby. He can refuse under his real agenda/availability; a refusal closes that branch without spending the player's proposed action or claiming that he acted.
3. **The nominations answer.** Compare the actual nominations and recorded participating actions. If the plan missed, admit it or discuss the changed circumstances. If it landed, give accurate shared credit or make a subjective ownership claim explicitly labelled as a claim. Do not manufacture causal credit merely because the chosen target was nominated.
4. **Who owned the risk?** The finale uses the player's recorded action and public power outcome to ask whether they acted independently or borrowed a plan. Credible ownership and acknowledged cooperation are distinct valid endings. Dan's praise does not automatically award a résumé point.

If the target leaves before the nomination occasion, end `target-no-longer-eligible`; no automatic substitute. If Dan leaves, preserve the proposal as a proposal and recap only actions that actually occurred.

### Dr. Will Kirby — Terms Behind the Smile

`dr-will-kirby`; `allstar-will-clear-terms-v1`. Tension: enjoying the performance without confusing charm, intention and an agreement.

1. **The easy answer.** In a legitimate campaign conversation, Will gives an uncommitted line. The player can play along, ask what he is actually offering, or leave. Friendly dialogue alone creates no Vote/Safety row.
2. **A promise or a possibility?** Ask for a precise existing deal, ask for a nonbinding read, or explicitly remain uncommitted. Will's consent/refusal follows the qualified NPC policy. The receipt clearly distinguishes an intention from an accepted obligation.
3. **The reveal.** The actual vote/settlement may support the arrangement, break it, or leave an earlier claim unverified. The player can acknowledge having accepted uncertainty or challenge a contradiction only when the evidence is player-known. No new lie is attributed simply because an outcome was unfavorable.
4. **What did you knowingly buy?** Finale callback asks about taking a calculated risk on an explicit bargain versus trusting a pleasant answer. Its wording cites the exact earlier choice and known result. No automatic “Will betrayed you” story.

Refusal ends `no-deal`; death of the voting occasion ends `occasion-closed`; departure preserves the last honest uncertainty instead of forcing a confrontation.

### Derrick Levasseur — Name the Exception

`derrick-levasseur`; `allstar-derrick-exception-v1`. Tension: a decision rule that becomes inconvenient when it touches an ally. Opens when the player has actual nomination authority, or a genuine legal consultation with the current HoH.

1. **State the rule.** Derrick asks for the player's stated priority: public competition threat, a known broken word, or current relationship concerns. Show only criteria supported by facts the player knows; “I haven't settled on one” is valid.
2. **Apply it to your ally.** If the same criterion genuinely implicates a player ally, discuss that exception. Choose a consistent recommendation, openly disclose the exception, or withdraw the recommendation. No invented allegiance or hidden breach is revealed to create a dilemma.
3. **The decision belongs to its owner.** Read the actual nomination. A player HoH owns their choice; an NPC HoH can reject advice. Record whether the player's proposal was consistent, revised transparently, or still unresolved. Discussion does not become a Safety word.
4. **The reason you gave.** Recap and any eligible finale question compare the stated reason with the public decision. Changing one's mind with an explanation is not automatically dishonesty; knowingly claiming the opposite of a recorded public act remains challengeable through existing evidence rules.

If the player loses authority or the target becomes ineligible, the decision closes without retroactive obligation. Derrick's departure stops new questions but retains the stated criterion and actual decision.

### Janelle Pierzina — Earned Leverage

`janelle-pierzina`; `allstar-janelle-earned-leverage-v1`. Tension: preparing to compete versus asking others to carry your game. Opens before an actual competition in which the player can participate.

1. **Back yourself.** Janelle asks what the player intends to prepare. Choose the existing paid study/practice action, conserve the action for social play, or decline the challenge. Never silently grant an extra study or penalize an accessibility setting.
2. **The competition is the test.** Play the actual challenge with its normal rules. Winning, losing after study, losing without study, and an ineligible/withdrawn occasion are distinct observations. Janelle does not award a fake win or read the player's unobserved inputs.
3. **Spend the leverage.** With real power, choose a legal strategic action or explicitly decline Janelle's suggestion; without power, make a lawful campaign/lobby request or acknowledge that it is not yours to decide. Support does not create a new promise without confirmation.
4. **More than the scoreboard.** Finale callback distinguishes actual wins from effort and the choices made with power. A loss followed by a sound social decision can be a satisfying ending. The campaign never guarantees better competition odds or a juror's vote.

If Janelle leaves, the competition result still exists, but no absent-person praise scene plays. If the competition becomes unavailable, close `no-contest-occasion` with no replacement reward.

### Danielle Reyes — Two Readings of the Room

`danielle-reyes`; `allstar-danielle-two-readings-v1`. Tension: trusting a perceptive ally while respecting uncertainty. Opens from one real player-known observation that can support two reasonable strategic interpretations.

1. **A read, labelled as a read.** Danielle offers an interpretation of the observed conversation/relationship, explicitly not proof of a secret alliance or ballot. Choose to discuss it, challenge it respectfully, or keep your own counsel.
2. **What would change your mind?** Seek an existing legal conversation/witness opportunity; act on the uncertain read now; or wait for the next public decision. The first route costs the ordinary action and may learn nothing. Do not guarantee useful evidence because the campaign needs it.
3. **The public test.** Compare the next actual nomination or disclosed agreement with the bounded prediction. Classify supported, contradicted or still unresolved. A matching outcome is not proof of hidden motive. The player can credit the useful read or own the risk they took.
4. **Read, then decide.** The finale emphasizes the distinction between a good read and an accountable decision. Cite the actual chosen action, not supposed mind-reading. No automatic belief update about concealed alliances.

If the subject leaves, stop the pending investigation and retain `unresolved-subject-departed`; if Danielle leaves, her recorded interpretation remains a claim, not posthumous proof.

### Vanessa Rousso — The Price of One Vote

`vanessa-rousso`; `allstar-vanessa-price-of-vote-v1`. Tension: the best arithmetic versus the price a person will actually accept. Opens at an existing eligible pact meeting/campaign with a genuine offer and at least two lawful choices.

1. **Put the alternatives on the table.** Vanessa asks which legal target/deal the player prefers. Show public counts and player-known commitments separately from uncertain intentions. No perfect ballot totals.
2. **Name the cost.** Accept existing terms, make the one allowed counter, or lie low. The counter goes through the actual negotiation policy and can fail. Clearly list the obligation, parties, occasion and expiry before consent; an attractive forecast is not acceptance.
3. **A narrow majority, or no majority.** Read the real negotiated support and vote settlement. Preserve the difference between attempted counter, accepted deal and carried vote; do not change B9 accounting to make the arc look successful.
4. **Was the price worth it?** Finale callback weighs the actual concession and result. A principled refusal may preserve freedom even if the vote is lost. No arbitrary success reward is added on top of the ordinary deal consequences.

If the offered target or voter becomes ineligible, use existing expiry. Vanessa's departure does not silently cancel obligations to other surviving parties or expose her private ballot.

### Tyler Crispen — Separate Conversations, One Choice

`tyler-crispen`; `allstar-tyler-one-choice-v1`. Tension: keeping channels open without promising incompatible things. Opens only when the player already knows two genuine competing requests involving Tyler; the campaign does not reveal his hidden alliances to manufacture them.

1. **Keep the doors open.** Discuss both known requests or decline involvement. Merely listening does not join an alliance or certify an accusation.
2. **Set the boundary.** Choose one specific existing deal, tell both sides the player is uncommitted where disclosure is lawful, or reject both. The system refuses mutually incompatible new obligations under the qualified unified rules; it does not invite a double-promise exploit.
3. **The choice becomes visible.** At the real decision, react to what each participant actually knows. Tyler can accept the boundary, continue persuasion within the existing rules, or refuse cooperation. No public exposure unless a legal disclosure action and actual audience warrant it.
4. **Access is not allegiance.** The finale distinguishes maintaining useful contact from keeping a word. Recap the promise actually made and settled, without treating every conversation as a pact.

An obsolete request lapses individually. If one side leaves, do not rewrite the earlier record as though there was never a difficult choice.

### Chelsie Baham — When to Show Your Hand

`chelsie-baham`; `allstar-chelsie-show-your-hand-v1`. Tension: timing and audience for legitimate strategic information.

1. **You know something useful.** Start from an existing player-owned shareable fact about the current game, not Chelsie's hidden knowledge. Discuss whether disclosing it could change a live decision.
2. **Choose the audience.** Share through an existing legal command with one eligible recipient, disclose publicly only if the actual fact permits that route, or keep it private. Display what will be shared and with whom. Chelsie may decline to carry information; her agreement is not a forced successful leak.
3. **The window moves.** Follow the actual audience/knowledge receipts and subsequent public decision. Early disclosure can enable a response; holding it can preserve information but miss that occasion. Do not deterministically punish either choice or infer invisible downstream conversations.
4. **Timing you can defend.** Finale callback references who genuinely learned what and when, restricted to the permitted audience. The public recap redacts still-private content. Credit is a defensible claim, not a free résumé score.

If the recipient departs before delivery, cancel that delivery. If Chelsie leaves, player-owned facts retain their ordinary availability; the campaign does not seize them or reveal them for closure.

### Rachel Reilly — Say It to My Face

`rachel-reilly`; `allstar-rachel-direct-answer-v1`. Tension: a direct strategic disagreement without inventing personal misconduct. Opens on an actual known disagreement in the season, such as a lawful target proposal Rachel does not support.

1. **A clear objection.** Rachel asks the player to state their position directly. Choices: own the disagreement; discuss it privately; or decline further debate. None is labelled cowardice, and “calm down” is not a mandatory trap answer.
2. **One claim, one answer.** Cite a valid player-known game fact, acknowledge that the dispute is a judgment call, or propose a specific lawful compromise. Claims remain claims; a compromise requires both parties' consent and ordinary funding.
3. **The decision arrives.** The public nomination/vote outcome or actual settled word provides closure. A direct disagreement can end respectfully even if neither side changes their vote. Do not create a strike, blow-up or conduct event.
4. **Respect without agreement.** Finale callback contrasts a truthful direct position with a promise actually broken, if one exists. Use the existing canonical consequence once; no extra “confrontational” penalty or guaranteed admiration.

Refusal records `discussion-declined`. Departure ends the conversation promptly; the recap describes the disagreement, not an invented reconciliation scene.

### Xavier Prather — The Plan Everyone Signed

`xavier-prather`; `allstar-xavier-common-plan-v1`. Tension: alliance cohesion versus an individual member's interests. Opens only in a real known alliance of at least three with an eligible existing pact meeting.

1. **Hear the disagreement.** Xavier proposes hearing the actual eligible members' preferences. The player can join the meeting, decline, or ask for their own known position to be considered. No invisible member is represented by a fabricated vote.
2. **A plan, not unanimity.** Back the actual proposal, make the one legal counter, or abstain. Show who has actually agreed using existing visibility rules. A majority plan is never described as unanimous when a member dissented.
3. **Who carried it?** At the actual settlement, distinguish the plan, members' permitted known behavior and results. A member's departure or loss of voting eligibility is not automatically disloyalty. Breaches belong to their real makers.
4. **What held the group together?** Finale callback asks whether the player listened, compromised or knowingly chose their own game. Preserve dissent in the record; no flat bonus for following every order.

If the alliance dissolves before the meeting, close `pact-dissolved`; obligations already accepted settle under existing rules. Xavier leaving does not transfer his leadership or pledges to the player.

### Jun Song — The Kitchen Is a Crossroads

`jun-song`; `allstar-jun-crossroads-v1`. Tension: a useful position between conversations versus unwanted entanglement. Uses the card's existing game-facing kitchen/social tendency, not invented domestic biography.

1. **An ordinary shared task.** An existing kitchen/house-life interaction puts the player and Jun in a lawful conversation. Agree to talk, keep the task social, or leave. The scene does not create an invisible eavesdrop.
2. **Separate fact from inference.** Jun and the player discuss one actual player-known observation. Choose to ask an eligible person directly, keep the observation private, or share it through an existing allowed route with uncertainty clearly labelled. Jun's presence does not grant the player's knowledge to her unless disclosure really occurs.
3. **Stay between, or take a side.** At the next genuine request, remain independent or consent to a specific finite deal. Both have costs: less committed support versus reduced freedom. The game does not invent a hidden faction to punish independence.
4. **The route through the house.** Finale recap cites information actually learned/shared and commitments actually made. A quiet game can have a clear record without secretly awarding influence for every kitchen visit.

If the observation's subject leaves, retain it as dated context and close any stale ask. Jun's departure ends the staged scene; her prior private statements are not published for an epilogue.

## Shared saved-state and authority contract

Reuse `StorylineState`: immutable templateId, fixed cast, beatId, path, nextWeek/nextAnchor, status and endingId. Use the existing Game lane and ordinary story scheduling; do not allocate a new manager, scene system or save collection. Each campaign uses at most four roles, four principal beats and four small bounded numeric flags. Existing limits are cast8, path24, vars16 with values -1000..1000; do not repurpose numeric vars for record IDs or unbounded sequences. Branches should generally be derived from path and canonical receipts, leaving fewer redundant flags.

The generic dialogue layer owns each interactive episode, command identity, response/evidence snapshots and time settlement. The story cycle owns campaign progress. Keep these owners distinct and link them only through the qualified generic mechanism. The CLOSED snapshot's DialogueRules permits only its current pilot; these new IDs are not presently accepted merely because they fit a DTO. Implement catalog/validator/UI support under the next approved content/rules boundary, using existing version fields. Never rename V1/V2 IDs, reinterpret their saves, or turn new campaigns on for an old season through load-time inference. No new schema number is proposed here.

The existing canonical commitment record has no generic campaign-reference field, and its `origin` is a validated closed vocabulary. Do not put a campaign ID into origin or invent a serialized join. For the first slice, the generic commitment work must provide an exact existing accepted-command/receipt association and prove the one relevant row can be recovered unambiguously on load. If that association is not yet qualified, keep the content inactive rather than selecting “the latest promise.” A proposal, intention, accepted word and settled outcome remain distinct.

At every choice, revalidate participant status, live power/nomination frame, phase, occasion, evidence and audience. Use the engine's normal guarded command path and Store transaction; a UI button cannot directly alter standing, voting, knowledge, alliance membership or AP. Decline creates no obligation and no farming reward. An explicit “ask again later” may defer only to a single actual later anchor while the same occasion is valid, and cannot produce a second opening or fresh entry benefit.

Once a campaign is terminal, its small historical cycle remains available for recap; it does not occupy a live interaction. An NPC leaving closes interactive work immediately, with distinct `subject-departed`/`occasion-closed` endings where appropriate. Existing owed commitments settle through their owner. The player leaving closes their active campaign without simulating choices for them. Do not recast the named alumnus with another body to force a conclusion. The NPC's identity is the existing roster template ID, not a display-name substring; customized cards use their actual displayed identity and must not impersonate a different saved template.

The finale reads only real durable outcomes. Add personalized phrasing to an already eligible question when its exact receipt is selected, retaining the existing question count and scoring; otherwise provide a noninteractive recap. Never fabricate a question or answer in `juryExchanges`, force the named NPC onto the jury, disclose a secret ballot, or award an automatic vote. A recap may say that a belief remained unresolved. Do not double-count canonical warmth, breach, deal or jury effects with a campaign-specific bonus.

Pacing and repetition use existing headliner/cooldown and story-budget facilities plus catalog predicates over retained cycles. Do not use real time or main RNG for campaign scheduling. Eligibility/casting ties use existing deterministic story selection and stable IDs. At capacity, refuse the complete new path rather than partially allocating it. Measuring a season where a campaign never becomes eligible is a reach result, not permission to force its trigger.

## Integration sequence and evidence

1. Finish the currently ordered generic dialogue, historical-reader/save, unified consent/settlement and role/AP gates. Preserve all old schema/content behavior and test floors.
2. Implement Cody only, with the existing catalog/engine/director/receipt routes and ordinary owned scene staging. The two narrative decisions and actual reveal/recap are the first complete vertical slice. No second campaign until save/load, privacy, refusals and native route evidence pass.
3. Add three contrasting arcs next: Danielle's uncertain evidence, Janelle's actual competition, Xavier's multi-member pact. These exercise information, power and group authority, not just alternate dialogue text.
4. Add the remaining eight through the same qualified path. Review each for meaningful branch differences, truthful consequences and valid exits. Keep story authoring separate from retuning NPC decision coefficients.
5. Measure eligible/offered/accepted/completed/declined/expired counts separately in regular, All-Star and mixed casts of8/12/16. Report per-character reach, repeated asks, unresolved/early departures, choice distribution, player comprehension and campaign decisions per season. Start from the proposed two-campaign budget; do not claim the historical story pacing report validates these new arcs.
6. Run native save/recovery and real season walks with the qualified source, preserving hidden-category adapter coverage. Then inspect ordinary and Largest text, keyboard/controller focus, caption duration, staged body ownership and reduced-motion variants. Human playtests should ask whether the player can explain what they promised, what remained uncertain, and why the outcome happened.

Source basis: `Simulation/Story/LoreLegacy.cs` ECDBC33F7BC01FBF13C62EECC491610ABFE4DC6E9936F3C0E455FA8730F4059D; `StorylineState.cs` 0E72D761FD93CDFE30ED1ADBFB94152EB1E3E5330E522DAF5FDDDFFD25748029; `Story/StoryTemplates.cs` 9F59EC3B21C4EACA91A585B063541A17CD2B6E589E77279C442B43B4E5DA4B17; `Story/EpisodeStoryValidation.cs` 48A89F4F28E6F1930FD8CEDF73BCC0EC2809D0DBCE33CC8DD7B3FBBBC0307E70. Relevant observed contracts also include StoryPeople, DialogueState, UnifiedCommitments, FinaleQuestions and EpisodeDirector.Finale in that same CLOSED snapshot. These pins establish the design's source inputs; no content was imported or executed.
