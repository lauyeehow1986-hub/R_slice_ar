# Slice-AR (Android) — Roadmap

Phased delivery toward full feature parity with the iOS Slice-AR. Each phase is independently
buildable and should end clean (`unity_get_compilation_errors` clean + on-device smoke test).

## Phase 0 — Scaffold
- Create Unity 6 URP project in the repo; add a Unity `.gitignore`.
- Add packages: AR Foundation, ARCore XR Plugin, Unity Localization.
- Vendor `mlavik1/UnityVolumeRendering` into `Assets/ThirdParty/UnityVolumeRendering` (keep its MIT `LICENSE`).
- Land `CLAUDE.md` + this `docs/roadmap.md`.

## Phase 1 — 3D core
- Load a bundled sample volume from `StreamingAssets`.
- Raycast direct volume rendering in a standard 3D scene with an orbit camera.
- **Verify:** build to device; sample volume renders; orbit works.

## Phase 2 — Motion slicing (non-AR)
- Cross-section plane whose transform is driven by device gyroscope/accelerometer.
- Visible cut surface where the plane intersects the volume.
- **Verify:** on-device, plane tracks device motion; cut surface updates smoothly.

## Phase 3 — AR mode
- AR Foundation session; anchor the volume in the world.
- AR camera pose drives the slicing plane; UI toggle between 3D and AR modes.
- Detect ARCore availability; hide AR mode gracefully when unsupported.
- **Verify:** on ARCore hardware, volume is world-anchored and sliceable by moving the device.

## Phase 4 — LUTs + shading
- Transfer-function presets = color LUTs; UI to switch/edit them.
- Normal/gradient-based shading toggle.
- **Verify:** switching LUTs and shading changes the render as expected.

## Phase 5 — Import
- Android Storage Access Framework picker.
- Import image sequences + small DICOM datasets; editable voxel size.
- **DICOM caveat:** verify UnityVolumeRendering's DICOM path under IL2CPP/ARM64 (SimpleITK
  native libs vs. managed reader); fall back to managed reader or fo-dicom if needed.
- **Verify:** import a public sample DICOM series; renders with correct voxel scale.

## Phase 6 — Annotations
- Place/edit markers anchored to the volume; persist per dataset.
- **Verify:** annotations survive save/reload and stay anchored under slicing.

## Phase 7 — Localization
- Wire six languages (EN/IT/ES/DE/JA/FR) via Unity Localization string tables.
- **Verify:** switching device/app locale updates all UI strings.

## Phase 8 — Sample data + polish
- Bundle or on-demand-download public sample datasets (Harvard Dataverse or equivalent,
  license-checked).
- Disclaimer screen ("not for diagnosis/clinical use").
- Packaging (AAB), APK-size and performance pass.
- **Verify:** clean install runs end-to-end; size acceptable; disclaimer shown on first run.

## Cross-cutting risks
- **DICOM on Android** — resolve native vs. managed path in Phase 5 (Phases 1–4 unblocked via RAW/bundled volumes).
- **APK size** — original iOS is ~300 MB; prefer on-demand datasets + a slim default volume; ship AAB.
- **AR hardware** — AR mode requires ARCore; 3D + motion modes must work everywhere.
- **Licensing/IP** — retain third-party licenses; confirm clearance for the "Slice-AR" name before any Play Store listing.
