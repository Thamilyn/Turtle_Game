# Repository Guidelines

## Project Structure & Module Organization

This Unity 6 project uses Universal Render Pipeline (URP).
- `Assets/Scripts/`: runtime movement, follow-camera, and prototype-help components in the `TurtleGame` namespace.
- `Assets/Editor/`: prototype generation and controller verification tools in `TurtleGame.Editor`; keep `UnityEditor` dependencies here.
- `Assets/Scenes/`, `Assets/Prefabs/`, and `Assets/Materials/`: playable scenes and reusable assets.
- `Assets/Settings/`: rendering configuration; `Assets/TutorialInfo/` contains template documentation.
- `Packages/`: dependency manifest and lockfile; `ProjectSettings/`: shared Unity configuration and editor version.

## Build, Test, and Development Commands

Use Unity **6000.3.23f1**, as recorded in `ProjectSettings/ProjectVersion.txt`. In PowerShell, set `$unity` to that installation's `Unity.exe`; run commands from the repository root.

- `& $unity -projectPath .`: open the editor. Load `Assets/Scenes/TurtlePrototype.unity`, enter Play Mode, and use WASD.
- `& $unity -batchmode -projectPath . -executeMethod TurtleGame.Editor.TurtlePrototypeBuilder.Create -quit -logFile setup.log`: generate prototype assets. The builder refuses to overwrite existing targets.
- `& $unity -batchmode -projectPath . -executeMethod TurtleGame.Editor.TurtleControllerVerification.Run -logFile verification.log`: run behavior checks; the verifier exits with status 0 or 1. Omit `-quit` so Play Mode checks can finish.

The builder and verifier target `Assets/TurtleGame/Scenes/TurtlePrototype.unity`, whereas existing assets live under `Assets/Scenes/`. Generate the expected scene or reconcile paths before verification. For player builds, use Unity's Build Profiles and explicitly select the intended scene; build settings currently enable `SampleScene`.

## Coding Style & Naming Conventions

Follow existing C#: four-space indentation, braces on separate lines, PascalCase types/methods/properties, and camelCase private fields/parameters. Match component filenames to class names. Prefer private `[SerializeField]` fields for Inspector configuration. No formatter or lint configuration is checked in.

Keep physics on the player root and model rotation under `Visual`; the camera follows the root.

## Testing Guidelines

Unity Test Framework is installed, but no dedicated test suite or coverage threshold exists. The custom verifier checks movement, diagonal speed, braking, gravity, collisions, timestep consistency, model replacement, and camera targeting. Inspect `Logs/controller-verification-result.txt`. For new framework tests, use `Assets/Tests/EditMode/` or `Assets/Tests/PlayMode/` and descriptive `*Tests.cs` names.

## Commit & Pull Request Guidelines

History contains one commit, `Initialize Unity URP project scaffold`; follow its concise, imperative style. Keep changes focused. PRs should explain behavior, list verification performed, link relevant issues, and include screenshots for visual changes. Commit asset `.meta` files together with their assets; exclude generated caches, logs, and builds.
