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

## Model loading and caching

`ModelDescriptor(FileName, Url, Sha256)` (`UI/Utils/Abstraction`) pins a model's identity;
`Sha256` is the uppercase hex digest and is compared case-insensitively.

- The cache file name is `ModelDescriptor.CacheFileName`: the first 16 hex digits of `Sha256`, a
  dash, then `FileName`. A model whose bytes change therefore lands under a new name even when its
  `FileName` doesn't, so the release checklist below needs no file-renaming rule.
- `ModelDownloader.EnsureModelAsync` (`UI/Utils/ModelDownloader.cs`) downloads to
  `<cache name>.download` beside the final cache path, hashes the downloaded bytes, and only renames
  it into place on a match (`fileSystem.Move`, atomic). A mismatch throws `ModelDownloadException`
  and leaves no partial file at the final name. If the final file already exists, it's trusted
  without re-hashing — the integrity check runs once, at download time, never again on every load.
- Every enhancer is also an `IModelDefinition` listing its `ModelDescriptor`s; both roles are
  registered together by `AddPhotoEnhancement<T>`. At startup `ModelCacheSweep` (an
  `IMauiInitializeService`, so it runs inside `MauiApp.Build` before any page exists) calls
  `ModelDownloader.RemoveUnusedModels`, which deletes every file in the cache that isn't a listed
  model's cache name or its `.download` — the latter kept because the app isn't single-instance and
  another instance may be mid-download. This is what removes a retired model (e.g. the GFPGAN/YuNet
  pair) or a replaced model's previous bytes.
- `ModelSessionFactory.LoadSessionAsync` (`UI/Utils/Platforms/Windows/ModelSessionFactory.cs`)
  opens `InferenceSession` from the cached file's path, not a buffered stream — matters once a
  model gets into the hundreds of MB (LaMa is ~208MB); a `MemoryStream` copy would transiently
  double that.
- Cache location: `IStorage.Models`, under `%APPDATA%\{app GUID}\.models`.
- All three Windows `IPhotoEnhancement`s are registered `AddSingleton` and lazily
  `??=` their `InferenceSession`(s) on first use, so a loaded session lives for the app's
  lifetime. That `??=` isn't guarded against a concurrent first-use race — a known, accepted,
  pre-existing pattern shared by all three enhancers, not something to fix in just one of them.
- `RestorePhotoEnhancement` only loads the inpainter session if the detector found any damage
  (`HasAnyDamage`) — an undamaged photo never pays LaMa's load cost.
- Perf: LaMa measured ~2.2s per 512x512 tile in Release with default `SessionOptions`. A first-ever
  Debug run on one machine took ~15 minutes once — a one-time JIT/cold-start anomaly, not a
  sustained cost; re-running under the same conditions matched the Release number.

## Publishing a model release

Convention: one `models-vN` GitHub release per asset *generation*, not per model —
`models-v1` is Upscale + Colorize's weights, `models-v2` is Restore's. A later change to any one
model's bytes gets a new tag (`models-v3`, ...); never `gh release upload --clobber` onto an
existing tag, and never delete an old release or its assets — a shipped build pins its own
`ModelDescriptor.Sha256` against the tag current when it was built, so old tags must keep
resolving. Per-asset convention: `<name>.onnx` plus `<name>.LICENSE.txt` (the upstream project's
actual LICENSE text, not a paraphrase), plus a NOTICE file where the license requires attribution
beyond the license text itself.

Checklist for adding or replacing a model:

1. Produce the final `.onnx` bytes — either converted from a checkpoint via
   `Tools/GT4.Tools.ModelExport` (below) or downloaded ready-made from a trustworthy ONNX export.
2. `Get-FileHash -Algorithm SHA256 <file>` — this value becomes `ModelDescriptor.Sha256`.
3. Write `<name>.LICENSE.txt`.
4. **Ask the user before uploading anything.** This is the standing never-publish-without-asking
   rule, and a model release also carries its own licensing judgment calls the owner needs to
   weigh (e.g. BOPBTL's non-binding "research only" README note, LaMa's Places2 training-data
   question) — a prior "let's try this approach" is not the same sign-off as "publish these
   bytes."
5. `gh release create models-vN --title ... --notes ...`, then `gh release upload models-vN
   <file1> <file2> ...`.
6. Update the `ModelDescriptor` literals in code: `FileName`, `Url` (pointing at the new tag),
   `Sha256`.
7. Re-download from the real published URL and re-hash it, confirming it matches what's now
   pinned in code — this closes the loop on "did the upload actually produce the bytes I hashed,"
   not just "did I hash the right local file before uploading."

## Converting a PyTorch checkpoint (`Tools/GT4.Tools.ModelExport`)

Windows-only console tool (TorchSharp CPU + `TorchSharp.PyBridge` + `Onnxify.TorchSharp`), in
`GT4.Windows.slnf` but deliberately not in any CI filter — it's a one-off conversion aid with
heavy third-party dependencies that have no reason to be restored on every CI run. Usage:
`GT4.Tools.ModelExport <checkpoint.pt> <output.onnx>`.

- **A legacy (pre-1.6, non-zip) pickle checkpoint must be re-saved first.** `PyBridge.load_py`
  flatly rejects that format (`NotImplementedException`). Fix it once with Python:
  `torch.save(torch.load("old.pt", weights_only=False), "new.pt")`.
- **Module field names must mirror the original Python attribute names exactly**, not C#
  convention (`down_sample`, not `DownSample`) — `PyBridge` matches `state_dict` keys by name.
- **Fixed, non-learned buffers don't load from the checkpoint.** `load_py(strict: false,
  loadedParameters: ...)` returns which parameters loaded; the scratch detector's own antialiasing
  filters are exactly the ones that don't, and get set analytically afterward
  (`SetAntialiasingFilters`). The one real bug this process caught: the first attempt set that
  filter on a `Conv2d` without `groups: channels`, which silently broadcast it into a full dense
  convolution instead of a depthwise one — a numerical parity check against the real upstream
  PyTorch model is what caught it, plausibility alone would not have.
- **Always verify numerically against the real upstream model before trusting an export** — not
  just "it runs and produces a plausible-looking output."
  `Tools/GT4.Tools.ModelExport/verify_parity.py` automates this for the scratch detector: an
  independent PyTorch reimplementation (copied from the real upstream source, not reusing the
  TorchSharp port's own code, so a bug shared by both wouldn't be masked) compared against the
  exported ONNX via `onnxruntime`, across a few input sizes plus one at or below the
  architecture's known minimum size, so a legitimate failure mode is confirmed to match too, not
  just the happy path. Run manually — `python verify_parity.py <checkpoint.pt> <exported.onnx>`,
  needs `torch`+`onnxruntime` locally — there's no CI path for it, same reasoning as the tool
  itself. The scratch detector's own conversion lands at ~2-4e-5 max absolute difference (float32
  noise floor) at every size that's expected to work.

## Known gap: test-fixture generators aren't committed

`Tests/GT4.UI.Utils.Tests/Fixtures/*.onnx` (including `tiny-detector-identity.onnx` and
`tiny-inpainter-constant.onnx`) are committed as binaries, but the `Onnxify`-based script that
built them only ever existed in a scratchpad and is gone now — the same gap noted on #528's
fixtures. If one of these fixtures ever needs to change, its generator has to be rebuilt from
scratch against `Onnxify`'s graph API, not edited in place.
