using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;
using UMA;
using UMA.CharacterSystem;
using UnityEngine.TestTools;

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// The cast library as content: one look per template, each dressed in pieces UMA actually
    /// ships and that the race it is built on can wear.
    ///
    /// <para>Every failure mode here is silent in the game. A wardrobe name that does not resolve is
    /// skipped by <see cref="UmaBodyProvider"/> so the houseguest simply arrives without trousers; a
    /// piece cut for the other race is dropped by UMA with a warning nobody reads; two pieces in one
    /// wardrobe slot means the second quietly replaces the first. None of it throws, so none of it
    /// shows up anywhere except on the body, which is why it is pinned here instead.</para>
    /// </summary>
    public sealed class UmaCastLookPlayModeTests
    {
        /// <summary>
        /// The library must cover the table the casting screen draws from, both rosters. Anyone
        /// missing falls back through <c>Resolve</c> to a trait-matched appearance, which is five
        /// faces — the thing twenty-four looks exist to stop.
        /// </summary>
        [Test]
        public void EveryTemplate_HasALookOfItsOwn()
        {
            var missing = CastTemplates.Everyone
                .Where(template => !UmaCastLibrary.TryGet(template.Id, out _))
                .Select(template => template.Id)
                .ToArray();

            Assert.That(missing, Is.Empty,
                "These houseguests can be cast but have no UMA look: " + string.Join(", ", missing));
        }

        /// <summary>The player's own body is not in the template table and still needs a look.</summary>
        [Test]
        public void ThePlayer_HasALook()
        {
            Assert.That(UmaCastLibrary.TryGet(ContentCatalog.PlayerId, out var look), Is.True);
            Assert.That(look.Wardrobe, Is.Not.Empty);
        }

        /// <summary>
        /// A body built on the wrong race is the one mistake in this table a player would name out
        /// loud, and the pronouns are what the rest of the game already says about that person.
        /// </summary>
        [Test]
        public void EveryLooksRace_AgreesWithTheTemplatesPronouns()
        {
            foreach (var template in CastTemplates.Everyone)
            {
                Assert.That(UmaCastLibrary.TryGet(template.Id, out var look), Is.True, template.Id);
                string expected = template.Pronouns == "she/her" ? UmaCastLibrary.FemaleRace
                    : template.Pronouns == "he/him" ? UmaCastLibrary.MaleRace
                    : look.Race;
                Assert.That(look.Race, Is.EqualTo(expected),
                    template.Name + " is " + template.Pronouns + " everywhere else in the game.");
            }
        }

        /// <summary>
        /// Their own id wins over the appearance the trait mapping picked; someone with no look of
        /// their own still gets that appearance, so an imported houseguest is still dressed.
        /// </summary>
        [Test]
        public void Resolve_PrefersTheHouseguestsOwnIdOverTheAppearance()
        {
            Assert.That(UmaCastLibrary.Resolve("alex-chen", "maya-hassan", out var own), Is.True);
            Assert.That(own.Race, Is.EqualTo(UmaCastLibrary.MaleRace), "Alex, not Maya.");

            Assert.That(UmaCastLibrary.Resolve("someone-the-table-never-heard-of", "maya-hassan", out var fallback),
                Is.True);
            UmaCastLibrary.TryGet("maya-hassan", out var maya);
            Assert.That(fallback, Is.SameAs(maya));

            Assert.That(UmaCastLibrary.Resolve(null, null, out _), Is.False);
        }

        /// <summary>
        /// One piece per wardrobe slot. UMA keeps the last recipe applied to a slot, so a second
        /// shirt is not a layered look — it is the first shirt not being worn.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryLook_WearsAtMostOnePieceInEachWardrobeSlot()
        {
            yield return null;
            var indexer = UMAAssetIndexer.Instance;
            Assert.That(indexer, Is.Not.Null, "The Global Library must be present for any of this to work.");

            foreach (var appearanceId in UmaCastLibrary.AppearanceIds)
            {
                Assert.That(UmaCastLibrary.TryGet(appearanceId, out var look), Is.True);
                var bySlot = new Dictionary<string, string>();
                foreach (var recipeName in look.Wardrobe)
                {
                    var recipe = indexer.GetAsset<UMAWardrobeRecipe>(recipeName);
                    if (recipe == null) continue;   // the resolution test owns that failure
                    Assert.That(bySlot.ContainsKey(recipe.wardrobeSlot), Is.False,
                        appearanceId + " wears both '" + (bySlot.TryGetValue(recipe.wardrobeSlot, out var first) ? first : "?")
                        + "' and '" + recipeName + "' in the " + recipe.wardrobeSlot + " slot; only the second would show.");
                    bySlot[recipe.wardrobeSlot] = recipeName;
                }
            }
        }

        /// <summary>
        /// Every piece must be cut for the race the look is built on. UMA refuses an incompatible
        /// recipe rather than adapting it, so a male shirt on a female body is a bare chest.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryWardrobeRecipe_IsCutForTheRaceItIsWornOn()
        {
            yield return null;
            var indexer = UMAAssetIndexer.Instance;
            Assert.That(indexer, Is.Not.Null);

            foreach (var appearanceId in UmaCastLibrary.AppearanceIds)
            {
                Assert.That(UmaCastLibrary.TryGet(appearanceId, out var look), Is.True);
                foreach (var recipeName in look.Wardrobe)
                {
                    var recipe = indexer.GetAsset<UMAWardrobeRecipe>(recipeName);
                    if (recipe == null) continue;
                    Assert.That(recipe.compatibleRaces, Does.Contain(look.Race),
                        appearanceId + " is a " + look.Race + " and wears '" + recipeName
                        + "', which is cut for " + string.Join(", ", recipe.compatibleRaces) + ".");
                }
            }
        }
    }
}
