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

The assembled station is [FuelStop04.prefab](CatWalk/Assets/SpaceCourier/Prefabs/FuelStop04.prefab). Search for `t:Prefab FuelStop04` in Unity's Project window. The kit contains 35 reusable models, 182 module instances and one shared texture atlas, including the corrected cabin interior, supports and surface joins.

Blender sources, previews, the asset catalog and rebuilding instructions are in [ArtSource/FuelStop04](ArtSource/FuelStop04/README.md). The kit is self-contained and does not require the old courier gameplay prototype.
