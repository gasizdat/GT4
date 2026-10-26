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
output tensor back into a bitmap. No face-specific handling: it shares the same known failure mode
as the restore model below on extreme-angle or low-confidence faces.

## Colorize — `ColorizePhotoEnhancement.cs`

Model: DDColor-tiny (fp16). Split the photo into CIE Lab's `L` (lightness) channel at full
resolution, downsample it to the model's fixed 512x512 input, and run it — DDColor predicts
*chroma only* (`a`/`b`), never touching luminance. Recombine the original full-resolution `L` with
the upsampled predicted chroma in Lab space and convert back to sRGB. Tonal detail is therefore
always the original photo's; the model only ever adds color.

## Restore — `RestorePhotoEnhancement.cs`

Two models in sequence, per detected face, not whole-image:

1. **Detect** — YuNet (`yunet-2023mar.onnx`) finds faces and landmarks. Its decode logic is ported
   from OpenCV's `face_detect.cpp` rather than run through the ONNX graph itself (`YuNetFaceDetector`).
2. **Align, restore, paste back** — each face's landmarks are fit to facexlib's FFHQ template with a
   least-squares similarity transform (`FaceAligner`), cropped/warped to the 512x512 GFPGAN v1.4
   expects, and restored. The restored crop is pasted back with an analytic distance-to-edge feather
   standing in for facexlib's raster erode-then-blur mask — chosen specifically to avoid depending on
   facexlib's separately licensed face-parsing model. Everything outside detected face regions is
   untouched.

See the commit introducing this file for the detector/weights license breakdown (YuNet: MIT;
GFPGAN: Apache-2.0 code/weights, but trained against StyleGAN2, whose license restricts the Work to
non-commercial research/evaluation — a deferred question, not resolved by this doc).
