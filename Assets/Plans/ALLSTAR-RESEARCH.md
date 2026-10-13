# All-Star research and characterization

Catalog version: `bb26-2024-v1`. Canon closes after BB26 in 2024. It includes eligible shared reality-show appearances, including Snake in the Grass (2022), The Traitors US1 (2023), The Traitors US2 (2024), and Big Brother Reindeer Games (2023). BB27, Deal or No Deal Island 2025, The Traitors US3, and other later events are outside this version.

The immutable catalog separates sourced public events from authored game behavior. Its relationship numbers, trust offsets, style priorities, voice guides and dialogue examples are design choices, not measurements of the real people or quotations. Public information supports a reputation; it does not give an NPC knowledge of another NPC's current private ballot, motives, relationship score or deal.

## Characterization contract

The twelve stable identities match the existing All-Stars roster exactly. Names, titles and gameplay identity are separate: `dr-will-kirby` remains the stable template ID, while the title is `Dr.`, the given name is `Will`, and the family name is `Kirby`. Short references use `Will`; full references use `Dr. Will Kirby`. A title must not be extracted as his first name.

| Profile | Authored strategic priorities and voice |
| --- | --- |
| Dan Gheesling | Individual persuasion, several routes to the end, and a possible crisis reversal. Calm reassurance; account for the jury cost of betrayal rather than guaranteeing that a clever survival move wins the game. |
| Dr. Will Kirby | Visible threat reduction, entertaining social access, conditional competition throws and useful shields. Playful irony and charm; neither compulsory losing nor zero competition ability. |
| Derrick Levasseur | Stable coalitions and separately protected social options. Measured questions that make a workable plan feel shared; no omniscient detective dialogue. |
| Janelle Pierzina | Competitive survival and reciprocal protection. Direct questions about missing information; friendship and strategic trust can diverge. |
| Cody Calafiore | Early power, dependable protection, coalition stability and decisive endgame competition. His own strategic choices remain visible; he is not simply another winner's subordinate. |
| Danielle Reyes | A dependable concealed partner, observation of alliance links and adaptation to a returning-player target. Social warmth and wit with concise private analysis. |
| Vanessa Rousso | Evidence, deal conditions, bridges between groups and alternatives under uncertainty. Detailed questions; genuine emotion and logical choices can coexist. |
| Tyler Crispen | Individual reassurance, overlapping protection and a genuine core partner. Relaxed speech; managing multiple promises carries risks. |
| Chelsie Baham | Timed disclosure, attention to other real threats and rebuilding after a power shift. Friendly confidence with sharper private analysis; no invented evidence. |
| Rachel Reilly | Competition, reciprocal loyalty, specific confrontations and adaptive social protection. Energetic and direct, with quieter cooperative choices too. Her BB13 adaptation is already documented before the cutoff. |
| Xavier Prather | Composure, coalition protection, priorities among competing loyalties and endgame cuts. Measured agreement; credit Tiffany and other Cookout members for their contributions. |
| Jun Song | Accessible house activities, information from both sides and active middle positioning. Dry wit and practical warmth; floating is not inactivity. |

These are ranked authoring hints. Mechanics should map recognized template IDs to an explicit preferred repertoire instead of parsing English hint strings. Current events, available information, safety and legal action rules remain stronger than a preference. A player controlling an alumnus chooses their own behavior.

## Directed relationship defaults

Each row is one unique shared-history pair. Direction is meaningful. Relationship and prior trust are separate. The columns below are `relationship / priorTrustOffset`; actual prior trust uses `50 + offset`. These are approved authored starting values, not empirical personality scores.

