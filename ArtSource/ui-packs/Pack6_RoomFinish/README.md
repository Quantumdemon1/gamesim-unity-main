# GameSim Room Finish Pack 6

Purpose
-------
This pack is for finishing the EXISTING GameSim rooms. It is not a floor-plan replacement.

Use these assets as overlays, decals, material masks, rug/fabric textures, framed art, prop labels, and editor staging aids.

Core rule
---------
Copy the level of finish, composition, prop density, materials and room identity from the approved room mockups.
Do NOT copy altered room architecture.

Recommended Unity import settings
---------------------------------
WallTreatments / Rugs / Bedding / Upholstery / Kitchen / GameRoom / HOHRewards /
BedroomClutter / NominationRoom / DiaryRoom:
- Texture Type: Default or Sprite depending usage
- Wrap Mode: Repeat for seamless/tileable surfaces where appropriate
- Filter Mode: Bilinear
- Compression: High Quality
- Mip Maps: ON for world-space/material usage
- sRGB: ON for color textures

Transparent decals / frames / editor markers:
- Texture Type: Sprite (2D and UI) or Default for material/decal use
- Alpha Is Transparency: ON
- Wrap Mode: Clamp
- Mip Maps: OFF for Screen Space UI, ON for world-space decals

EditorInteractionMarkers:
- Editor-only / development reference
- Do not include in shipped runtime visuals

Room priorities
---------------
1. Diary Room
2. Kitchen / Dining
3. Living Room
4. HOH Suite
5. Nomination Room
6. Game Room
7. Shared Bedrooms
8. Competition Yard

Environment finishing order
---------------------------
1. Furniture scale / room focal composition
2. Major furniture
3. Materials
4. Medium dressing
5. Small prop clusters
6. GameSim identity
7. Interaction staging
8. Final light/emissive balance

Do not change the established URP 17.6 render pipeline or baked-night lighting solution.
