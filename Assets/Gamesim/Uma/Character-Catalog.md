# Authoring wardrobe content

The creator reads `UmaWardrobeCatalog` assets under a Resources folder at
`Gamesim/CharacterCatalog`. The shipped `HouseWardrobe.asset` lists the installed
wardrobe content; body compatibility, slots, conflict rules, dye channels and
thumbnails still come from the actual UMA recipe.

To add a compatible hairstyle or garment:

1. Author/import its UMA wardrobe recipe and include it in the UMA Global Library.
2. Create a catalog with **Create > Gamesim > Characters > UMA wardrobe catalog**,
   or add an entry to the existing catalog. Put new catalogs in the Resources path
   above. Set the exact `recipeName`, a player-facing label and `available`.
3. Keep the generated `id` permanently. It identifies the item in saved characters.
   On a recipe rename, change `recipeName` but retain every former name in `aliases`.
   Old schema 13 recipe-name saves resolve through those aliases without being rewritten.
4. Set `styleGroup` to the same value for equivalent items on different bodies.
   Tags such as color/pattern prefer similar equivalents; lower `fallbackPriority`
   breaks ties. Do not group garments as equivalents solely because they share a slot.
5. Check both bodies, their supported DNA extremes, all five outfit categories,
   seating and locomotion. Check thumbnails and color channels. A baked texture does
   not become dyeable simply by adding a catalog entry.

The loader rejects duplicate IDs/aliases and exposes `Diagnostics`; the catalog
tests require the shipped data to have none. A catalog is an allowlist, so set
`available: false` to retire an item from the picker while retaining its saved-ID
mapping. Missing content preserves the original saved selection and uses a
temporary compatible rendering fallback. Adding a garment in a supported slot
requires content changes, not creator UI changes. New slots or rig families need
an explicit provider/UX capability addition.

The shipped IDs were seeded from each asset's GUID, but runtime resolution uses the
catalog record. Replacing or moving a UMA asset therefore does not change a saved
ID. No Unity/UMA upgrade is required by this catalog.
