using System;
using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    public sealed partial class PortVerification
    {
        // These gestures are presentation, not simulation commands. Baselines are deliberately
        // taken after TryOpenNpc has paused/flushed the house's existing NPC clock.
        private sealed class ConversationNavigationBaseline
        {
            public string json, savePath;
            public uint gameplayRandom, npcRandom;
            public int spent, budget;
            public string[] accepted;
            public byte[] saved, backup;
            public bool hadBackup;
        }

        private ConversationNavigationBaseline ConversationBaseline()
        {
            var state = seasonDirector.Snapshot;
            RequireSeason(EpisodeValidation.TryValidate(state, out var reason),
                "Conversation navigation requires a canonical valid season: " + reason);
            RequireSeason(state.npcSocial != null, "The canonical season must retain its NPC random stream.");
            string path = seasonDirector.SavePath;
            RequireSeason(!string.IsNullOrEmpty(path) && File.Exists(path),
                "Conversation verification requires the actual durable selected slot.");
            string backupPath = new EpisodeSaveStore(path).BackupPath;
            bool backup = File.Exists(backupPath);
            return new ConversationNavigationBaseline
            {
                json = JsonUtility.ToJson(state), savePath = path,
                gameplayRandom = state.randomState, npcRandom = state.npcSocial.randomState,
                spent = EpisodeEngine.SocialActionsSpent(state), budget = EpisodeEngine.SocialActionBudget(state),
                accepted = state.acceptedCommandIds.ToArray(),
                saved = ReadConversationSlotBytes(path),
                hadBackup = backup, backup = backup ? ReadConversationSlotBytes(backupPath) : null,
            };
        }

        private static byte[] ReadConversationSlotBytes(string path)
        {
            // The same 8 MiB limit the existing save store admits; never inspect an arbitrary
            // unbounded file while checking a presentation gesture. Hold the read against
            // replacement/writes; a metadata precheck alone would permit a growth race.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long length = stream.Length;
                RequireSeason(length >= 0 && length <= SaveJson.MaximumBytes,
                    "The selected conversation slot exceeds the save-store byte limit.");
                var bytes = new byte[(int)length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    RequireSeason(read > 0, "The selected conversation slot ended before its held length.");
                    offset += read;
                }
                RequireSeason(stream.ReadByte() == -1 && stream.Length == length,
                    "The selected conversation slot changed length during its bounded read.");
                return bytes;
            }
        }

        private void RequireConversationUnchanged(ConversationNavigationBaseline before, string gesture)
        {
            var after = seasonDirector.Snapshot;
            RequireSeason(JsonUtility.ToJson(after) == before.json,
                gesture + " changed the complete authoritative season.");
            RequireSeason(after.randomState == before.gameplayRandom && after.npcSocial.randomState == before.npcRandom,
                gesture + " advanced a random stream.");
            RequireSeason(EpisodeEngine.SocialActionsSpent(after) == before.spent
                && EpisodeEngine.SocialActionBudget(after) == before.budget,
                gesture + " changed the action budget or spent counters.");
            RequireSeason(after.acceptedCommandIds.SequenceEqual(before.accepted),
                gesture + " submitted another simulation command.");
            RequireSeason(seasonDirector.SavePath == before.savePath
                && File.Exists(before.savePath)
                && ReadConversationSlotBytes(before.savePath).SequenceEqual(before.saved),
                gesture + " rewrote or replaced the durable selected slot.");
            string backupPath = new EpisodeSaveStore(before.savePath).BackupPath;
            RequireSeason(File.Exists(backupPath) == before.hadBackup
                && (!before.hadBackup || ReadConversationSlotBytes(backupPath).SequenceEqual(before.backup)),
                gesture + " rotated or rewrote the selected slot's backup.");
        }

        private IEnumerator EnterSeasonConversationCategory(string caption)
        {
            RequireSeason(seasonDirector.IsConversationOpen, "A named topic requires an open conversation.");
            yield return DrainSeasonConversationExchange("before-topics", false);
            var baseline = ConversationBaseline();
            if (seasonDirector.CurrentConversationCategory != caption)
            {
                if (HasSeasonButtonText(EpisodeDirector.ConversationTopicsBackCaption))
                {
                    yield return ClickSeasonButton(EpisodeDirector.ConversationTopicsBackCaption);
                    seasonReport.conversationNavigations++;
                    RequireConversationUnchanged(baseline, "Back to topics");
                }
                RequireSeason(HasSeasonButtonText(caption), "The actual topic control is unavailable: " + caption);
                yield return ClickSeasonButton(caption);
                seasonReport.conversationNavigations++;
            }
            RequireSeason(seasonDirector.CurrentConversationCategory == caption,
                "The actual topic gesture did not reach " + caption + ".");
            RequireConversationUnchanged(baseline, "Selecting " + caption);
        }

        private IEnumerator ReturnSeasonConversationTopics()
        {
            RequireSeason(seasonDirector.IsConversationOpen, "Returning to topics requires an open conversation.");
            yield return DrainSeasonConversationExchange("before-topics", false);
            var baseline = ConversationBaseline();
            if (HasSeasonButtonText(EpisodeDirector.ConversationTopicsBackCaption))
            {
                yield return ClickSeasonButton(EpisodeDirector.ConversationTopicsBackCaption);
                seasonReport.conversationNavigations++;
            }
            RequireSeason(seasonDirector.CurrentConversationCategory == null,
                "The actual Back gesture did not restore the topic wheel.");
            RequireConversationUnchanged(baseline, "Returning to root topics");
        }

        private IEnumerator DrainSeasonConversationExchange(string label, bool graphical,
            Func<string, IEnumerator> capture = null, bool skip = false)
        {
            int gestures = 0;
            while (seasonDirector.IsConversationExchangePlaying && gestures < 2)
            {
                CheckSeasonDeadline();
                var baseline = ConversationBaseline();
                string speaker = seasonDirector.ConversationExchangeSpeakerId;
                if (graphical)
                {
                    string frame = label + (speaker == seasonDirector.Snapshot.playerId ? "-player" : "-npc");
                    if (capture != null) yield return capture(frame);
                    else yield return CaptureSeason(frame, true);
                    RequireConversationUnchanged(baseline, "Capturing the accepted speaker");
                }
                string caption = skip ? EpisodeHud.ConversationSkipCaption : EpisodeHud.ConversationContinueCaption;
                RequireSeason(HasSeasonButtonText(caption), "The accepted exchange needs its actual playback control.");
                yield return ClickSeasonButton(caption);
                seasonReport.conversationPlaybackControls++;
                RequireConversationUnchanged(baseline, caption);
                gestures++;
                if (skip) break;
            }
            RequireSeason(!seasonDirector.IsConversationExchangePlaying,
                "The accepted exchange did not close after its bounded playback gestures.");
        }
    }
}
