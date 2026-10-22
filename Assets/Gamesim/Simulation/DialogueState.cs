using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Schema 29's closed dialogue storage. This foundation keeps the rule disabled;
    /// no episode, grant, time receipt or conditional obligation is authored yet.
    /// Required lists initialize empty but clones preserve invalid nulls for validation.
    /// </summary>
    [Serializable]
    public sealed class DialogueState
    {
        public List<DialogueEpisodeState> episodes = new List<DialogueEpisodeState>();
        public List<DialogueRoleGrantState> grants = new List<DialogueRoleGrantState>();
        public List<DialogueVetoOccasionState> vetoOccasions = new List<DialogueVetoOccasionState>();

        public DialogueState Clone()
        {
            var copy = (DialogueState)MemberwiseClone();
            copy.episodes = episodes?.Select(item => item?.Clone()).ToList();
            copy.grants = grants?.Select(item => item?.Clone()).ToList();
            copy.vetoOccasions = vetoOccasions?.Select(item => item?.Clone()).ToList();
            return copy;
        }
    }

    [Serializable]
    public sealed class DialogueEpisodeState
    {
        public string id, npcId, occasionKind, occasionId, entryEventId;
        public int contentVersion, week, window, startedRevision, startedSequence;
        public EpisodePhase startedPhase;
        public DialogueEntryReceiptState entry;
        public List<DialogueResponseState> responses = new List<DialogueResponseState>();
        public List<DialogueEvidenceState> evidence = new List<DialogueEvidenceState>();
        public DialogueStatementState statement;
        public DialogueTimeReceiptState time;

        public DialogueEpisodeState Clone()
        {
            var copy = (DialogueEpisodeState)MemberwiseClone();
            copy.entry = entry?.Clone();
            copy.responses = responses?.Select(item => item?.Clone()).ToList();
            copy.evidence = evidence?.Select(item => item?.Clone()).ToList();
            copy.statement = statement?.Clone();
            copy.time = time?.Clone();
            return copy;
        }
    }

    [Serializable]
    public sealed class DialogueEntryReceiptState
    {
        public string kind, grantId;
        public int revision, windowActionBefore, windowActionAfter;
        public DialogueEntryReceiptState Clone() => (DialogueEntryReceiptState)MemberwiseClone();
    }

    [Serializable]
    public sealed class DialogueResponseState
    {
        public string eventId, speakerId, responseKey;
        public int pathIndex, revision;
        public DialogueResponseState Clone() => (DialogueResponseState)MemberwiseClone();
    }

    [Serializable]
    public sealed class DialogueEvidenceState
    {
        public string id, kind, eventId;
        public int pathIndex, capturedRevision;
        public PowerRow power;
        public MemoryState memory;
        public string statementId;

        public DialogueEvidenceState Clone()
        {
            var copy = (DialogueEvidenceState)MemberwiseClone();
            copy.power = power?.Clone();
            copy.memory = memory?.Clone();
            return copy;
        }
    }

    [Serializable]
    public sealed class DialogueStatementState
    {
        public string id, eventId, speakerId, targetId, answer, conditionKind;
        public int pathIndex, week, revision, sequence;
        public List<string> audienceIds = new List<string>();

        public DialogueStatementState Clone()
        {
            var copy = (DialogueStatementState)MemberwiseClone();
            copy.audienceIds = audienceIds == null ? null : new List<string>(audienceIds);
            return copy;
        }
    }

    [Serializable]
    public sealed class DialogueRoleGrantState
    {
        public string id, ownerId, role, sourceKind, status, consumedEpisodeId, expiryReason;
        public int week, sourceRevision, sourceSequence, consumedRevision, expiredRevision;
        public EpisodePhase sourcePhase;
        public List<string> sourceNominees = new List<string>();

        public DialogueRoleGrantState Clone()
        {
            var copy = (DialogueRoleGrantState)MemberwiseClone();
            copy.sourceNominees = sourceNominees == null ? null : new List<string>(sourceNominees);
            return copy;
        }
    }

    [Serializable]
    public sealed class DialogueTimeReceiptState
    {
        public int revision, sequence, window, proposedDelta, appliedDelta;
        public string reason;
        public int spentRevision, spentSequence;
        public int spentWindowAction;
        public DialogueTimeReceiptState Clone() => (DialogueTimeReceiptState)MemberwiseClone();
    }

    [Serializable]
    public sealed class DialogueVetoOccasionState
    {
        public int week, resultRevision, resultSequence;
        public string hohId, holderId;
        public List<string> participants = new List<string>();
        public List<string> nomineesAtResult = new List<string>();
        public DialogueVetoDecisionState decision;

        public DialogueVetoOccasionState Clone()
        {
            var copy = (DialogueVetoOccasionState)MemberwiseClone();
            copy.participants = participants == null ? null : new List<string>(participants);
            copy.nomineesAtResult = nomineesAtResult == null ? null : new List<string>(nomineesAtResult);
            copy.decision = decision?.Clone();
            return copy;
        }
    }

    [Serializable]
    public sealed class DialogueVetoDecisionState
    {
        public int revision, sequence;
        public List<string> activeIds = new List<string>();
        public List<string> nominees = new List<string>();
        public List<string> replacementCandidates = new List<string>();
        public bool used;
        public string savedId, replacementId;

        public DialogueVetoDecisionState Clone()
        {
            var copy = (DialogueVetoDecisionState)MemberwiseClone();
            copy.activeIds = activeIds == null ? null : new List<string>(activeIds);
            copy.nominees = nominees == null ? null : new List<string>(nominees);
            copy.replacementCandidates = replacementCandidates == null ? null : new List<string>(replacementCandidates);
            return copy;
        }
    }

    [Serializable]
    public sealed class ConditionalVetoTermsState
    {
        public string statementId, consentEventId;
        public int consentPathIndex, consentRevision;
        public ConditionalVetoResolutionState resolution;

        public ConditionalVetoTermsState Clone()
        {
            var copy = (ConditionalVetoTermsState)MemberwiseClone();
            copy.resolution = resolution?.Clone();
            return copy;
        }
    }

    [Serializable]
    public sealed class ConditionalVetoResolutionState
    {
        public string reason, departedId, departedStatus;
        public int revision, sequence, vetoOccasionWeek;
        public ConditionalVetoResolutionState Clone() => (ConditionalVetoResolutionState)MemberwiseClone();
    }
}
