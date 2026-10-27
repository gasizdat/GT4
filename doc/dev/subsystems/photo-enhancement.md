# Subsystem: photo enhancement (issue #506)

`UI/Utils/Platforms/Windows/*PhotoEnhancement.cs` implements `IPhotoEnhancement` against local
ONNX models via `Microsoft.ML.OnnxRuntime`, Windows-only (`GT4.UI.Utils` multi-targets
android/ios/maccatalyst/windows, so every implementation is `#if WINDOWS`-gated and left
unregistered everywhere else). Each one is an independent, single-step image transform the add-photo
flow offers as a button; none of them chain automatically. This page sketches the *approach* each
takes, not a line-by-line trace — read the class for that.

## Upscale — `UpscalePhotoEnhancement.cs`

Model: Real-ESRGAN `realesr-general-x4v3` (SRVGGNetCompact weights). Decode the photo, normalize
RGB to `[0, 1]` into a CHW tensor, and run the *whole* image through the model in one pass — the
model's input/output axes are dynamic, so there's no tiling or batching. Denormalize the 4x-larger
output tensor back into a bitmap.

## Colorize — `ColorizePhotoEnhancement.cs`

Model: DDColor-tiny (fp16). Split the photo into CIE Lab's `L` (lightness) channel at full
resolution, downsample it to the model's fixed 512x512 input, and run it — DDColor predicts
*chroma only* (`a`/`b`), never touching luminance. Recombine the original full-resolution `L` with
the upsampled predicted chroma in Lab space and convert back to sRGB. Tonal detail is therefore
always the original photo's; the model only ever adds color.

## Restore — `RestorePhotoEnhancement.cs`

General structural damage repair (scratches, creases, tears, dust), not face-specific — the
originally-scoped #506 design, after GFPGAN's face-restoration substitute was found to carry a
genuine license defect (confirmed from TencentARC/GFPGAN's own `LICENSE` file: its StyleGAN2-
derived generator is restricted to non-commercial research/evaluation under NVIDIA's license,
independent of the repo's Apache-2.0 header). See issue #506's comment thread for the full
investigation and the alternatives considered.

Two models in sequence, over the whole photo, not per-face:

1. **Detect** — a U-Net scratch detector (`scratch-detector.onnx`, converted from Microsoft's
   Bringing-Old-Photos-Back-to-Life project's `FT_Epoch_latest.pt` checkpoint by
   `Tools/GT4.Tools.ModelExport`) produces a binary damage mask: grayscale, resized so the short
   side is 256 (rounded to a multiple of 16), sigmoid + a 0.4 threshold, then resized back to the
   photo's full resolution with nearest-neighbor.
2. **Inpaint, composite** — LaMa (`lama-fp32.onnx`, `Carve/LaMa-ONNX`'s `lama_fp32.onnx`) fills
   damage at its fixed 512x512 input, tiled across the photo at native resolution (never resizing
   the whole photo through the model) with reflect-padding at the edges; tiles with no mask pixels
   are skipped. `RestorePhotoEnhancement.CompositeMaskedPixels` then takes the inpainted value only
   where the mask marks damage — every other pixel stays exactly the source pixel.

Model licenses: the scratch detector's code and weights are MIT (Microsoft's own README also
carries a non-binding "academic research use only" note, confirmed not to be a second enforceable
license term); LaMa is Apache-2.0, trained on Places2, whose dataset-redistribution terms raise an
unresolved (industry-wide, not GT4-specific) question about whether that restriction also binds a
model trained on it.
