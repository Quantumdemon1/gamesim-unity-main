using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free talk's person tables (slice S5's groundwork, built switched off at talk rules 5; spec 5.4): each of the twelve
    /// regulars' own talks, in their own voice. What every table holds, that no line names anybody, that from talk rules 5 a
    /// regular's own talk goes ahead of their pool's (spec 3.13), and that below it every regular is offered exactly main's talk.
    /// <see cref="TalkTablesTests"/> checks every row's text, gates and tones as for the pools. Simulation only, so the Unity-free
    /// subset runs it too.
    /// </summary>
    public sealed class TalkPersonTablesTests
    {
        const int TalksEach = 30;

        static List<string> Regulars => CastTemplates.In(CastTemplates.Roster.Regular).Select(t => t.Id).ToList();

        static IEnumerable<TalkRow> PersonRows => TalkTables.PersonTables.Values.SelectMany(t => t);

        static IEnumerable<KeyValuePair<string, string>> Lines(TalkRow row)
        {
            yield return new KeyValuePair<string, string>(row.Id + " open", row.Open);
            for (int i = 0; i < row.Replies.Length; i++)
            {
                var r = row.Replies[i];
                yield return new KeyValuePair<string, string>(row.Id + " reply " + i + " line", r.Line);
                yield return new KeyValuePair<string, string>(row.Id + " reply " + i + " lands", r.Lands);
                yield return new KeyValuePair<string, string>(row.Id + " reply " + i + " even", r.Even);
                yield return new KeyValuePair<string, string>(row.Id + " reply " + i + " grates", r.Grates);
            }
            if (row.Standing == null) yield break;
            yield return new KeyValuePair<string, string>(row.Id + " standing honest", row.Standing.Honest);
            yield return new KeyValuePair<string, string>(row.Id + " standing neutral", row.Standing.Neutral);
            yield return new KeyValuePair<string, string>(row.Id + " standing dishonest", row.Standing.Dishonest);
        }

        // ------------------------------------------------------------------ the tables

        [Test]
        public void EveryRegularHasOneTableOfTheirOwnTalksWrittenForTalkLevelFiveAndNoAllStarHasOne()
        {
            Assert.That(Regulars.Count, Is.EqualTo(12), "The control: the twelve Regular Season templates.");
            Assert.That(TalkTables.PersonTables.Keys, Is.EquivalentTo(Regulars), "One table for each regular, and none for an All-Star.");
            var bad = new List<string>();
            foreach (var pair in TalkTables.PersonTables)
            {
                var rows = pair.Value;
                if (!TalkTables.Tables.Contains(rows)) bad.Add(pair.Key + ": a table the registry does not join is never offered nor checked");
                if (rows.Length != TalksEach) bad.Add(pair.Key + ": " + rows.Length + " talks, not " + TalksEach + " (spec 5.4)");
                string head = pair.Key.Split('-')[0] + ".";
                foreach (var row in rows)
                {
                    if (row.Owner != pair.Key || row.Pool != null) bad.Add(row.Id + ": owner " + row.Owner + ", pool " + row.Pool);
                    if (row.FromRules != TalkRules.ComesToTalk) bad.Add(row.Id + ": written for talk level " + row.FromRules + ", not 5");
                    if (!row.Id.StartsWith(head)) bad.Add(row.Id + ": want " + head + "<talk>");
                    if (row.Kind != TalkKind.Layer && row.Kind != TalkKind.Rhythm && row.Kind != TalkKind.Standing && row.Kind != TalkKind.Event)
                        bad.Add(row.Id + ": a " + row.Kind + " talk (stage and romance talks stay the pools')");
                }
                // What a person table covers: their first talk, a frame on each layer, where they stand, the week's events, and
                // the usuals that may come round again once all is spent (ruling R9).
                if (rows.Count(r => r.Kind == TalkKind.Layer && r.When.First) != 1) bad.Add(pair.Key + ": one first talk");
                for (int layer = 1; layer <= 3; layer++)
                    if (!rows.Any(r => r.Kind == TalkKind.Layer && !r.When.First && r.Layer == layer)) bad.Add(pair.Key + ": a frame on layer " + layer);
                if (!rows.Any(r => r.Kind == TalkKind.Standing)) bad.Add(pair.Key + ": a standing talk");
                if (rows.Count(r => r.Kind == TalkKind.Event) < 5) bad.Add(pair.Key + ": five event talks or more");
                if (rows.Count(r => r.Usual && r.When.Window == null && r.When.Week == 0) < 2) bad.Add(pair.Key + ": two usual talks or more");
            }
            // An All-Star's own rows are romance rows only (slice 3): an All-Star's ordinary talks come from the star pool (R7).
            foreach (var row in TalkTables.All.Where(r => r.Owner != null && !TalkRomance.IsRomanceKind(r.Kind) && !Regulars.Contains(r.Owner)))
                bad.Add(row.Id + ": an ordinary talk of " + row.Owner + "'s own");
            Assert.That(bad, Is.Empty);
        }

        [Test]
        public void NoOwnLineNamesAnybodyOrHoldsAPrivateRomanticOrGenderedWord()
        {
            // Every first name and surname on a card, both rosters. "Will" is left out: it is also the verb ("Will do.").
            var names = CastTemplates.Everyone.SelectMany(t => t.Name.Split(' ')).Where(n => n != "Dr." && n != "Will").Distinct().ToList();
            var named = new Regex(@"\b(?:" + string.Join("|", names.Select(Regex.Escape)) + @")\b");
            var gendered = new Regex(@"\b(he|she|him|her|his|hers|himself|herself)\b", RegexOptions.IgnoreCase);
            var rows = PersonRows.ToList();
            Assert.That(rows.Count, Is.EqualTo(12 * TalksEach), "The control: every regular's own talks are read.");
            var bad = new List<string>();
            foreach (var row in rows)
                foreach (var line in Lines(row))
                {
                    foreach (Match m in TalkTablesTests.DenyWords.Matches(line.Value ?? "")) bad.Add(line.Key + ": " + m.Value);
                    foreach (Match m in gendered.Matches(line.Value ?? "")) bad.Add(line.Key + ": " + m.Value);
                    foreach (Match m in named.Matches(line.Value ?? "")) bad.Add(line.Key + ": names " + m.Value);
                    // Only a standing form fills a token ({close}, {worry}, {safe}); no other line names a housemate or the player,
                    // who in a house of both rosters could be an All-Star.
                    foreach (string token in TalkText.Tokens)
                        if ((line.Value ?? "").Contains("{" + token + "}")) bad.Add(line.Key + ": the token {" + token + "}");
                }
            Assert.That(bad, Is.Empty);
        }

        // ------------------------------------------------------------------ the offer

        [Test]
        public void AtTalkLevelFiveEachRegularOpensWithTheirOwnFirstTalkAndBelowItWithMainsTalk()
        {
            var bad = new List<string>();
            foreach (string id in Regulars)
            {
                var s = FreeTalkFixture.Fresh(TalkRules.ComesToTalk);
                var npcs = FreeTalkFixture.Npcs(s);
                var npc = npcs[0];
                npc.sourceTemplateId = id;
                var other = npcs.Skip(1).First(c => StoryPeople.TemplateOf(c).Id != id);
                Assert.That(FreeTalk.WindowKey(s), Is.EqualTo(1), "The control: move-in night.");

                var offer = FreeTalk.Offer(s, npc.id);
                if (!offer.Available) { bad.Add(id + ": " + offer.Reason); continue; }
                if (offer.Talk.Owner != id || !offer.Talk.When.First) bad.Add(id + ": at level 5 their own first talk, not " + offer.Talk.Id);
                var elsewhere = FreeTalk.Offer(s, other.id);
                if (elsewhere.Talk == null || elsewhere.Talk.Owner == id) bad.Add(id + ": another housemate offered " + elsewhere.Talk?.Id);

                foreach (int level in new[] { TalkRules.Romance, TalkRules.Couples })
                {
                    s.story.talkRules = level;
                    var below = FreeTalk.Offer(s, npc.id);
                    TalkOffer main;
                    using (TalkBank.Use(TalkTables.All.Where(r => r.FromRules == 0))) main = FreeTalk.Offer(s, npc.id);
                    if (below.Talk == null || below.Talk.Owner != null || below.Talk.Id != main.Talk?.Id || below.Repeat != main.Repeat)
                        bad.Add(id + " at level " + level + ": offered " + below.Talk?.Id + ", main offers " + main.Talk?.Id);
                }
            }
            Assert.That(bad, Is.Empty);
        }

        [Test]
        public void AtTalkLevelFiveAWalkedSeasonOffersEachRegularTheirOwnTalksOnceAndTakesEveryAnswer()
        {
            var engine = new EpisodeEngine(FreeTalkFixture.Fresh(TalkRules.ComesToTalk, seed: 3211u));
            var house = FreeTalkFixture.Npcs(engine.Snapshot).Select(c => c.id).ToList();
            var own = house.ToDictionary(id => id, id => 0);
            var seen = new HashSet<string>();
            var kinds = new HashSet<TalkKind>();
            var bad = new List<string>();
            int lastKey = -1;
            for (int step = 0; step < 4000; step++)
            {
                var s = engine.Snapshot;
                if (s.phase == EpisodePhase.Finished || s.week > 3 || s.Find(s.playerId).status != ContestantStatus.Active) break;
                int key = FreeTalk.WindowKey(s);
                if (key != 0 && key != lastKey)
                {
                    lastKey = key;
                    foreach (var npc in FreeTalkFixture.Npcs(s))
                    {
                        var at = engine.Snapshot;
                        var offer = FreeTalk.Offer(at, npc.id);
                        if (offer.Talk == null) continue;
                        string where = "week " + at.week + " window " + key + " " + npc.id + ": " + offer.Talk.Id;
                        if (offer.Talk.Owner != null && !TalkRomance.IsRomanceKind(offer.Talk.Kind))
                        {
                            if (offer.Talk.Owner != StoryPeople.TemplateOf(npc).Id) bad.Add(where + ": another regular's own talk");
                            if (!offer.Repeat && !seen.Add(npc.id + "|" + offer.Talk.Id)) bad.Add(where + ": said twice");
                            own[npc.id]++;
                            kinds.Add(offer.Talk.Kind);
                        }
                        var reply = offer.Talk.Replies?.FirstOrDefault(r => r != null && r.Choice == TalkChoice.None && TalkRules.IsTone(r.Tone) && r.Tone != TalkRules.Flirt);
                        if (reply == null) continue;
                        var result = engine.Apply(FreeTalkFixture.Command(at, npc.id, offer.Talk.Id, reply.Tone));
                        if (!result.accepted) bad.Add(where + ": answer refused, " + result.reason);
                    }
                }
                var next = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(next.accepted, Is.True, "The walk: " + next.reason);
            }
            Assert.That(bad, Is.Empty);
            foreach (string id in house)
                Assert.That(own[id], Is.GreaterThanOrEqualTo(1), id + ": their own first talk on move-in night, at the least.");
            Assert.That(own.Values.Sum(), Is.GreaterThanOrEqualTo(4 * house.Count), "The house's own talks over three weeks.");
            Assert.That(kinds.Contains(TalkKind.Layer) && kinds.Contains(TalkKind.Rhythm), Is.True, "Own layer frames and house life were both said.");
        }
    }
}
