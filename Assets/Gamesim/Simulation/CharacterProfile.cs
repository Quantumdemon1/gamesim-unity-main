using System;

namespace Gamesim.Simulation
{
    /// <summary>A reusable houseguest. Seasons receive a copy, never a reference to this library entry.</summary>
    [Serializable]
    public sealed class CharacterProfile
    {
        public int version = 1;
        public string id, name, sourceTemplateId;
        public ContestantState contestant;

        public static CharacterProfile FromDraft(string id, CharacterDraft draft)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            return new CharacterProfile
            { id = id, name = draft.Name.Trim(), sourceTemplateId = draft.SourceTemplateId, contestant = draft.ToContestant() };
        }
        public CharacterDraft ToDraft() => CharacterDraft.FromContestant(contestant);
        public CharacterProfile Clone() => new CharacterProfile
        { version = version, id = id, name = name, sourceTemplateId = sourceTemplateId, contestant = contestant?.Clone() };
        public bool TryValidate(out string error)
        {
            error = null;
            if (version != 1 || !Guid.TryParseExact(id, "N", out _)) { error = "Invalid houseguest profile identifier or version."; return false; }
            if (contestant == null || contestant.stats == null || contestant.traits == null || contestant.nominationWeeks == null)
            { error = "The houseguest profile is incomplete."; return false; }
            if (name != contestant.name || sourceTemplateId != contestant.sourceTemplateId)
            { error = "The houseguest profile identity does not match its saved character."; return false; }
            return ToDraft().TryValidate(out error);
        }
    }
}
