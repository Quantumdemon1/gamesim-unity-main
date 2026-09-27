GameSim Asset Pack 5 — House + Broadcast Branding

Target: Unity uGUI + URP 17.6. This pack is environment/presentation-focused and does not require changing the render pipeline or UI framework.

Folders:
- EnvironmentWallGraphics: wall decals / feature graphics / room motifs
- NeonMasks: white emissive masks + colored references for neon signs
- DigitalDisplays: 1920x1080 world-screen backgrounds
- CompetitionKit: lanes, markers, podium fronts, station symbols, power graphics
- RoomSignage: room-name decals
- Broadcast: full-screen title cards and blank lower-thirds
- SurfaceDecals: grayscale/transparent detail masks
- VFXTextures: particle textures, streaks, confetti, smoke, pulses

Recommended Unity use:
1. Wall graphics / room signage: SpriteRenderer, Decal Projector, or unlit transparent plane. Use white versions as tintable masks.
2. Neon masks: use as emission/alpha masks in a URP Shader Graph; control emission color and intensity from material, not from the PNG.
3. Digital display backgrounds: world-space Canvas RawImage or screen mesh material. Keep dynamic text/portraits in Unity on top.
4. Competition kit: place on modular planes/meshes. Recolor lanes by material property where possible.
5. Surface decals: use as detail/roughness/emission masks; they are not physically accurate PBR scans.
6. VFX textures: Particle System texture sheets / additive or alpha-blended materials.

Quality rules:
- Keep normal house lighting/materials primary; neon is accent, not the whole room.
- Only competition/event mode should push saturated colors hard.
- Use world graphics to create room identity and landmarks from the overhead camera.
- Make Diary Room, HOH Suite, Memory Wall, and Competition Yard the strongest authored spaces.
- Prefer one hero wall graphic per room rather than many competing signs.
- Do not bake player names, votes, week numbers, scores or other dynamic data into world textures.
