# Bundled sample datasets — attribution & license

## MRHead (`MRHead_256x256x130_int16_le.raw`) — SHIPPED SAMPLE

- **Source:** 3D Slicer sample data ("MRHead"), an MRI (T1) scan of a human head.
  Original file `MR-head.nrrd` from the SlicerTestingData repository
  (SHA-256 `cc211f0dfd9a05ca3841ce1141b292898b2dd2d3f08286affadf823a7e58df93`).
- **License / usage rights:** The 3D Slicer sample datasets (MRHead, CBCT-MR Head,
  CT-MR Brain) were donated to the 3D Slicer project by the persons visible in the
  images, to be used without restrictions, and are covered by the same BSD-style
  license as 3D Slicer itself (per the Slicer maintainers on the official forum).
  This permits redistribution, including bundling into this application, with
  attribution retained.
- **Project:** https://www.slicer.org — © the 3D Slicer contributors.
- **Modifications:** the original `.nrrd` (gzip-encoded, 256×256×130, 16-bit signed,
  little-endian) was decompressed to a headerless RAW of the same dimensions/format
  for loading by `VolumeFileLoader`. No voxel values were altered.

## CThead — LOCAL TEST ONLY, NOT REDISTRIBUTED

`CThead_*.raw` (Stanford voldata) is **personal-use only** — its license forbids
redistribution/mirroring. It is git-ignored and is **never** committed or shipped.
It exists only as a local test volume on a developer machine.

---

This application is an educational/research tool and is **not for diagnosis or
clinical decision-making**.
