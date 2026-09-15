# Gamesim Unity source

This folder is the owned game-code and content boundary for the Unity port.

- `Runtime/Core`: engine-independent foundation code.
- `Runtime/Bootstrap`: application startup components.
- `Editor`: editor-only setup, validation, test, and build tooling.
- `Tests/EditMode`: fast foundation tests that run without entering Play Mode.
- `Scenes`: production scenes, beginning with `Bootstrap.unity`.
- `Art`, `Audio`, `Data`, and `Prefabs`: game content organized by responsibility.

Unity template assets remain outside this boundary until they are intentionally migrated.

