The original BOXOPHOBIC FREE Skybox Extended Shader package was imported from the
local Asset Store cache. Only the core shader/editor utilities and night cubemap
are included; the demonstration scenes and scenery are not needed by MoonBeach.

TurtleGame adds an optional **Blue Night Mode** to `Skybox/Cubemap Extended`.
It replaces the magenta background with a blue gradient and extracts the existing
cubemap stars before tinting, so their brightness can be adjusted independently.
The original rendering path is unchanged when Blue Night Mode is disabled.

The shader's Amplify graph is the original vendor graph. Re-exporting that graph
or updating the vendor shader requires reapplying the marked TurtleGame extension.

Tune `Assets/Materials/BlueNightSky.mat` for zenith color, horizon color and star
brightness. `Turtle Game > Cielo nocturno azul` applies the material or captures
the beach and sky with both the PC and Mobile URP configurations.
