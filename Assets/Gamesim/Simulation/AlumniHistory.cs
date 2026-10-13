#if GAMESIM_ALLSTAR_CANDIDATE
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>Private fresh-season candidate. Historical facts never become current-season acts.</summary>
    public static class AlumniHistory
    {
        public const int Version = 1;
        public const string PriorHistory = "prior-history", PriorCareer = "prior-career";
        public static bool On(EpisodeState state) => state != null && state.alumniRulesVersion == Version
            && state.alumniCatalogVersion == AlumniCatalog.Version;

        private static Dictionary<string, ContestantState> Identities(EpisodeState state) =>
            (state?.contestants ?? new List<ContestantState>()).Where(StoryPeople.IsRealPerson)
                .GroupBy(who => StoryPeople.TemplateOf(who).Id, StringComparer.Ordinal)
                .Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

        public static bool TryProfile(EpisodeState state, string id, out AlumniCatalog.Profile profile)
        {
            profile = null;
            if (!On(state) || state.Find(id) == null) return false;
            var identities = Identities(state);
            var person = state.Find(id);
            var template = StoryPeople.TemplateOf(person);
            return template != null && identities.TryGetValue(template.Id, out var unique) && unique.id == id
                && AlumniCatalog.TryGet(template.Id, out profile);
        }

        public static void InitializeFresh(EpisodeState state, SeasonBuilder.Choice choice)
        {
            if (state == null || choice == null) throw new ArgumentNullException(state == null ? nameof(state) : nameof(choice));
            if (state.week != 1 || state.phase != EpisodePhase.Social || state.revision != 0
                || state.acceptedCommandIds.Count != 0 || state.alumniRulesVersion != 0
                || state.story.facts.Any(f => f.kind == PriorHistory || f.kind == PriorCareer))
                throw new ArgumentException("Alumni rules initialize once, only on a fresh unplayed season.");
            state.alumniRulesVersion = Version;
            state.alumniCatalogVersion = AlumniCatalog.Version;
            state.playerHistoriesEnabled = choice.PlayerHistories;
            var identities = Identities(state);
            foreach (var profile in AlumniCatalog.All.OrderBy(p => p.TemplateId, StringComparer.Ordinal))
                if (identities.TryGetValue(profile.TemplateId, out var person))
                    Knowledge.SeedAlumniFact(state, PriorCareer, person.id, null, profile.TemplateId);
            if (!choice.PlayerHistories) return;
            foreach (var history in AlumniCatalog.PairHistories.OrderBy(p => p.FactId, StringComparer.Ordinal))
            {
                if (!identities.TryGetValue(history.LeftTemplateId, out var left)
                    || !identities.TryGetValue(history.RightTemplateId, out var right)) continue;
                Seed(state, left.id, right.id, history.LeftToRight);
                Seed(state, right.id, left.id, history.RightToLeft);
                Knowledge.SeedAlumniFact(state, PriorHistory, left.id, right.id, history.FactId);
            }
            if (identities.TryGetValue("derrick-levasseur", out var derrick)
                && identities.TryGetValue("cody-calafiore", out var cody))
            {
                if (!derrick.isPlayer && !cody.isPlayer) NpcDeals.SeedOpeningPair(state, derrick.id, cody.id);
                else NpcDeals.SeedOpeningProposal(state, derrick.isPlayer ? cody.id : derrick.id);
            }
        }

        private static void Seed(EpisodeState state, string from, string to, AlumniCatalog.DirectedPrior prior)
        {
            var edge = state.relationships.Single(r => r.fromId == from && r.toId == to);
            edge.score = prior.Relationship;
            edge.priorTrustOffset = prior.PriorTrustOffset;
        }

        public static string SubjectId(EpisodeState state, AlumniCatalog.PairHistory history, string npcId)
        {
            if (history == null || !TryProfile(state, npcId, out var profile)) return null;
            string other = profile.TemplateId == history.LeftTemplateId ? history.RightTemplateId
                : profile.TemplateId == history.RightTemplateId ? history.LeftTemplateId : null;
            return other != null && Identities(state).TryGetValue(other, out var person) ? person.id : null;
        }

        public static IReadOnlyList<AlumniCatalog.PairHistory> DiscussableFacts(EpisodeState state, string npcId)
        {
            if (!On(state) || !state.playerHistoriesEnabled || state.Find(npcId)?.status != ContestantStatus.Active
                || state.Find(npcId).isPlayer) return Array.Empty<AlumniCatalog.PairHistory>();
            return Array.AsReadOnly(AlumniCatalog.PairHistories.Where(history =>
                SubjectId(state, history, npcId) is string other && state.Find(other).status == ContestantStatus.Active
                && state.story.facts.Any(f => f.kind == PriorHistory && f.refId == history.FactId
                    && Knowledge.Knows(f, state.playerId) && Knowledge.Knows(f, npcId))).ToArray());
        }

        public static bool CanDiscuss(EpisodeState state, EpisodeCommand command, ContestantState target, out string error)
        {
            error = "Choose a known past connection and a valid conversation approach.";
            if (command == null || command.kind != EpisodeCommandKind.AskForIntel || target == null
                || target.id != command.targetId || target.isPlayer || command.actorId != state?.playerId) return false;
            var parts = (command.text ?? "").Split(':');
            if (parts.Length != 3 || parts[0] != "history"
                || (parts[2] != "curious" && parts[2] != "respectful" && parts[2] != "challenge")) return false;
            var history = DiscussableFacts(state, target.id).FirstOrDefault(p => p.FactId == parts[1]);
            if (history == null || SubjectId(state, history, target.id) != command.secondTargetId) return false;
            error = null; return true;
        }

        private static string PlayerWords(EpisodeState state, EpisodeCommand command)
        {
            bool yours = command.secondTargetId == state.playerId;
            switch (command.text.Split(':')[2])
            {
                case "curious": return yours ? "How do you feel about us playing again?" : "How do you feel about them now?";
                case "respectful": return yours ? "We both know how this game can go." : "I know you have history together.";
                default: return "Why should this time be any different?";
            }
        }

        internal static string RequestRef(EpisodeCommand command) => command.id + "\n" + command.targetId
            + "\n" + command.secondTargetId + "\n" + command.text;

        public static void Discuss(EpisodeState state, EpisodeCommand command, ContestantState target)
        {
            if (!CanDiscuss(state, command, target, out string error)) throw new ArgumentException(error);
            string approach = command.text.Split(':')[2];
            TryProfile(state, target.id, out var profile);
            double impact = approach == "respectful" ? 3 : approach == "curious" ? 2
                : profile.TemplateId == "dr-will-kirby" ? 0
                : target.traits.Contains("Analytical") || target.traits.Contains("Competitive")
                    ? (state.Score(target.id, state.playerId) >= 0 ? 2 : -3) : -3;
            if (impact != 0) EpisodeEngine.Change(state, state.playerId, target.id, impact,
                "We talked about our past games.", "history-discussed");
            var history = AlumniCatalog.PairHistories.Single(p => p.FactId == command.text.Split(':')[1]);
            string reply = Reply(state, target.id, command.secondTargetId, history, approach);
            EpisodeEngine.Log(state, "history-conversation", PlayerWords(state, command) + "\n" + reply,
                state.playerId, target.id);
            state.events.Last().historyCommandRef = RequestRef(command);
        }

        // Authored game dialogue. None of these lines asserts a secret current-season agreement.
        private static string Reply(EpisodeState state, string speaker, string other, AlumniCatalog.PairHistory history, string approach)
        {
            TryProfile(state, speaker, out var profile);
            string name = other == state.playerId ? "you" : CharacterNames.FirstName(state.Find(other).name);
            switch (history.FactId)
            {
                case "will-janelle-bb7":
                    if (profile.TemplateId == "dr-will-kirby") return approach == "challenge"
                        ? "You think I'm going to let " + (other == state.playerId ? "you" : "Janelle")
                            + " evict me at final four again? I'm very easy to underestimate."
                        : other == state.playerId ? "We've had a lot of fun together. I remember that final-four decision, too. Let's give ourselves a reason to keep talking."
                        : "Janelle is fun to be around. That does not mean I'm handing Janelle another decision at final four.";
                    return other == state.playerId ? "You can make almost anything sound good. I can enjoy your company and still check what you are promising."
                        : "Will can make almost anything sound good. I can enjoy his company and still check what he is promising.";
                case "hitmen-bb16": return other == state.playerId
                    ? "We know what we did together. What we agree to this season is a conversation for the two of us."
                    : "Cody and Derrick worked together before. Everybody knows that. This season still has to be played.";
                case "janelle-dan-traitors": return profile.TemplateId == "janelle-pierzina"
                    ? (other == state.playerId ? "I like you. But if I give information, I need something back. Friendship does not make a one-sided game work."
                        : "I like Dan. But if I give information, I need something back. Friendship does not make a one-sided game work.")
                    : (other == state.playerId ? "You know when somebody stops giving you anything useful. I have to earn my place in this house too."
                        : "Janelle knows when somebody stops giving Janelle anything useful. I have to earn my place in this house too.");
                case "janelle-rachel-snake": return "We are friends, and people will notice that. Friendship does not decide every move for us.";
                case "janelle-cody-bb22":
                case "janelle-tyler-bb22": return profile.TemplateId == "janelle-pierzina"
                    ? "I offered to be a shield before. Keeping somebody around only works if the protection goes both ways."
                    : "We were on different sides then. I have to look at where the numbers are in this house.";
                case "rachel-cody-traitors": return profile.TemplateId == "rachel-reilly"
                    ? (other == state.playerId ? "I know how you usually talk to me. When that changes, I pay attention."
                        : "I know how Cody usually talks to me. When that changes, I pay attention.")
                    : (other == state.playerId ? "You notice when somebody acts differently. I would rather be clear about what I am actually offering."
                        : "Rachel notices when somebody acts differently. I would rather be clear about what I am actually offering.");
                case "cody-xavier-reindeer": return "I have competed with " + name + " before. I still need to see what protection means this time.";
                case "danielle-janelle-bb7": return "We have been opponents before. That does not mean we cannot find a useful reason to work together now.";
                case "will-danielle-bb7": return "We have worked together, and we know what the other can do. I still read the terms carefully.";
                case "cody-tyler-bb22": return "We worked together before. You can respect somebody and still know that they might cut you at the end.";
                default: return "I have played with " + name + ". There is some history there, but I still need to see what happens in this house.";
            }
        }

        public static bool TryReceipt(EpisodeState state, EpisodeCommand command, out string playerLine, out string npcLine)
        {
            playerLine = npcLine = null;
            if (state == null || command == null || command.kind != EpisodeCommandKind.AskForIntel
                || command.actorId != state.playerId || !state.acceptedCommandIds.Contains(command.id)) return false;
            var matches = state.events.Where(e => e.kind == "history-conversation" && e.historyCommandRef == RequestRef(command)
                && e.audienceIds.Contains(state.playerId) && e.audienceIds.Contains(command.targetId)).ToArray();
            if (matches.Length != 1) return false;
            var words = matches[0].text.Split('\n');
            if (words.Length != 2 || string.IsNullOrWhiteSpace(words[0]) || string.IsNullOrWhiteSpace(words[1])) return false;
            playerLine = words[0]; npcLine = words[1]; return true;
        }

        internal static void InvalidatePositivePrior(EpisodeState state, string observer, string actor)
        {
            if (!On(state) || observer == null || actor == null || observer == actor) return;
            var edge = state.relationships.FirstOrDefault(r => r.fromId == observer && r.toId == actor);
            if (edge?.priorTrustOffset > 0) edge.priorTrustOffset = 0;
        }

        public static bool TryValidateState(EpisodeState state, out string error)
        {
            error = null;
            var facts = state.story?.facts ?? new List<HouseFactState>();
            if (state.alumniRulesVersion == 0)
                return (!state.playerHistoriesEnabled && string.IsNullOrEmpty(state.alumniCatalogVersion)
                    && !facts.Any(f => f.kind == PriorHistory || f.kind == PriorCareer))
                    || Refuse(out error, "Inactive alumni rules cannot hold alumni provenance.");
            if (!On(state)) return Refuse(out error, "Unsupported alumni rules or catalogue edition.");
            foreach (var edge in state.relationships.Where(r => r.priorTrustOffset != 0))
            {
                if (!state.playerHistoriesEnabled || !TryProfile(state, edge.fromId, out var from)
                    || !TryProfile(state, edge.toId, out var to)) return Refuse(out error, "Inherited trust has no unique alumni provenance.");
                var prior = AlumniCatalog.PairHistories.SelectMany(p => new[] { p.LeftToRight, p.RightToLeft })
                    .FirstOrDefault(p => p.FromTemplateId == from.TemplateId && p.ToTemplateId == to.TemplateId);
                if (prior == null || edge.priorTrustOffset != prior.PriorTrustOffset)
                    return Refuse(out error, "Inherited trust does not match its immutable catalogue provenance.");
            }
            foreach (var fact in facts.Where(f => f.kind == PriorHistory || f.kind == PriorCareer))
            {
                if (fact.week != 1 || fact.visibility != FactVisibility.Public
                    || !TryProfile(state, fact.actorId, out var actor)) return Refuse(out error, "Invalid public alumni provenance.");
                if (fact.kind == PriorCareer)
                { if (fact.refId != actor.TemplateId || fact.subjectId != null) return Refuse(out error, "Invalid career provenance."); }
                else
                {
                    if (!state.playerHistoriesEnabled || !TryProfile(state, fact.subjectId, out var subject))
                        return Refuse(out error, "Interpersonal history requires two unique alumni and enabled histories.");
                    var pair = AlumniCatalog.PairHistories.FirstOrDefault(p => p.FactId == fact.refId);
                    if (pair == null || pair.LeftTemplateId != actor.TemplateId || pair.RightTemplateId != subject.TemplateId)
                        return Refuse(out error, "Historical pair does not match its source.");
                }
            }
            foreach (var item in state.events)
                if (item.historyCommandRef != null && (item.kind != "history-conversation" || item.historyCommandRef.Length > 1200
                    || item.historyCommandRef.Split('\n').Length != 4)) return Refuse(out error, "Invalid historical conversation request provenance.");
            return true;
        }
        private static bool Refuse(out string error, string text) { error = text; return false; }
    }
}
#endif
