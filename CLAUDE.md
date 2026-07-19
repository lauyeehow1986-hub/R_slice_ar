# CLAUDE.md — Slice-AR (Android)

## What this is
Android reimplementation of the iOS app "Slice-AR": an interactive tool for exploring 3D
medical volume data (MRI/DICOM). Users define cross-section planes by moving the device, in
both an **AR mode** and a **standard 3D mode**. Educational/research tool — **not for
diagnosis or clinical decision-making** (state this in-app, matching the original's disclaimer).

Clean-room reimplementation built from the concept only. Do **NOT** copy the original app's
code, assets, bundle identifier, or branding.

## Tech stack
- **Unity 6.4 (6000.4.10f1)**, Universal Render Pipeline (URP), Android build target (IL2CPP, ARM64).
- **AR Foundation + ARCore XR Plugin** (AR mode, device pose → clipping plane). Added via the
  Package Manager / MCP after first open so versions resolve for this editor.
- Rendering core: **`com.mlavik1.easyvolumerenderer`** (mlavik1/UnityVolumeRendering, MIT) — a
  git-package dependency in `Packages/manifest.json` (supports Built-in/URP/HDRP; we use URP).
  If we need to modify its shaders for mobile/AR, embed it under `Packages/` as a local fork.
- MCP bridge: **`com.anklebreaker.unity-mcp`** (same bridge as gamev2) — lets this assistant
  drive the editor once the project is open.
- **Unity Localization** (EN / IT / ES / DE / JA / FR).
- Min Android API: **24+**. AR mode needs an ARCore-supported device; 3D + motion modes must
  work without ARCore.

## Project layout (target)
```
Assets/Scripts/            App code (slicing, AR session, UI, import, annotations)
Assets/Scenes/             Main, ARMode, ThreeDMode
Assets/StreamingAssets/    Bundled sample volume datasets
Assets/Localization/       String tables per language
Packages/manifest.json     Package deps (URP, Localization, MCP bridge, volume renderer)
docs/                      Design notes, roadmap (see docs/roadmap.md)
```

## Conventions
- C#, PascalCase types/methods, camelCase locals; one MonoBehaviour per file.
- Keep the volume-rendering package **untouched**; extend via new components in
  `Assets/Scripts/` so upstream updates stay mergeable. Only fork it into `Packages/` if a
  shader edit for mobile/AR is genuinely required.
- **Never commit patient data or real DICOM studies** — only public/synthetic sample volumes.
- No network calls except optional, explicit, user-initiated dataset downloads.

## Build & run
- Open in Unity Hub with the matching Unity 6 LTS editor.
- Platform → Android; scripting backend IL2CPP, target ARM64; add ARCore XR Plugin.
- Build APK/AAB via **File > Build Settings**, or the Unity MCP build tools.
- AR features must be smoke-tested on **physical hardware** (the emulator can't run ARCore).

## Feature parity target (from the iOS app)
Direct volume rendering · device-motion cross-section plane · AR + 3D modes · DICOM support ·
normal/gradient shading · annotations · color LUTs (transfer functions) · 6-language UI ·
import image sequences + small DICOM datasets from device storage · editable voxel size ·
pre-loaded sample datasets.

## Roadmap
Phased delivery (Phase 0 scaffold → Phase 8 polish). See [docs/roadmap.md](docs/roadmap.md).

## Key references
- Rendering core: https://github.com/mlavik1/UnityVolumeRendering (MIT)
- AR Foundation: https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.5/
- Original app (feature reference only): https://apps.apple.com/gb/app/slice-ar/id6746819867
