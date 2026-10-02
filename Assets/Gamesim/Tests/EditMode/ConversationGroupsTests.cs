using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The pure half of the conversation grouped by intent (ACTIONS-DEALS-ALLIANCES-PLAN V5, V6's
    /// tags, X-a's promise rows and X12's oath copy): what each verb's pill says, which promises a
    /// conversation offers, and what a loyalty declaration says it does. The panel itself is the
    /// PlayMode half (EpisodePlayModeTests.ConversationGroups.cs).
    /// </summary>
    public sealed class ConversationGroupsTests
    {
        /// <summary>A six-house at its campaign, the first houseguest at the head of it and the next two on the block, shaped by <paramref name="shape"/>.</summary>
        private static EpisodeState Campaign(System.Action<EpisodeState> shape = null)
        {
            var state = ContentCatalog.Create(7);
            state.strategyRulesStartWeek = 1;
            var npcs = state.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            state.phase = EpisodePhase.Campaign;
            state.hohId = npcs[0];
            state.nominees = new List<string> { npcs[1], npcs[2] };
            state.vetoHolderId = npcs[3];
            state.vetoPlayers = state.Active.Select(c => c.id).Take(EpisodeEngine.VetoPlayerCount(state.Active.Count())).ToList();
            if (!state.vetoPlayers.Contains(state.vetoHolderId)) state.vetoPlayers[state.vetoPlayers.Count - 1] = state.vetoHolderId;
            state.vetoResolved = true;
            shape?.Invoke(state);
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            return state;
        }

        private static CommandResult Say(EpisodeEngine engine, EpisodeCommandKind kind, string target, string about = null)
        {
            var state = engine.Snapshot;
            return engine.Apply(new EpisodeCommand
            {
                id = "conversation-groups-" + kind + "-" + state.revision, actorId = state.playerId,
                expectedRevision = state.revision, expectedPhase = state.phase, kind = kind, targetId = target, secondTargetId = about,
            });
        }

        [Test]
        public void APromiseToEvictIsOfferedToAVoterInTheCampaignAndNeverAboutTheListener()
        {
            var campaign = Campaign();
            string first = campaign.nominees[0], second = campaign.nominees[1];
            string voter = EpisodeEngine.Voters(campaign).First(c => !c.isPlayer).id;
            Assert.That(EpisodeDirector.PromiseToEvictTargets(campaign, voter), Is.EqualTo(new[] { first, second }),
                "A voter may promise another voter either nominee's eviction.");
            Assert.That(EpisodeDirector.PromiseToEvictTargets(campaign, first), Is.EqualTo(new[] { second }),
                "In a nominee's own conversation only the other nominee is offered: a promise to evict them, to them, "
                + "was kept by voting them out and thanked on their way to the jury.");

            var social = ContentCatalog.Create(7);
            Assert.That(social.phase, Is.EqualTo(EpisodePhase.Social));
            Assert.That(EpisodeDirector.PromiseToEvictTargets(social, social.Active.First(c => !c.isPlayer).id), Is.Empty,
                "Outside the campaign there is no vote to promise.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void APlayerWithoutAVoteIsOfferedNoPromiseAndTheEngineWouldRefuseOne(bool fromTheBlock)
        {
            var state = Campaign(s =>
            {
                if (fromTheBlock) s.nominees[1] = s.playerId;
                else s.hohId = s.playerId;
            });
            string listener = EpisodeEngine.Voters(state).First(c => !c.isPlayer).id;
            Assert.That(EpisodeDirector.PromiseToEvictTargets(state, listener), Is.Empty,
                (fromTheBlock ? "A nominee" : "The Head of Household") + " has no vote to promise.");
            // What the rows would have done: the engine refuses it, so a row could only be refused.
            var result = Say(new EpisodeEngine(state), EpisodeCommandKind.PromiseVote, listener, state.nominees.First(id => id != state.playerId));
            Assert.That(result.accepted, Is.False, "The engine refuses a promise from somebody who does not vote.");
            Assert.That(result.reason, Is.EqualTo("Choose a valid eviction target for your voting promise."),
                "refused for having no vote, not for anything else about the fixture.");
        }

        [Test]
        public void EachVerbsPillSaysWhatItIsFor()
        {
            Assert.That(EpisodeDirector.VerbTag(EpisodeCommandKind.VentAbout), Is.EqualTo(EpisodeDirector.RiskTag),
                "Venting to somebody who does not already dislike its subject takes ten off you half the time; it was filed as social.");
            foreach (var kind in new[]
            {
                EpisodeCommandKind.SmallTalk, EpisodeCommandKind.PersonalChat, EpisodeCommandKind.RelationshipBuilding,
                EpisodeCommandKind.StrategicDiscussion, EpisodeCommandKind.Talk, EpisodeCommandKind.ShareInformation,
                EpisodeCommandKind.PillowTalk, EpisodeCommandKind.Cook, EpisodeCommandKind.InviteUp, EpisodeCommandKind.PublicDefense,
                EpisodeCommandKind.AllianceMeet, EpisodeCommandKind.PlayAGame,
            })
                Assert.That(EpisodeDirector.VerbTag(kind), Is.EqualTo(EpisodeDirector.WarmthTag), kind + " only brings the two of you closer.");
            foreach (var kind in new[]
            {
                EpisodeCommandKind.DiscussGame, EpisodeCommandKind.ShareSecret, EpisodeCommandKind.VentAbout,
                EpisodeCommandKind.SpreadLie, EpisodeCommandKind.SpreadRumor,
            })
                Assert.That(EpisodeDirector.VerbTag(kind), Is.EqualTo(EpisodeDirector.RiskTag), kind + " is a roll that can land against you.");
            Assert.That(EpisodeDirector.VerbTag(EpisodeCommandKind.AskForIntel), Is.EqualTo(EpisodeDirector.LearnTag));
            foreach (var kind in new[] { EpisodeCommandKind.AskVote, EpisodeCommandKind.ReadPerson })
                Assert.That(EpisodeDirector.VerbTag(kind), Is.EqualTo(EpisodeDirector.LearnTag + " · " + EpisodeDirector.RiskTag + " · " + EpisodeDirector.FreeTag),
                    kind + " is free, and not safe: a read they notice costs three, a voter's lie found out sours things at the reveal.");
            foreach (var kind in new[] { EpisodeCommandKind.PromiseSafety, EpisodeCommandKind.PromiseFinalTwo, EpisodeCommandKind.PromiseVote, EpisodeCommandKind.FormAlliance })
                Assert.That(EpisodeDirector.VerbTag(kind), Is.EqualTo(EpisodeDirector.BindsYouTag), kind + " comes due later.");

            // The house's moves and the decision screens keep the words their tiles and rows read.
            Assert.That(EpisodeDirector.VerbTag(EpisodeCommandKind.Eavesdrop), Is.EqualTo("risky"));
            Assert.That(EpisodeDirector.VerbTag(EpisodeCommandKind.HouseMeeting), Is.EqualTo("risky"));
            Assert.That(EpisodeDirector.VerbTag(EpisodeCommandKind.BuyActionPoint), Is.EqualTo("strategic"));
            Assert.That(EpisodeDirector.VerbTag(EpisodeCommandKind.Lobby), Is.EqualTo("strategic"));
            Assert.That(EpisodeDirector.VerbTag(EpisodeCommandKind.RespondToDeal), Is.EqualTo("strategic"));
            Assert.That(EpisodeDirector.VerbTag(EpisodeCommandKind.SetBackdoorPlan), Is.EqualTo("strategic"));
        }

        [Test]
        public void APillSaysFreeExactlyWhenTheEngineSpendsNothing()
        {
            var campaign = Campaign();
            string voter = EpisodeEngine.Voters(campaign).First(c => !c.isPlayer).id;
            string other = campaign.Active.First(c => !c.isPlayer && c.id != voter).id;
            foreach (var (kind, about) in new[]
            {
                (EpisodeCommandKind.AskVote, (string)null), (EpisodeCommandKind.ReadPerson, null),
                (EpisodeCommandKind.Talk, null), (EpisodeCommandKind.VentAbout, other), (EpisodeCommandKind.AskForIntel, null),
            })
            {
                var engine = new EpisodeEngine(campaign);
                int before = EpisodeEngine.SocialActionsSpent(engine.Snapshot);
                var result = Say(engine, kind, voter, about);
                Assert.That(result.accepted, Is.True, kind + ": " + result.reason);
                bool free = EpisodeEngine.SocialActionsSpent(engine.Snapshot) == before;
                Assert.That(EpisodeDirector.VerbTag(kind).Contains(EpisodeDirector.FreeTag), Is.EqualTo(free),
                    kind + (free ? " spends nothing, and its pill must say so." : " spends an action, and its pill must not say it is free."));
            }
            Assert.That(EpisodeDirector.VerbTag(EpisodeCommandKind.SwearLoyalty), Is.EqualTo(EpisodeDirector.BindsYouTag + " · " + EpisodeDirector.FreeTag));
            Assert.That(EpisodeDirector.VerbTag(EpisodeCommandKind.DeclineLoyalty), Is.EqualTo(EpisodeDirector.FreeTag));
        }

        [Test]
        public void TheVerbRowsAreNewCaptionsNoPersonCarries()
        {
            var verbs = new[]
            {
                EpisodeDirector.VentPickerCaption, EpisodeDirector.LiePickerCaption, EpisodeDirector.WhisperPickerCaption,
                EpisodeDirector.CalloutPickerCaption, EpisodeDirector.TargetDealPickerCaption, EpisodeDirector.PromiseToEvictPickerCaption,
            };
            Assert.That(verbs.Distinct().Count(), Is.EqualTo(verbs.Length), "One caption, one verb row.");
            // The cast's names, and the names a player could type that read like the verb rows' own words.
            var names = ContentCatalog.Create(7).contestants.Select(c => c.name)
                .Concat(new[] { "someone", "a nominee", "…", "", "out publicly…" });
            foreach (var name in names)
            {
                var rows = new[]
                {
                    EpisodeDirector.VentCaption(name), EpisodeDirector.LieCaption(name), EpisodeHud.WhisperCaption(name),
                    EpisodeHud.CalloutCaption(name), EpisodeHud.DealProposeCaption("target agreement against " + name),
                    EpisodeDirector.PromiseToEvictCaption(name),
                };
                Assert.That(rows.Intersect(verbs), Is.Empty, "A verb row never carries a person's caption, whatever they are called: '" + name + "'.");
            }
            Assert.That(EpisodeDirector.VentCaption("Jo"), Is.EqualTo("Vent about Jo"), "The people keep the words their rows had.");
            Assert.That(EpisodeDirector.LieCaption("Jo"), Is.EqualTo("Tell them something untrue about Jo"));
            Assert.That(EpisodeDirector.PromiseToEvictCaption("Jo"), Is.EqualTo("Promise to evict Jo"));
            // Every picker's people, word for word, the cards drawn around them since P1 (UI-UX-PASS-PLAN):
            // the name over the caption is decoration, and the caption is still the control's.
            Assert.That(EpisodeHud.WhisperCaption("Jo"), Is.EqualTo("Whisper about Jo"));
            Assert.That(EpisodeHud.CalloutCaption("Jo"), Is.EqualTo("Call Jo out publicly"));
            Assert.That(EpisodeHud.DealProposeCaption(DealKind.Title(DealKind.TargetAgreement).ToLowerInvariant() + " against Jo"),
                Is.EqualTo("Propose a target agreement against Jo"));
        }

        [Test]
        public void TheLoyaltyDeclarationSaysWhatTheEngineDoesWithABreachEitherWay()
        {
            // The copy as written: the words the diary test reads stay, and the "does not bind" claim goes.
            Assert.That(EpisodeDirector.OathOfferLine("Maya Chen"), Does.Contain("not Maya Chen's consent or promise"));
            Assert.That(EpisodeDirector.OathOfferLine("Maya Chen"), Does.Contain("if either of you nominates or votes to evict the other"));
            Assert.That(EpisodeDirector.OathRecordedLine("Maya Chen"), Does.Contain("holds both ways"));
            Assert.That(EpisodeDirector.OathRecordedLine("Maya Chen"), Does.Not.Contain("does not bind"));
            // The notebook says it in the same terms; it said a declaration "is not a mutual guarantee".
            Assert.That(EpisodeDirector.OathNotebookNote, Does.Contain("not a promise from them").And.Contain("holds both ways")
                .And.Contain("by either of you").And.Contain("in front of the house"));

            // What it says: a houseguest who votes out or nominates the player breaks the player's own
            // declaration, in words the house is told (the engine logs the plan with no audience), and
            // every witness with no feeling for either side thinks less of the breaker.
            var snapshot = new WebOathSnapshot { week = 3 };
            snapshot.actors.Add(new WebOathActor { id = "you", name = "You", status = "Active", isPlayer = true });
            foreach (var name in new[] { "Maya", "Jo", "Sam", "Ali" })
                snapshot.actors.Add(new WebOathActor { id = name.ToLowerInvariant(), name = name, status = "Active" });
            snapshot.oaths.Add(new WebOathRecord { playerId = "you", targetId = "maya", week = 2, timestamp = 1 });

            var vote = WebLoyaltyOaths.EvictionVote(snapshot, "maya", "you");
            Assert.That(vote.broken, Is.True, "Maya's vote against the player breaks the player's declaration.");
            Assert.That(vote.actorId, Is.EqualTo("maya"));
            Assert.That(vote.logDescription, Does.StartWith("Maya broke their loyalty oath to You"));
            Assert.That(vote.ripples.Select(r => r.fromId), Is.EquivalentTo(new[] { "jo", "sam", "ali" }), "The house hears it.");
            Assert.That(vote.ripples.All(r => r.delta < 0), Is.True, "and thinks less of the breaker.");

            var rolls = WebLoyaltyOaths.NominationNeutralWitnesses(snapshot, "maya", "you").Select(_ => .5).ToArray();
            var nomination = WebLoyaltyOaths.Nomination(snapshot, "maya", "you", rolls);
            Assert.That(nomination.broken, Is.True, "So does Maya nominating the player.");
            Assert.That(nomination.actorId, Is.EqualTo("maya"));

            var mine = WebLoyaltyOaths.EvictionVote(snapshot, "you", "maya");
            Assert.That(mine.broken, Is.True, "and the player voting Maya out breaks it from the other side.");
        }
    }
}