| Stable fact ID | Left participant | Right participant | Left toward right | Right toward left |
| --- | --- | --- | --- | --- |
| `hitmen-bb16` | Derrick | Cody | 60 / +25 | 60 / +25 |
| `janelle-rachel-snake` | Janelle | Rachel | 45 / +15 | 45 / +15 |
| `cody-danielle-reindeer` | Cody | Danielle | 25 / +10 | 25 / +10 |
| `danielle-xavier-reindeer` | Danielle | Xavier | 25 / +10 | 25 / +10 |
| `cody-tyler-bb22` | Cody | Tyler | 25 / 0 | 25 / 0 |
| `will-janelle-bb7` | Will | Janelle | 20 / -10 | 20 / -10 |
| `will-danielle-bb7` | Will | Danielle | 20 / -10 | 20 / -10 |
| `danielle-janelle-bb7` | Danielle | Janelle | 10 / 0 | 10 / 0 |
| `janelle-dan-traitors` | Janelle | Dan | 25 / -10 | 25 / 0 |
| `janelle-cody-bb22` | Janelle | Cody | -15 / -10 | 0 / 0 |
| `janelle-tyler-bb22` | Janelle | Tyler | -15 / -10 | 0 / 0 |
| `rachel-cody-traitors` | Rachel | Cody | 10 / -10 | 10 / 0 |
| `cody-xavier-reindeer` | Cody | Xavier | 0 / -10 | 0 / 0 |

Historical affection is not current protection. Old pairs can repair, fracture, become shields, or become targets in the new season. Strong friendship must not reveal a hidden current final two. Dan and Will have evidence of mutual strategic respect but no shared contestant season in this canon; they are not a fourteenth co-player history.

With histories OFF, individual style and generalized public reputation remain. Remove interpersonal priors, old pair commitments, pair-based threat assumptions, and dialogue referring to old interpersonal events. Career metadata in the catalog is not a permission to show those events in an OFF conversation.

## Evidence ledger

`AlumniCatalog.Source` holds stable source IDs, URLs and publication years. Every career fact and pair fact names its supporting source IDs. Event year is separate from publication year. Sources are selected for documented public gameplay and participant interviews, not fan assertions about private relationships, diagnosis, romantic motives or permanently held grudges.

