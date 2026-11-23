using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk's romance (talk rules 3, slice S3): the Flirt answer and its scenes, the choices and their continuations, the
    /// couple as the existing showmance bond and Couple fact, endings, jealousy, the house's reaction at the anchors, and a
    /// partner the player nominates. Every view change is an act through <see cref="ChangeOnStream"/> on a keyed story stream,
    /// arcless; every coin a keyed story coin: nothing here draws from the season's stream. Below talk rules 3 none of it runs.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>The arc a couple formed in free talk starts at its second beat (the spec's fallback: its beats run on the thread).</summary>
        internal const string LateNightsArc = "late-nights", LateNightsSecondBeat = "noticed";

        /// <summary>What a JustTalk command would do at talk rules 3, played on a copy of the state: null when refused.</summary>
        internal static TalkResult PreviewRomanceTalk(EpisodeState s, string npcId, string text)
        {
            if (s == null || !TalkRomance.LevelOn(s)) return null;
            var copy = s.Clone();
            try { return RomanceTalk(copy, npcId, text); }
            catch (RuleException) { return null; }
        }

        /// <summary>One JustTalk command at talk rules 3: a continuation, a choice, a Flirt, or a tone on the talk on offer.</summary>
        private static TalkResult RomanceTalk(EpisodeState s, string npcId, string text)
        {
            Require(TalkRomance.TryParse(text, out string talkId, out string answer, out string seenBy), FreeTalk.NotOnOfferReason);
            var choice = TalkChoices.FromAnswer(answer);
            Require(choice != TalkChoice.None || TalkRules.IsTone(answer), FreeTalk.ToneReason);
            if (seenBy != null)
                Require(seenBy != npcId && s.Find(seenBy) is ContestantState seen && !seen.isPlayer && seen.status == ContestantStatus.Active,
                    FreeTalk.NotOnOfferReason);

            // A continuation: a choice on the scene left open with them this window. Not counted.
            var open = FreeTalk.Contact(s, npcId);
            int key = FreeTalk.WindowKey(s);
            if (choice != TalkChoice.None && key != 0 && open != null && open.talkWindow == key && open.talkPending.Length > 0
                && string.Equals(open.talkPending, talkId, StringComparison.Ordinal))
                return Continue(s, npcId, open, choice, seenBy);

            var offer = FreeTalk.Offer(s, npcId);
            Require(offer.Available, offer.Reason);
            Require(string.Equals(talkId, offer.Talk.Id, StringComparison.Ordinal), FreeTalk.NotOnOfferReason);
            var talk = offer.Talk;
            var npc = s.Find(npcId);
            bool romanceRow = TalkRomance.IsRomanceKind(talk.Kind);
            TalkRow flirtRow = null;
            TalkResult result;
            if (choice != TalkChoice.None)
            {
                Require(romanceRow && TalkRomance.ChoicesNow(s, npcId, talk).Contains(choice), FreeTalk.ToneReason);
                result = ChoiceAnswer(s, npcId, offer, choice);
            }
            else if (answer == TalkRules.Flirt && !romanceRow)
            {
                Require(TalkRomance.FlirtOffered(s, npcId, talk), FreeTalk.ToneReason);
                flirtRow = TalkRomance.FlirtLine(s, npcId);
                result = FreeTalk.Answer(s, npcId, offer, flirtRow.Replies[0]);
                result.TalkId = talk.Id;
                // A Flirt on a stage talk plays the romance, not the stage: the stage talk stays due, with no event row.
                if (talk.Kind == TalkKind.Stage) { result.StageReached = FriendStage.Stranger; result.StageEvent = null; result.Favour = TalkFavour.None; }
                // A Flirt answered in kind at a good landing warms both a little more (R5's stop at 25 has already cut a gain to 0).
                if (result.Warmth == 1 && TalkRomance.SceneFor(s, npcId) == RomanceScene.InKind) result.Warmth = TalkRomance.InKindWarmth;
            }
            else
            {
                var reply = talk.Replies.FirstOrDefault(r => r != null && r.Choice == TalkChoice.None && r.Tone == answer);
                Require(reply != null, FreeTalk.ToneReason);
                if (answer == TalkRules.Flirt)
                {
                    Require(talk.Kind == TalkKind.Opening && TalkRomance.FlirtOpen(s, npcId), FreeTalk.ToneReason);
                    flirtRow = talk;
                }
                result = FreeTalk.Answer(s, npcId, offer, reply);
            }

            var before = FreeTalk.Contact(s, npcId);
            result.SparkBefore = before?.spark ?? 0;
            result.RomanceBefore = before?.romance ?? "";
            bool spend = !romanceRow && !(flirtRow != null && talk.Kind == TalkKind.Stage) || talk.Kind == TalkKind.Opening || talk.Kind == TalkKind.Reaction;
            var contact = ApplyCountedTalk(s, npc, result, choice == TalkChoice.None ? answer : null, spend);
            // A pushed turn-aside's cool: this counted talk was one of the talks it lowered (read in FreeTalk.LandingNow).
            if (contact.cool > 0) contact.cool--;
            // A romance talk's one-off flags are spent with it.
            if (talk.Kind == TalkKind.Jealousy) contact.talkFlags.Remove(TalkRules.Jealous);
            if (talk.Kind == TalkKind.Reaction) contact.talkFlags.Remove(TalkRules.CoupleTalk);
            if (talk.Kind == TalkKind.Ex) contact.talkFlags.Remove(TalkRules.Ex);

            if (flirtRow != null) Flirted(s, npc, contact, flirtRow, result);
            else if (choice != TalkChoice.None) Chose(s, npc, contact, talk, choice, result, seenBy);
            if (seenBy != null && contact.romance == TalkRules.Couple && MakePublic(s, npc.id)) result.WentPublic = true;
            result.SparkAfter = contact.spark;
            result.RomanceAfter = contact.romance;
            return result;
        }

        /// <summary>A choice answered as the counted talk (their declaration, their opening, a couple's talk): a neutral talk's rapport, no warmth, no tone.</summary>
        private static TalkResult ChoiceAnswer(EpisodeState s, string npcId, TalkOffer offer, TalkChoice choice)
        {
            var npc = s.Find(npcId);
            var contact = FreeTalk.Contact(s, npcId);
            var reply = offer.Talk.Replies.FirstOrDefault(r => r != null && r.Choice == choice);
            int tenths = FreeTalk.Tenths(0, false, FreeTalk.ClassOf(npc), TalkChemistry.Band(s, npcId));
            int rapportBefore = contact?.rapport ?? 0, tenthsBefore = contact?.talkTenths ?? 0, total = tenthsBefore + tenths;
            return new TalkResult
            {
                NpcId = npcId, TalkId = offer.Talk.Id, Choice = choice, Landing = 0,
                Reply = TalkText.Fill(s, npcId, reply?.Line ?? TalkChoices.Caption(choice)),
                Reaction = TalkText.Fill(s, npcId, reply?.Lands ?? ""),
                Tenths = tenths, RapportBefore = rapportBefore, TenthsBefore = tenthsBefore,
                RapportAfter = Math.Min(100, rapportBefore + total / 10), TenthsAfter = total % 10,
                AgreementAfter = contact?.agreement ?? 0, LeanBefore = s.story.talkLean, LeanAfter = s.story.talkLean,
            };
        }

        /// <summary>A choice on the scene left open this window (<c>contact.talkPending</c>): not counted, no gain beyond the choice's own.</summary>
        private static TalkResult Continue(EpisodeState s, string npcId, ContactState contact, TalkChoice choice, string seenBy)
        {
            var npc = s.Find(npcId);
            Require(s.Find(s.playerId)?.status == ContestantStatus.Active && npc != null && !npc.isPlayer && npc.status == ContestantStatus.Active,
                FreeTalk.NotInHouseReason);
            var row = TalkBank.Find(contact.talkPending);
            Require(row != null && TalkRomance.ChoicesNow(s, npcId, row).Contains(choice), FreeTalk.ToneReason);
            var reply = row.Replies?.FirstOrDefault(r => r != null && r.Choice == choice);
            var result = new TalkResult
            {
                NpcId = npcId, TalkId = row.Id, Choice = choice, Continuation = true,
                Reply = TalkText.Fill(s, npcId, reply?.Line ?? TalkChoices.Caption(choice)),
                Reaction = TalkText.Fill(s, npcId, reply?.Lands ?? ""),
                RapportBefore = contact.rapport, TenthsBefore = contact.talkTenths, RapportAfter = contact.rapport, TenthsAfter = contact.talkTenths,
                AgreementAfter = contact.agreement, LeanBefore = s.story.talkLean, LeanAfter = s.story.talkLean,
                SparkBefore = contact.spark, RomanceBefore = contact.romance,
            };
            contact.talkPending = "";
            Chose(s, npc, contact, row, choice, result, seenBy);
            if (seenBy != null && contact.romance == TalkRules.Couple && MakePublic(s, npc.id)) result.WentPublic = true;
            result.RapportAfter = contact.rapport;
            result.SparkAfter = contact.spark;
            result.RomanceAfter = contact.romance;
            return result;
        }

        /// <summary>The player's Flirt (U2, U3, U6, U7): the scene by the housemate's stance and interest, R8's Just friends lifted.</summary>
        private static void Flirted(EpisodeState s, ContestantState npc, ContactState contact, TalkRow flirtRow, TalkResult result)
        {
            bool lifted = contact.romance == TalkRules.Friends;
            if (lifted) contact.romance = "";
            var scene = TalkRomance.SceneFor(s, npc.id);
            contact.flirts = Math.Min(TalkRules.MostCount, contact.flirts + 1);
            MayHear(s, npc.id);
            result.Scene = scene;
            TalkRow row = null;
            switch (scene)
            {
                case RomanceScene.InKind:
                    contact.spark = Math.Min(TalkRomance.MostSpark, contact.spark + TalkRomance.SparkStep(s, npc.id, result.Landing));
                    if (contact.romance != TalkRules.Flirting) { contact.romance = TalkRules.Flirting; contact.romanceWeek = s.week; }
                    result.SceneTalkId = flirtRow.Id;
                    result.Choices = TalkRomance.ChoicesNow(s, npc.id, flirtRow);
                    if (result.Choices.Count > 0) contact.talkPending = flirtRow.Id;
                    result.Change = lifted ? RomanceChange.FriendsLifted : RomanceChange.Spark;
                    return;
                case RomanceScene.SoftNo:
                    contact.spark = 0;
                    contact.romance = TalkRules.Closed;
                    contact.romanceClosedUntil = Math.Min(TalkRules.ForTheSeason, s.week + TalkRomance.SoftNoWeeks);
                    contact.romanceWeek = s.week;
                    row = TalkRomance.SceneRow(s, npc.id, TalkKind.SoftNo);
                    result.Change = RomanceChange.SoftNo;
                    break;
                case RomanceScene.HoldBack:
                    row = TalkRomance.SceneRow(s, npc.id, TalkKind.HoldBack);
                    result.Change = RomanceChange.HoldBack;
                    break;
                case RomanceScene.LineDrawn:
                    AddFlag(contact, TalkRules.FlirtRemoved);
                    row = TalkRomance.SceneRow(s, npc.id, TalkKind.LineDrawn);
                    result.Change = RomanceChange.LineDrawn;
                    break;
                case RomanceScene.TurnAside:
                    contact.turnAsides = Math.Min(3, contact.turnAsides + 1);
                    result.Attempt = contact.turnAsides;
                    row = TalkRomance.SceneRow(s, npc.id, TalkKind.TurnAside, result.Attempt);
                    if (result.Attempt == 2 && contact.talkFlags.Contains(TalkRules.Pushy)) result.ActWarmth = Act(s, s.playerId, npc.id, -1, "turn2");
                    if (result.Attempt >= 3) AddFlag(contact, TalkRules.FlirtRemoved);
                    if (result.Attempt == 1 && row != null)
                    {
                        result.SceneSecond = TalkText.Fill(s, npc.id, row.Second ?? "");
                        result.Choices = TalkRomance.ChoicesNow(s, npc.id, row);
                        contact.talkPending = row.Id;
                    }
                    result.Change = RomanceChange.TurnAside;
                    break;
            }
            // Their in-kind line belongs to an interested answer only: every other scene says its own.
            result.Reaction = null;
            if (row != null) { result.SceneTalkId = row.Id; result.SceneLine = TalkText.Fill(s, npc.id, row.Open ?? ""); }
        }

        /// <summary>A choice's effects (U3, U6, R8), on the counted talk or a continuation.</summary>
        private static void Chose(EpisodeState s, ContestantState npc, ContactState contact, TalkRow row, TalkChoice choice, TalkResult result, string seenBy)
        {
            result.Choice = choice;
            switch (choice)
            {
                case TalkChoice.MakeItOfficial:
                {
                    Require(TalkRomance.Ready(s, npc.id), FreeTalk.ToneReason);
                    string other = TalkRomance.PartnerOf(s);
                    if (other != null && other != npc.id) { SecondDeclaration(s, npc, contact, other, result); return; }
                    if (TalkRomance.Interested(s, npc.id))
                    {
                        FormCouple(s, npc, contact, result);
                        Scene(s, npc.id, result, RomanceScene.Accepts, TalkKind.Accepts);
                    }
                    else
                    {
                        CloseFor(s, contact, TalkRomance.NotLikeThatWeeks);
                        Scene(s, npc.id, result, RomanceScene.Refuses, TalkKind.Refuses);
                        result.Change = RomanceChange.NotLikeThat;
                    }
                    return;
                }
                case TalkChoice.Accept:
                    FormCouple(s, npc, contact, result);
                    return;
                case TalkChoice.NotYet:
                    contact.notYets = Math.Min(3, contact.notYets + 1);
                    contact.romanceWeek = s.week;
                    result.Change = RomanceChange.NotYet;
                    return;
                case TalkChoice.NotLikeThat:
                {
                    CloseFor(s, contact, TalkRomance.NotLikeThatWeeks);
                    string cls = FreeTalk.ClassOf(npc);
                    int act = cls == TalkPools.Steady ? -2 : cls == TalkPools.Guarded ? 1 : 0;
                    if (act != 0) result.ActWarmth = Act(s, s.playerId, npc.id, act, "notlike");
                    result.Change = RomanceChange.NotLikeThat;
                    return;
                }
                case TalkChoice.JustFriends:
                    contact.romance = TalkRules.Friends;
                    contact.romanceWeek = s.week;
                    contact.talkPending = "";
                    result.Change = RomanceChange.JustFriends;
                    return;
                case TalkChoice.EndItKindly:
                    result.ActWarmth = Act(s, s.playerId, npc.id, TalkRomance.EndKindly, "end");
                    EndCouple(s, npc, contact, false, "You and " + npc.name + " ended it kindly.");
                    contact.talkFlags.Remove(TalkRules.Jealous);
                    result.Change = RomanceChange.Ended;
                    return;
                case TalkChoice.LaughItOff:
                    result.ActWarmth = Act(s, s.playerId, npc.id, 2, "laugh");
                    contact.rapport = Math.Min(100, contact.rapport + 1);
                    AddFlag(contact, TalkRules.RunningJoke);
                    result.Change = RomanceChange.TurnAsideAnswered;
                    return;
                case TalkChoice.PushAgain:
                    result.ActWarmth = Act(s, s.playerId, npc.id, -3, "push");
                    contact.cool = 2;
                    AddFlag(contact, TalkRules.Pushy);
                    result.Change = RomanceChange.TurnAsideAnswered;
                    return;
                case TalkChoice.Apologize:
                    contact.rapport = Math.Min(100, contact.rapport + 1);
                    AddFlag(contact, TalkRules.TookTheNoWell);
                    result.Change = RomanceChange.TurnAsideAnswered;
                    return;
            }
        }

        private static void Scene(EpisodeState s, string npcId, TalkResult result, RomanceScene scene, TalkKind kind)
        {
            result.Scene = scene;
            var row = TalkRomance.SceneRow(s, npcId, kind);
            if (row == null) return;
            result.SceneTalkId = row.Id;
            result.SceneLine = TalkText.Fill(s, npcId, row.Open ?? "");
        }

        private static void CloseFor(EpisodeState s, ContactState contact, int weeks)
        {
            contact.romance = TalkRules.Closed;
            contact.romanceClosedUntil = Math.Max(contact.romanceClosedUntil, Math.Min(TalkRules.ForTheSeason, s.week + weeks));
            contact.romanceWeek = s.week;
            contact.talkPending = "";
        }

        /// <summary>
        /// A couple (U4): the existing showmance bond, private, and the existing Couple fact, whispered; Late Nights' thread from its
        /// second beat where the arc may run. No view moves: the couple is the bond.
        /// </summary>
        private static void FormCouple(EpisodeState s, ContestantState npc, ContactState contact, TalkResult result)
        {
            Require(!TalkRomance.CoupleBarred(s, npc.id), FreeTalk.NotOnOfferReason);
            var bond = Bonds.Form(s, s.playerId, npc.id, BondKinds.Showmance, BondStatus.Private, null);
            Require(bond != null, FreeTalk.NotOnOfferReason);
            Knowledge.Create(s, FactKinds.Couple, s.playerId, npc.id, FactVisibility.Whispered, null);
            contact.romance = TalkRules.Couple;
            contact.romanceWeek = s.week;
            contact.coupleAnchors = 0;
            contact.notYets = 0;
            contact.talkPending = "";
            result.Change = RomanceChange.Couple;
            result.RomanceEvent = "You and " + npc.name + " are a couple now.";
            Log(s, TalkRomance.EventKind, result.RomanceEvent, s.playerId, npc.id);
            StartLateNights(s, npc.id);
        }

        /// <summary>
        /// Late Nights' thread for a couple free talk made (the spec's fallback, U4): its later beats key on the running arc, not
        /// the bond, so the arc starts at its second beat. Only where the arc itself may run: the player and the partner allowed a
        /// romance story and neither an All-Stars template, no Late Nights running, the personal lane free; otherwise the couple
        /// stands without it.
        /// </summary>
        private static void StartLateNights(EpisodeState s, string partnerId)
        {
            var template = StoryCatalog.Find(LateNightsArc);
            if (template == null || !StoryOn(s) || template.rulesVersion > s.story.rulesVersion || template.Beat(LateNightsSecondBeat) == null) return;
            if (!StoryPeople.PlayerAllowed(s, StoryPeople.Sensitivity.Romance) || !StoryPeople.Allowed(s, partnerId, StoryPeople.Sensitivity.Romance)) return;
            // An All-Stars template on either side, whatever the card is named (R7): the couple stands on the bond alone.
            if (FreeTalk.IsAllStar(s.Find(s.playerId)) || FreeTalk.IsAllStar(s.Find(partnerId))) return;
            if (s.storylines.Any(x => x.templateId == template.id && StorylineStatus.Running(x.status))) return;
            if (RunningCycles(s).Any(x => x.lane == template.lane && !LeavesTheLaneOpen(s, x))) return;
            string gossip = s.Active.Where(x => !x.isPlayer && x.id != partnerId && !s.Allied(s.playerId, x.id))
                .OrderByDescending(x => Personality.Of(x).Sociable).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault()?.id;
            var binding = new ArcBinding().With("PARTNER", partnerId).With("GOSSIP", gossip).Headlining(partnerId);
            StartCycle(s, template, binding, CurrentAnchor(s) ?? StoryAnchors.EvictionNight, LateNightsSecondBeat);
        }

        /// <summary>A couple ends: the bond ends (a betrayal when <paramref name="betrayed"/>), romance with them closed for the season, an ex talk due.</summary>
        private static void EndCouple(EpisodeState s, ContestantState npc, ContactState contact, bool betrayed, string line)
        {
            Bonds.End(s, s.playerId, npc.id, BondKinds.Showmance, betrayed);
            contact.romance = TalkRules.Ended;
            contact.romanceClosedUntil = TalkRules.ForTheSeason;
            contact.romanceWeek = s.week;
            contact.talkPending = "";
            if (npc.status == ContestantStatus.Active) AddFlag(contact, TalkRules.Ex);
            if (line != null) Log(s, TalkRomance.EventKind, line, s.playerId, npc.id);
        }

        /// <summary>
        /// A second declaration while a couple stands (spec 3.9): both end it, -10 each on their view of the player, and a
        /// permanent ledger event with each.
        /// </summary>
        private static void SecondDeclaration(EpisodeState s, ContestantState npc, ContactState contact, string partnerId, TalkResult result)
        {
            var partner = s.Find(partnerId);
            var partnerContact = FreeTalk.Contact(s, partnerId);
            foreach (var id in new[] { partnerId, npc.id })
            {
                Act(s, id, s.playerId, TalkRomance.SecondDeclarationCost, "second");
                RelationshipLedger.Record(s, s.playerId, id, TalkRomance.SecondDeclarationEvent, TalkRomance.SecondDeclarationCost,
                    "Declared to two housemates at once.", permanent: true);
            }
            if (partner != null && partnerContact != null) EndCouple(s, partner, partnerContact, true, null);
            contact.romance = TalkRules.Ended;
            contact.romanceClosedUntil = TalkRules.ForTheSeason;
            contact.romanceWeek = s.week;
            contact.talkPending = "";
            result.ActWarmth = TalkRomance.SecondDeclarationCost;
            result.Change = RomanceChange.SecondDeclaration;
            result.RomanceEvent = "You declared to " + npc.name + " while with " + (partner?.name ?? "someone") + ". Both walked away.";
            Log(s, TalkRomance.EventKind, result.RomanceEvent, s.playerId, npc.id);
        }

        /// <summary>A Flirt with one housemate may get back to a partner, or to anyone the player has a spark of 20 or more with (may-hear: their coin at the next anchor).</summary>
        private static void MayHear(EpisodeState s, string flirtedWith)
        {
            foreach (var c in s.story.contacts)
            {
                if (c == null || c.npcId == flirtedWith || s.Find(c.npcId)?.status != ContestantStatus.Active) continue;
                if (c.romance == TalkRules.Couple || c.romance == TalkRules.Flirting && c.spark >= TalkRomance.JealousSpark) AddFlag(c, TalkRules.MayHear);
            }
        }

        /// <summary>The couple's fact goes public (U8): through the Knowledge path "Everyone's Talking" uses; two third housemates get a reaction talk. False when it already was, or there is none.</summary>
        private static bool MakePublic(EpisodeState s, string partnerId)
        {
            var fact = Knowledge.Of(s, FactKinds.Couple, StoryConsumers.CoupleRef(s, s.playerId, partnerId));
            if (fact == null || fact.visibility == FactVisibility.Public) return false;
            Knowledge.Create(s, FactKinds.Couple, s.playerId, partnerId, FactVisibility.Public, null);
            foreach (var third in s.Active.Where(x => !x.isPlayer && x.id != partnerId)
                         .OrderByDescending(x => Personality.Of(x).Sociable).ThenBy(x => x.id, StringComparer.Ordinal).Take(2).ToList())
            {
                var contact = FreeTalk.Contact(s, third.id);
                if (contact == null)
                {
                    if (s.story.contacts.Count >= s.contestants.Count) continue;
                    contact = new ContactState { npcId = third.id };
                    s.story.contacts.Add(contact);
                }
                AddFlag(contact, TalkRules.CoupleTalk);
            }
            return true;
        }

        private static void AddFlag(ContactState contact, string flag)
        {
            if (!contact.talkFlags.Contains(flag) && contact.talkFlags.Count < TalkRules.MostFlags) contact.talkFlags.Add(flag);
        }

        /// <summary>An act on a keyed story stream, arcless (U3): <paramref name="from"/>'s view of <paramref name="to"/>, the other side 0.8 to 1.2 of it.</summary>
        private static int Act(EpisodeState s, string from, string to, int delta, string purpose)
        {
            ChangeOnStream(s, from, to, delta,
                StoryRandom.Stream(s, "talk:v1:act:" + purpose + ":" + from + ":" + to + ":" + s.week + ":" + FreeTalk.WindowKey(s)), arcless: true);
            return delta;
        }

        /// <summary>
        /// The romance at an anchor (talk rules 3): a scene left open lapses; a may-hear takes its coin (jealousy, -5 and a talk);
        /// a couple whose bond is gone ends; a partner whose view of the player is under 10 ends it (-8); a couple goes public after
        /// three anchors; an ex talk lapses after two weeks.
        /// </summary>
        private static void RomanceAtAnchor(EpisodeState s, string anchor)
        {
            if (!TalkRomance.LevelOn(s) || anchor == StoryAnchors.Conversation) return;
            int key = FreeTalk.WindowKey(s);
            foreach (var c in s.story.contacts)
                if (c != null && c.talkPending.Length > 0 && c.talkWindow != key) c.talkPending = "";
            if (!TalkRomance.On(s) || s.Find(s.playerId)?.status != ContestantStatus.Active) return;
            foreach (var c in s.story.contacts.ToList())
            {
                if (c == null) continue;
                var npc = s.Find(c.npcId);
                bool active = npc != null && npc.status == ContestantStatus.Active;
                if (c.talkFlags.Remove(TalkRules.MayHear) && active
                    && StoryRandom.Unit(s, "talk:v1:jealous:" + c.npcId + ":" + s.week + ":" + anchor) < TalkRomance.JealousyChance)
                {
                    Act(s, c.npcId, s.playerId, TalkRomance.JealousyCost, "jealous");
                    AddFlag(c, TalkRules.Jealous);
                }
                if (!active) { c.talkFlags.Remove(TalkRules.Jealous); c.talkFlags.Remove(TalkRules.CoupleTalk); }
                if (c.romance == TalkRules.Couple)
                {
                    if (!active || !Bonds.Holds(s, s.playerId, c.npcId, BondKinds.Showmance))
                    {
                        c.romance = TalkRules.Ended;
                        c.romanceClosedUntil = TalkRules.ForTheSeason;
                        c.romanceWeek = s.week;
                        if (active) AddFlag(c, TalkRules.Ex);
                    }
                    else if (s.Score(c.npcId, s.playerId) < TalkRomance.EndsBelowView)
                    {
                        Act(s, c.npcId, s.playerId, TalkRomance.TheyEndIt, "they-end");
                        EndCouple(s, npc, c, false, npc.name + " ended it with you.");
                    }
                    else
                    {
                        c.coupleAnchors = Math.Min(TalkRules.MostCount, c.coupleAnchors + 1);
                        if (c.coupleAnchors >= TalkRomance.PublicAfterAnchors) MakePublic(s, c.npcId);
                    }
                }
                if (c.talkFlags.Contains(TalkRules.Ex) && s.week > c.romanceWeek + TalkRomance.ExWeeks) c.talkFlags.Remove(TalkRules.Ex);
            }
        }

        /// <summary>The player as Head of Household put their partner on the block (spec 3.9): they end it, -8 more, closed for the season. After every draw of the command.</summary>
        private static void PartnerNamed(EpisodeState s, IEnumerable<string> named)
        {
            if (!TalkRomance.On(s) || s.hohId != s.playerId || named == null) return;
            foreach (var id in named.Where(x => x != null).Distinct().ToList())
            {
                var c = FreeTalk.Contact(s, id);
                var npc = s.Find(id);
                if (c == null || npc == null || c.romance != TalkRules.Couple) continue;
                Act(s, id, s.playerId, TalkRomance.TheyEndIt, "named");
                EndCouple(s, npc, c, true, npc.name + " ended it after you put them on the block.");
            }
        }
    }
}
