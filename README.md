# CatWalk

Unity project (Unity **6000.0.48f1**, URP).

## Getting started

1. Clone the repository.
2. Open the `CatWalk/` folder in Unity Hub (Add → project from disk).
3. Use editor version `6000.0.48f1`; Unity will regenerate `Library/` on first open.

## Blender integration

[Blender Bridge](https://github.com/Kayosac/Blender-Unity-Integration) **0.1.1** is installed through Unity Package Manager from the local archive in `Tools/Packages/`. The archive contains the unmodified upstream commit [`f3c4b34a784401c36dd1c6e911cb0fcf1a293d04`](https://github.com/Kayosac/Blender-Unity-Integration/commit/f3c4b34a784401c36dd1c6e911cb0fcf1a293d04), including the Blender add-on and self-tests. This avoids the Git/xcrun architecture mismatch in the current macOS Unity installation and keeps the package available after cloning this project.

Install Blender 5.0 or newer; the bridge loads its Blender add-on automatically.

Select an FBX in Unity and choose **Open in Blender** in the inspector or **Blender → Open in Blender** in the context menu. In Blender, use the **Unity** sidebar panel (**N**) and **Export to Unity** to send changes back.

If Blender is not detected, set its path in **Preferences → Blender Bridge**. Project options are under **Project Settings → Blender Bridge**.

To update the bridge, create a new UPM archive from the desired upstream commit with `git archive --format=tar.gz --prefix=package/ --output=<archive.tgz> <commit>`, place it in `Tools/Packages/`, and update the archive path in `CatWalk/Packages/manifest.json`.

## FuelStop04 station

The assembled station is [FuelStop04.prefab](CatWalk/Assets/CatWalk/Prefabs/FuelStop04.prefab). Search for `t:Prefab FuelStop04` in Unity's Project window. The kit contains 35 reusable models, 182 module instances and one shared texture atlas, including the corrected cabin interior, supports and surface joins.

The kit consists of finished art assets; no baking scripts or generation step are required.

## Environment art layout

All environment kits share `CatWalk/Assets/CatWalk/Content/Environment/`:

- `Models/<Pack>/` — models grouped by pack (`FuelStop04`, `SpaceKonbini`).
- `Materials/` — shared model materials using the built-in `Universal Render Pipeline/Lit` shader, plus the explicitly requested procedural planet material described below.
- `Textures/` — color atlases and emission maps.
- `Modules/<Pack>/` — reusable prefabs built from individual models.

Complete station prefabs remain in `CatWalk/Assets/CatWalk/Prefabs/`.
Blender sources, concepts and catalogs live locally in `ArtSource/<Pack>/` and are never committed.
Every agent must follow the layout and asset migration rules in [AGENTS.md](AGENTS.md).

## Code layout

All game code (C# and shaders) lives in `CatWalk/Assets/CatWalk/Content/Code/`, one folder per game section. See [AGENTS.md](AGENTS.md) for the rules.

## Procedural planet surface

Assign [PlanetSurface.mat](CatWalk/Assets/CatWalk/Content/Environment/Materials/PlanetSurface.mat) to a sphere, or create a material with shader **CatWalk/Planet Surface**. Duplicate the material to give each planet its own settings. The shader is in `CatWalk/Assets/CatWalk/Content/Code/Planets/Shaders/`.

Use a sphere centred on its local origin with a uniform positive Transform scale. Unity's built-in Sphere works; a more evenly subdivided sphere gives a more uniform silhouette. Surface coordinates come from the local 3D direction, so there are no texture inputs, UV seams or stretched poles. Keep these objects out of static batching, which replaces their local coordinates.

- **Shape:** Seed changes the layout. Terrain scale, hills, roughness and mountains shape the surface. Relief depth controls the maximum displacement as a fraction of the original sphere radius; 0 disables geometric displacement. The original sphere is the outer envelope, and terrain lies inside it, keeping the renderer's original culling bounds valid.
- **Craters / Depressions / Canyons:** independent toggles, frequency/scale and depth. Craters also have density, size and rim height; canyons have width and meandering controls. Larger frequency values produce smaller, more numerous features.
- **URP Lit Surface:** lowland, highland and cliff colors, height blending, cliff slope threshold, metallic and smoothness. Lighting uses URP's own `UniversalFragmentPBR`, with standard lights, shadows, reflection probes, GI, fog and SSAO. Forward/Forward+ and deferred renderers are supported through `UniversalForwardOnly` and a matching depth/normals pass.
- **Grass Cover:** enable/disable, green/dry colors, coverage, patch scale, elevation range, maximum slope, detail, microrelief and smoothness. Elevation is the normalized terrain field (0–1); slope is `1 - cos(angle)` (0 is flat, 1 is vertical). Grass is a surface layer, not individual blades. Fine grass detail fades when it becomes smaller than a pixel.
- **Adaptive Tessellation:** maximum subdivisions defaults to 4, reducing to the base mesh with camera distance. Near/far distances are measured in planet radii. A value of 1 uses the base mesh; increasing to 8 or 16 substantially increases triangle count. GPUs without tessellation use the second subshader, which displaces existing vertices. Use a denser sphere there for fine silhouettes.

Displacement is visual: the material does not change a SphereCollider/MeshCollider or the gravity radius. For walkable terrain, collision geometry must be authored to match separately. Narrow canyons and small crater rims need sufficient mesh/tessellation density to affect the silhouette; their normal and color detail is still visible at lower density. Changing shape parameters after lightmap baking requires rebaking. Terrain animation/motion-vector output and standalone grass geometry are outside this material's scope.
