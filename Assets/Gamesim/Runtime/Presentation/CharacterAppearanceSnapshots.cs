using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>Resolve preset references only when a new season is staged, before its first durable save.</summary>
    public static class CharacterAppearanceSnapshots
    {
        public static void Materialize(EpisodeState state)
        {
            if (state?.contestants == null) return;
            var catalog = (CharacterBodySource.Provider as IModularCharacterBodyProvider)?.Catalog;
            foreach (var person in state.contestants)
            {
                var recipe = person.appearance?.Clone() ?? CharacterAppearance.Preset(person.sourceTemplateId ?? person.id);
                recipe.fallbackId = CharacterPresentation.AppearanceId(person, ContentCatalog.CanonicalId(person.id));
                person.appearance = catalog == null ? recipe : catalog.Materialize(recipe);
                if (person.appearance == null || !person.appearance.TryValidate(out _))
                    throw new System.ArgumentException("The appearance for " + person.name + " could not be prepared for this season.");
            }
        }
    }
}
