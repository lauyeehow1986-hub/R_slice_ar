# Slice-AR (Android)

An interactive tool for exploring 3D medical volume data (MRI / CT / DICOM) on an Android phone.
View any cross-section as a flat slice or a 3D cut-away, and in AR mode move the device through a
volume anchored in the room to carve the cutting plane by hand.

> ### ⚠️ Not for diagnosis
> Slice-AR is an **educational and research tool**. It is **not a medical device** and must **not**
> be used for diagnosis, treatment, or any clinical decision-making.
>
> The bundled dataset is a public, de-identified sample. Only load data you are authorised to use —
> never real patient studies without consent.

## Install

Download the APK from the [latest release](https://github.com/lauyeehow1986-hub/R_slice_ar/releases/latest)
and open it on your phone.

Android will warn you that the file came from an unknown source, and Play Protect may ask to scan it.
That is expected for any app installed outside the Play Store — allow installation from your browser
or file manager when prompted.

**Requirements**

| | |
|---|---|
| Android | 7.1 (API 25) or newer |
| Size | ~60 MB installed |
| AR mode | Needs an [ARCore-supported device](https://developers.google.com/ar/devices) |
| 3D mode | Works on any device — no ARCore required |

On a phone without ARCore the app opens straight into the 3D viewer and hides the AR button.

## What it does

**3D viewer** — Axial, coronal and sagittal planes with correct anatomical orientation. Tilt the
phone to scrub through the stack, pinch to zoom, drag to orbit the 3D cut-away.

**AR mode** — Anchors the volume in the room and drives the cutting plane from the device pose, so
walking the phone through it sweeps the cross-section. Anchors to a detected plane where one is
available, which holds far steadier than a free anchor.

**Rendering** — Direct volume rendering with gradient shading, intensity windowing tuned separately
for CT and MRI, and four colour lookup tables (Grayscale, Hot Metal, Rainbow, Cool).

**Annotations** — Place markers on a slice and measure distances between them in millimetres. Saved
per dataset, on the device only.

**Import your own data** — Headerless RAW (with editable dimensions, data type, endianness and voxel
size), image sequences as a `.zip`, and uncompressed DICOM series as a `.zip`. Everything stays on
the phone.

**Nine languages** — English, Italian, Spanish, German, Japanese, French, Chinese, Malay, Tamil.

## Privacy

Slice-AR makes **no network calls**. Nothing is uploaded, and no analytics or telemetry are
collected. Imported datasets and annotations are written only to the app's private storage and are
removed when you uninstall.

## Bundled dataset

**MRHead** — a T1 MRI head scan from the [3D Slicer](https://www.slicer.org/) sample data, donated
for use without restriction and covered by Slicer's BSD-style licence. See
[`Assets/StreamingAssets/DATASETS_LICENSE.md`](Assets/StreamingAssets/DATASETS_LICENSE.md) for full
attribution.

## Building from source

Requires **Unity 6000.4.10f1** with Android Build Support (IL2CPP, ARM64).

1. Clone and open the project in Unity Hub.
2. The platform is already set to Android; packages resolve from `Packages/manifest.json`.
3. **File ▸ Build Profiles ▸ Build** (or **Build And Run** with a phone connected over USB).

AR features can only be tested on physical hardware — the emulator cannot run ARCore.

## Third-party

- [UnityVolumeRendering](https://github.com/mlavik1/UnityVolumeRendering) by Matias Lavik — MIT.
  The raycast volume renderer, transfer functions and DICOM/RAW importers.
- [NativeFilePicker](https://github.com/yasirkula/UnityNativeFilePicker) by yasirkula — MIT.
  Android file picking for dataset import.
- Unity AR Foundation and the ARCore XR Plugin.

## About

A clean-room Android reimplementation built from the concept of an existing iOS app of the same
name. It shares no code, assets, or branding with it, and is not affiliated with or endorsed by its
developer.