| Source ID | Evidence used |
| --- | --- |
| [`dan-bb14-2012`](https://bigbrothernetwork.com/big-brother-14-runner-up-dan-gheesling-interview/) | Dan's final-two positioning and acknowledgment of failed jury persuasion. |
| [`dan-will-2013`](https://www.myentertainmentworld.ca/2013/03/my-tv-award-nominee-qa-dan-gheesling/) | Will's strategic influence on Dan; reputation respect rather than a shared season. |
| [`will-janelle-2006`](https://www.tvguide.com/news/bbs-janelle-showmances-37332/) | Janelle's competitive pressure, friendship with Will and decision to evict him. |
| [`will-strategy-2017`](https://www.globaltv.com/big-brother-alum-will-kirby-talks-season-19-chairing-jury-debate/) | Conditional throwing, humor and charisma. |
| [`derrick-bb16-2014`](https://bigbrothernetwork.com/derrick-levasseur-big-brother-16-winner-interview/) | Hitmen trust and overlapping protective relationships. |
| [`cody-bb22-2020`](https://parade.com/1113492/mikebloom/cody-calafiore-big-brother-22-winner-interview/) | Coalition management, qualified trust in Tyler and endgame autonomy. |
| [`janelle-bb22-2020`](https://parade.com/1081772/mikebloom/janelle-pierzina-big-brother-22-evicted-interview/) | BB22 groups, threat reputation and nomination context. |
| [`janelle-traitors-2024`](https://parade.com/tv/janelle-pierzina-the-traitors-season-2-exit-interview) | Friendship with Dan, early cooperation and later conflict over information. |
| [`danielle-bb7-2006`](https://www.jokersupdates.com/jarticles/danielle-reyes-chat-interview) | Concealed trust with Jason, respect for opponents and not assuming old friendships remain alliances. |
| [`bb7-cast-2006`](https://www.paramountpressexpress.com/cbs-entertainment/releases/?view=12939) | Official shared cast, including Will, Danielle and Janelle. |
| [`bb7-danielle-exit-2006`](https://www.realitytvworld.com/news/big-brother-7-all-stars-evicts-two-houseguests-determines-final-four-4316m.php) | Chilltown's role in the replacement nomination preceding Danielle's eviction. |
| [`bb7-danielle-nominations-2006`](https://www.realityblurred.com/realitytv/2006/08/big-brother-7-janelle_james_nominated/) | Danielle nominating Janelle and James. |
| [`bb-legacy-2022`](https://time.com/collections/reality-tv-most-influential-seasons/6197749/big-brother/) | Will's BB2 competition-free victory. |
| [`bb-cookout-history-2021`](https://time.com/6103015/big-brother-first-black-winner/) | Historic Cookout context and BB3's unsequestered jury. |
| [`vanessa-poker-2015`](https://www.cardplayer.com/poker-news/19484-vanessa-rousso-poker-was-the-best-preparation-for-deep-run-on-big-brother) | Information gathering and uncertainty. |
| [`vanessa-bb17-2015`](https://bigbrothernetwork.com/vanessa-rousso-big-brother-17-finale-interview/2/) | Deals, alliance bridges and misreading an endgame opponent. |
| [`tyler-bb20-2018`](https://www.tvguide.com/news/big-brother-20-tyler-crispen-exit-interview/) | Core Kaycee partnership and overlapping accepted offers. |
| [`tyler-bb22-2020`](https://www.etonline.com/big-brother-all-stars-tyler-crispen-on-what-went-wrong-and-if-he-was-genuine-about-leaving-game) | Committee history and Cody's endgame targeting. |
| [`chelsie-bb26-2024`](https://parade.com/tv/chelsie-baham-big-brother-26-winner-interview) | Rebuilding, timed information and a protected competitive shield. |
| [`rachel-bb13-2011`](https://bigbrothernetwork.com/big-brother-13-winner-rachel-reilly-interview/) | Social adaptation, reciprocal protection and honored endgame deals. |
| [`snake-cast-interview-2022`](https://www.usanetwork.com/usa-insider/snake-in-the-grass-interview-with-survivor-big-brother-contestants) | Rachel and Janelle's shared competition. |
| [`rachel-traitors-2023`](https://www.realityblurred.com/realitytv/2023/01/rachel-reilly-traitors-us-interview/) | Rachel's observation of changed behavior from Cody. |
| [`xavier-bb23-2021`](https://parade.com/1271118/mikebloom/xavier-prather-big-brother-23-interview/) | Emotional regulation, competing loyalties and Tiffany's structure. |
| [`jun-bb4-2003`](https://www.jokersupdates.com/jarticles/jun-song-chat) | Deliberate kitchen access, practical warmth and active positioning. |
| [`reindeer-cast-2023`](https://www.paramountpressexpress.com/cbs-entertainment/releases/?view=108916-big-brother-reindeer-games-announces-the-nine-big-brother-legends-competing-in-the-hit-reality-shows-ultimate-holiday-showdown) | Official shared cast of the 2023 competition. |
| [`reindeer-danielle-cody-2023`](https://parade.com/tv/big-brother-reindeer-games-danielle-reyes-cody-calafiore-elimination-interview) | Mutual respect, Danielle's group and Cody's qualified trust in Xavier. |

## Authoring and validation boundaries

Every `OriginalDialogueExample` is original fictional game dialogue, explicitly labeled by `DialogueProvenance`. Historical references in additional dialogue must require the relevant fact and current participants; they must not recreate an old eviction as if it occurred in this season. The setting never causes historical relationships to overwrite current play during load or a UI redraw.

The catalog has no episode-state API, save fields, serializer attributes, rule activation or random calls. All published records have get-only members and all lists use immutable read-only wrappers. Its tests pin the exact roster, canon cutoff, source coverage, thirteen unique pairs, directed values, title parsing boundary, exact lookup identity and resistance to list mutation. Candidate history mechanics and runtime integration require separate validation; catalog tests alone do not establish their completion.
