using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The prospective Vote family's internal entries, reached by reflection from the editor's test
    /// assembly, which cannot see Gamesim.Simulation's internals (no InternalsVisibleTo, by the lead's
    /// decision; the precedent is UnifiedSafetyWholeStateTests' Prospective). Each call names the
    /// actual production owner and its exact signature, so a renamed or reshaped entry fails here as a
    /// missing owner instead of compiling against something else. Nothing here validates, converts or
    /// installs anything of its own: it forwards the arguments and unwraps the owner's exception.
    /// </summary>
    internal static class ProspectiveVoteFacade
    {
        private static readonly Assembly Simulation = typeof(EpisodeState).Assembly;
        private static readonly Type ContextType = typeof(UnifiedVoteCompletedReveal).GetNestedType("Context", BindingFlags.NonPublic);
        private static readonly Type ReferencesType = Simulation.GetType("Gamesim.Simulation.UnifiedVoteReferences");
        private static readonly Type OutString = typeof(string).MakeByRefType();

        /// <summary>EpisodeValidation.TryValidateProspectiveUnifiedVote: the internal mode-2 whole core.</summary>
        internal static bool TryValidateProspectiveUnifiedVote(EpisodeState s, out string error) =>
            Try(typeof(EpisodeValidation), "TryValidateProspectiveUnifiedVote", new[] { typeof(EpisodeState), OutString }, s, out error);

        /// <summary>UnifiedCommitmentHearings.TryValidateProspectiveVoteStorage.</summary>
        internal static bool TryValidateProspectiveVoteStorage(EpisodeState s, out string error) =>
            Try(typeof(UnifiedCommitmentHearings), "TryValidateProspectiveVoteStorage", new[] { typeof(EpisodeState), OutString }, s, out error);

        /// <summary>UnifiedCommitmentHearings.CanonicalLeaf.</summary>
        internal static bool CanonicalLeaf(EpisodeState s, HouseFactState fact) =>
            (bool)Call(typeof(UnifiedCommitmentHearings), "CanonicalLeaf", new[] { typeof(EpisodeState), typeof(HouseFactState) }, s, fact);

        /// <summary>UnifiedVoteCompletedReveal.TryContext; the context is opaque here and only handed back.</summary>
        internal static bool TryContext(EpisodeState s, out object context, out string error)
        {
            var args = new object[] { s, null, null };
            bool accepted = (bool)Invoke(Method(typeof(UnifiedVoteCompletedReveal), "TryContext",
                typeof(EpisodeState), ContextType.MakeByRefType(), OutString), args);
            context = args[1]; error = (string)args[2];
            return accepted;
        }

        /// <summary>UnifiedVoteFamilyValidation.TryValidateVoteRow, with a context from <see cref="TryContext"/>.</summary>
        internal static bool TryValidateVoteRow(EpisodeState s, object context, IReadOnlyList<UnifiedVoteRevealState> archive,
            UnifiedCommitmentState row, out string error)
        {
            var args = new object[] { s, context, archive, row, null };
            bool accepted = (bool)Invoke(Method(typeof(UnifiedVoteFamilyValidation), "TryValidateVoteRow", typeof(EpisodeState),
                ContextType, typeof(IReadOnlyList<UnifiedVoteRevealState>), typeof(UnifiedCommitmentState), OutString), args);
            error = (string)args[4];
            return accepted;
        }

        /// <summary>UnifiedVoteFamilyValidation.FirstDecision: the first actually deciding archived frame.</summary>
        internal static bool FirstDecision(EpisodeState s, IReadOnlyList<UnifiedVoteRevealState> archive, UnifiedCommitmentState row,
            out UnifiedVoteRevealState deciding, out string status, out string actor, out string error)
        {
            var args = new object[] { s, archive, row, null, null, null, null };
            bool accepted = (bool)Invoke(Method(typeof(UnifiedVoteFamilyValidation), "FirstDecision", typeof(EpisodeState),
                typeof(IReadOnlyList<UnifiedVoteRevealState>), typeof(UnifiedCommitmentState),
                typeof(UnifiedVoteRevealState).MakeByRefType(), OutString, OutString, OutString), args);
            deciding = (UnifiedVoteRevealState)args[3]; status = (string)args[4]; actor = (string)args[5]; error = (string)args[6];
            return accepted;
        }

        /// <summary>UnifiedVoteFamilyValidation.TryValidateDraftBundle.</summary>
        internal static bool TryValidateDraftBundle(EpisodeState s, IReadOnlyList<UnifiedCommitmentState> additions,
            IReadOnlyList<DealState> rawAdditions, DealState rawReplacement, out string error)
        {
            var args = new object[] { s, additions, rawAdditions, rawReplacement, null };
            bool accepted = (bool)Invoke(Method(typeof(UnifiedVoteFamilyValidation), "TryValidateDraftBundle", typeof(EpisodeState),
                typeof(IReadOnlyList<UnifiedCommitmentState>), typeof(IReadOnlyList<DealState>), typeof(DealState), OutString), args);
            error = (string)args[4];
            return accepted;
        }

        /// <summary>UnifiedVoteFamilyValidation.TryValidateAnswer.</summary>
        internal static bool TryValidateAnswer(EpisodeState s, UnifiedCommitmentState answered, out string error)
        {
            var args = new object[] { s, answered, null };
            bool accepted = (bool)Invoke(Method(typeof(UnifiedVoteFamilyValidation), "TryValidateAnswer",
                typeof(EpisodeState), typeof(UnifiedCommitmentState), OutString), args);
            error = (string)args[2];
            return accepted;
        }

        /// <summary>UnifiedVoteFamilyValidation.Prefix: an origin's source identity prefix.</summary>
        internal static string Prefix(string origin) =>
            (string)Call(typeof(UnifiedVoteFamilyValidation), "Prefix", new[] { typeof(string) }, origin);

        /// <summary>UnifiedVoteReferences.ProjectPromise: a canonical promise row's detached raw view.</summary>
        internal static PromiseState ProjectPromise(UnifiedCommitmentState row) =>
            (PromiseState)Call(ReferencesType, "ProjectPromise", new[] { typeof(UnifiedCommitmentState) }, row);

        /// <summary>UnifiedVoteReferences.ProjectDeal: a canonical deal row's detached raw view.</summary>
        internal static DealState ProjectDeal(UnifiedCommitmentState row) =>
            (DealState)Call(ReferencesType, "ProjectDeal", new[] { typeof(UnifiedCommitmentState) }, row);

        /// <summary>UnifiedVoteReferences.DealsUnchecked: raw and projected canonical deals, detached.</summary>
        internal static IReadOnlyList<DealState> DealsUnchecked(EpisodeState s) =>
            (IReadOnlyList<DealState>)Call(ReferencesType, "DealsUnchecked", new[] { typeof(EpisodeState) }, s);

        private static bool Try(Type owner, string name, Type[] parameters, EpisodeState s, out string error)
        {
            var args = new object[] { s, null };
            bool accepted = (bool)Invoke(Method(owner, name, parameters), args);
            error = (string)args[1];
            return accepted;
        }

        private static object Call(Type owner, string name, Type[] parameters, params object[] args) =>
            Invoke(Method(owner, name, parameters), args);

        private static MethodInfo Method(Type owner, string name, params Type[] parameters)
        {
            Assert.That(owner, Is.Not.Null, "The prospective Vote owner type of " + name + " exists in Gamesim.Simulation.");
            Assert.That(ContextType, Is.Not.Null, "UnifiedVoteCompletedReveal.Context is the archive context's actual owner.");
            var method = owner.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, parameters, null);
            Assert.That(method, Is.Not.Null, owner.Name + "." + name + " is the actual production owner with this signature.");
            return method;
        }

        private static object Invoke(MethodInfo method, object[] args)
        {
            try { return method.Invoke(null, args); }
            catch (TargetInvocationException wrapped) when (wrapped.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(wrapped.InnerException).Throw();
                throw;
            }
        }
    }
}
