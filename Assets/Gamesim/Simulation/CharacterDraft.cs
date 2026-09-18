using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// A houseguest being built: setup step two of the reference build's wizard.
    ///
    /// <para>The cast screen made it possible to choose who you play as. This is the step that was
    /// still missing — the one where you are not any of them. Name, age, occupation, hometown, bio
    /// and pronouns; two traits at most; eight stats starting at five with five spare points, held
    /// between one and ten.</para>
    ///
    /// <para><b>The stat block is mutated, not recomputed.</b> That matters because
    /// <see cref="WebTraits.Apply"/> clamps on the way up and on the way down, so a stat at nine
    /// that gains a trait's +2 lands on ten and gives back two when the trait is removed, finishing
    /// at eight. Rebuilding the block from scratch on every change would quietly repair that and
    /// make a houseguest built here disagree with the same houseguest built in the web form. The
    /// asymmetry is pinned by <c>ClampingIsLossyOnTheWayBack_MatchingTheWebGame</c> and is deliberate
    /// there too.</para>
    ///
    /// <para>Nothing here persists. A draft becomes a <see cref="ContestantState"/> at the moment the
    /// season is built and is then forgotten, so the creator adds no saved field and needs no schema
    /// version — every field it writes has existed since schema 8.</para>
    /// </summary>
    public sealed class CharacterDraft
    {
        /// <summary>What every stat starts at before traits or spare points.</summary>
        public const double StartingStat = 5;

        /// <summary>The source's allowance, spent one point at a time.</summary>
        public const int SparePoints = 5;

        /// <summary>
        /// The ages the form offers.
        ///
        /// <para>Narrower than the engine's own bound, which accepts nought to a hundred and twenty
        /// because it has to accept whatever an old save holds. This is the range a person can pick,
        /// not the range a record may contain.</para>
        /// </summary>
        public const int MinimumAge = 18, MaximumAge = 99, DefaultAge = 28;

        /// <summary>Bounds taken from <see cref="EpisodeValidation"/>, so a draft cannot build an
        /// invalid season and then fail at the save.</summary>
        public const int NameLimit = 100, ShortLimit = 100, BioLimit = 1000;

        public string Name = string.Empty;
        public string Occupation = string.Empty;
        public string Hometown = string.Empty;
        public string Bio = string.Empty;
        public string Pronouns = "they/them";
        public string Archetype = "The Newcomer";
        public string HomeRoom = "Living";
        public string Motive = "Choose who to trust, survive the vote, and build a game you can explain.";
        public int Age = DefaultAge;

        public readonly List<string> Traits = new List<string>();
        public ContestantStats Stats = Flat();

        private int spent;

        /// <summary>The pronoun sets the form offers, which are the ones the shipped cast uses.</summary>
        public static readonly string[] PronounOptions = { "she/her", "he/him", "they/them" };

        /// <summary>A blank slate — the source's "Create your own".</summary>
        public static CharacterDraft Blank() => new CharacterDraft();

        /// <summary>
        /// A card opened for editing — the source's "Customize this character".
        ///
        /// <para><b>The card's stats do not come along, and the difference is not small.</b> The
        /// source lists what a chosen character brings — name, age, job, hometown, bio, traits, look
        /// and pronouns — and stats are not on it; step two then says every stat starts at five with
        /// five spare points, for anyone who reaches that step by any route. So this copies the copy
        /// and the traits, applies the trait boosts to a flat five, and leaves the allowance
        /// unspent.</para>
        ///
        /// <para>The consequence is worth stating plainly, because it is a real one.
        /// <see cref="CastTemplates.ToContestant"/> builds a card's stats through
        /// <see cref="WebTraits.CreateStats"/>, which seeds each stat at six, five or four depending
        /// on whether any held trait names it — so a card played as it is has a sharper, more lopsided
        /// block than the same card customised, which starts even and then has five points to spend
        /// wherever it likes. Customising is therefore not a way to keep a houseguest and adjust
        /// them; it is a way to start again from what they were. Pinned by
        /// <c>CustomisingACardDoesNotPreserveItsStats</c>.</para>
        /// </summary>
        public static CharacterDraft From(CastTemplates.Template template)
        {
            var draft = new CharacterDraft();
            if (template == null) return draft;

            // Hometown and bio are left blank on purpose: the cast table has never held either.
            // They arrived with the creator's own card copy in schema 8, and writing twenty-four
            // biographies to fill them in would be authoring a cast rather than porting one.
            draft.Name = template.Name ?? string.Empty;
            draft.Occupation = template.Occupation ?? string.Empty;
            draft.Pronouns = string.IsNullOrEmpty(template.Pronouns) ? "they/them" : template.Pronouns;
            draft.Archetype = string.IsNullOrEmpty(template.Archetype) ? "The Newcomer" : template.Archetype;
            draft.HomeRoom = string.IsNullOrEmpty(template.HomeRoom) ? "Living" : template.HomeRoom;
            if (!string.IsNullOrEmpty(template.Motive)) draft.Motive = template.Motive;
            if (template.Age > 0) draft.Age = Math.Max(MinimumAge, Math.Min(MaximumAge, template.Age));

            foreach (var trait in template.Traits ?? Array.Empty<string>()) draft.AddTrait(trait);
            return draft;
        }

        // ---------------------------------------------------------------- points

        public int Spent => spent;
        public int Remaining => SparePoints - spent;

        public bool CanRaise(string stat) =>
            Remaining > 0 && WebTraits.Get(Stats, stat) < WebTraits.Maximum;

        /// <summary>Only points the player put in can be taken back out, never a trait's boost.</summary>
        public bool CanLower(string stat) =>
            spent > 0 && WebTraits.Get(Stats, stat) > WebTraits.Minimum;

        public bool Raise(string stat)
        {
            if (!CanRaise(stat)) return false;
            WebTraits.Set(Stats, stat, WebTraits.Get(Stats, stat) + 1);
            spent++;
            return true;
        }

        public bool Lower(string stat)
        {
            if (!CanLower(stat)) return false;
            WebTraits.Set(Stats, stat, WebTraits.Get(Stats, stat) - 1);
            spent--;
            return true;
        }

        // ---------------------------------------------------------------- traits

        public bool HasTrait(string trait) =>
            Traits.Any(held => string.Equals(held, trait, StringComparison.OrdinalIgnoreCase));

        public bool CanAddTrait(string trait) =>
            WebTraits.Known(trait) && !HasTrait(trait) && Traits.Count < WebTraits.MaximumTraits;

        public bool AddTrait(string trait)
        {
            if (!CanAddTrait(trait)) return false;
            // The table's own spelling, not the caller's, so two traits that differ only in case
            // cannot both be held and the boost table is always found.
            string canonical = WebTraits.Boosts.Keys.First(key =>
                string.Equals(key, trait, StringComparison.OrdinalIgnoreCase));
            Traits.Add(canonical);
            WebTraits.Apply(Stats, canonical, true);
            return true;
        }

        public bool RemoveTrait(string trait)
        {
            int index = Traits.FindIndex(held => string.Equals(held, trait, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return false;
            string held = Traits[index];
            Traits.RemoveAt(index);
            WebTraits.Apply(Stats, held, false);
            return true;
        }

        /// <summary>Which trait would be displaced by adding another, so the form can say so.</summary>
        public string Crowded => Traits.Count >= WebTraits.MaximumTraits ? Traits[0] : null;

        // ---------------------------------------------------------------- committing

        /// <summary>
        /// Whether this would build a houseguest the engine accepts.
        ///
        /// <para>Every bound checked here is one <see cref="EpisodeValidation"/> also checks. The
        /// duplication is the point: a draft that fails validation at the save has already replaced
        /// the player's slot decision with an error message, and the form can say "your name is too
        /// long" while they are still typing it.</para>
        /// </summary>
        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(Name)) return Fail(out error, "Give your houseguest a name.");
            if (Name.Length > NameLimit) return Fail(out error, "That name is longer than " + NameLimit + " characters.");
            if (Age < MinimumAge || Age > MaximumAge)
                return Fail(out error, "Age is between " + MinimumAge + " and " + MaximumAge + ".");
            if (Length(Occupation) > ShortLimit || Length(Hometown) > ShortLimit
                || Length(Pronouns) > ShortLimit || Length(Archetype) > ShortLimit)
                return Fail(out error, "Occupation, hometown, pronouns and archetype are " + ShortLimit + " characters at most.");
            if (Length(Bio) > BioLimit) return Fail(out error, "A bio is " + BioLimit + " characters at most.");
            if (Traits.Count > WebTraits.MaximumTraits)
                return Fail(out error, "Two personality traits at most.");
            if (Traits.Any(trait => !WebTraits.Known(trait)))
                return Fail(out error, "One of those traits is not on the list.");
            if (spent < 0 || spent > SparePoints)
                return Fail(out error, "Spare points are between zero and " + SparePoints + ".");
            if (WebTraits.StatNames.Any(stat =>
                    WebTraits.Get(Stats, stat) < WebTraits.Minimum || WebTraits.Get(Stats, stat) > WebTraits.Maximum))
                return Fail(out error, "Every stat is held between " + WebTraits.Minimum + " and " + WebTraits.Maximum + ".");
            error = null;
            return true;
        }

        /// <summary>
        /// The houseguest this draft describes.
        ///
        /// <para>The id is <see cref="ContentCatalog.PlayerId"/> for the same reason a chosen card's
        /// is: which record the engine treats as yours does not depend on who you decided to be.</para>
        /// </summary>
        public ContestantState ToContestant()
        {
            if (!TryValidate(out var error)) throw new ArgumentException(error);
            return new ContestantState
            {
                id = ContentCatalog.PlayerId,
                name = Name.Trim(),
                pronouns = Pronouns,
                isPlayer = true,
                status = ContestantStatus.Active,
                motive = Motive,
                homeRoom = HomeRoom,
                archetype = Archetype,
                occupation = Trim(Occupation),
                hometown = Trim(Hometown),
                bio = Trim(Bio),
                age = Age,
                traits = new List<string>(Traits),
                stats = Stats.Clone(),
            };
        }

        /// <summary>
        /// A separate draft with the same contents, so backing out of the form can put back what was
        /// there. Written out by hand rather than cloned: <c>MemberwiseClone</c> would hand the copy
        /// the <i>same</i> trait list and stat block, and the form edits both.
        /// </summary>
        public CharacterDraft Copy()
        {
            var copy = new CharacterDraft
            {
                Name = Name, Occupation = Occupation, Hometown = Hometown, Bio = Bio,
                Pronouns = Pronouns, Archetype = Archetype, HomeRoom = HomeRoom, Motive = Motive,
                Age = Age, spent = spent, Stats = Stats.Clone(),
            };
            copy.Traits.AddRange(Traits);
            return copy;
        }

        // ---------------------------------------------------------------- internals

        private static ContestantStats Flat()
        {
            var stats = new ContestantStats();
            foreach (var name in WebTraits.StatNames) WebTraits.Set(stats, name, StartingStat);
            return stats;
        }

        private static int Length(string value) => value == null ? 0 : value.Length;
        private static string Trim(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private static bool Fail(out string error, string message) { error = message; return false; }
    }
}
