# Subsystem: photo enhancement (issue #506)

`UI/Utils/Platforms/Windows/*PhotoEnhancement.cs` implements `IPhotoEnhancement` against local
ONNX models via `Microsoft.ML.OnnxRuntime`, Windows-only (`GT4.UI.Utils` multi-targets
android/ios/maccatalyst/windows, so every implementation is `#if WINDOWS`-gated and left
unregistered everywhere else). Each one is an independent, single-step image transform, offered from
a photo's or image attachment's ✨ adorner menu in both the person dialog and the family dialog; none of them chain
automatically. This page sketches the *approach* each
takes, not a line-by-line trace — read the class for that.

An enhancer only ever sees decoded pixels: `ApplyAsync(SKBitmap)`. `PhotoEnhancementExtensions.EnhanceAsync`
wraps it for the `PhotoInfo` the photo's `IDataConverter` hands out (decode, enhance, re-encode as JPEG,
mark the caption). The decode applies the photo's EXIF orientation, so the copy is saved upright. `PhotoEnhancer` (`UI/App/Dialogs`), the flow both `CreateOrUpdatePersonDialog`
and `CreateOrUpdateNameDialog` call, turns the result back into a photo through that same converter.
The copy is therefore a new, uncommitted photo — never the source's stored `Id`, which would show the
source's cached image and collide with it on save. Never hand an enhancer `Data.Content`: a captioned photo's bytes are a
`GedcomPhotoResidue` envelope, not an image.

Every copy says it was AI-altered: its caption is the source's with the enhancement's localized
`CaptionMark` appended (`Ivan, 1952 (AI: colorized)`), or just the mark when the source has none. A
copy of a copy appends again, so its caption names every step in order and nothing parses an earlier
mark. The mark is an ordinary caption: it exports as GEDCOM `TITL` and the user can edit or remove it.
Because every copy is captioned, it is always saved in the Tagged category; whether it is a person or
a family photo comes from the source, since the converter stamps a person category on whatever it
envelopes.

An image attachment (a scanned letter, a certificate) gets the same ✨ adorner; a PDF or other file
doesn't. Its copy stays an attachment of the source's category, inserted right after it, and its title
takes the mark. `AttachmentDataConverter` encodes it from the `AttachmentInfo` whose `Image` is the
result, so the copy is typed by its JPEG bytes: `image/jpeg`, and the source's file name with a
`.jpeg` extension.

An enhancer refuses a photo it can't or needn't change by throwing `InvalidOperationException` with
a localized message, before any copy exists; `SafeCommand`'s error alert shows it, and nothing is
added.

## Upscale — `UpscalePhotoEnhancement.cs`

Model: Real-ESRGAN `realesr-general-x4v3` (SRVGGNetCompact weights). Normalize RGB to `[0, 1]` into
a CHW tensor and run the photo through the model in 512x512 tiles. A single pass would need about
1.1 GB of working memory per input megapixel; tiled, memory grows only with the 4x-larger result
bitmap. Each tile is padded with 34 px of its real neighbours, the reach of the model's 34 stacked
3x3 convolutions, so the stitched result is identical to a single pass, with no seams (measured
bit-exact against one). Denormalize each tile's output into the x4 bitmap, which has 16 times the
source's pixels. A photo over 1 MP (1024x1024) is therefore refused with an alert before the model
loads. Run time and the x4 bitmap's memory both grow with area: a 44 MP scan would need a 2.8 GB
bitmap, and on the CPU provider a 720x1000 photo took 9.9 s and a 790x1132 one 12.6 s (#583).

The x4 bitmap is then halved, as Real-ESRGAN's own outscale option does, so the copy is x2 and its
caption says so. That changes only what is stored and shown: the file is about 0.38 times x4's at
the same JPEG quality, and a 1 MP source still comes out about 2048 px on the long side.
Measurements are on #539. The filter is GT4's choice, not upstream's Lanczos: linear mipmaps,
which at exactly half are a true 2x2 average with no ringing. Side by side on real photos, Skia's
Mitchell cubic came out softer, and Catmull-Rom looked about the same as the average.

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
   photo's full resolution with nearest-neighbor. The shrink averages (linear mipmaps, as
   upstream's Pillow bicubic does), so dust specks too small to survive it go unflagged. The mask
   is then dilated, three passes of a 3x3 kernel as upstream's `Global/test.py` does, at the
   photo's full resolution, so the margin around a flagged line stays a few photo pixels at any
   photo size.
2. **Inpaint, composite** — LaMa (`lama-fp32.onnx`, `Carve/LaMa-ONNX`'s `lama_fp32.onnx`) fills
   damage at its fixed 512x512 input, tiled across the photo at native resolution (never resizing
   the whole photo through the model) with reflect-padding at the edges; tiles with no mask pixels
   are skipped. Tiles overlap by at least 128 px, and each tile's result fades out over the last
   128 px toward an edge with more photo beyond it, since LaMa saw nothing across that edge.
   Without the overlap, a repair crossing a tile edge showed a hard seam (#574): two tiles filled
   their halves independently, and damage touching a tile's edge was filled with invented colour.
   On a heavily cracked 1426x1928 photo, where every tile has damage, 20 tiles run instead of 12:
   74 s instead of 52 s, both including LaMa's 8 s load, measured for #574 before #583's speedup. `RestorePhotoEnhancement.CompositeMaskedPixels` then takes the inpainted value only
   where the mask marks damage — every other pixel stays exactly the source pixel.

A photo whose mask marks nothing is refused ("no damage was found") rather than returned as a
re-encoded duplicate of the source.

Model licenses: the scratch detector's code and weights are MIT (Microsoft's own README also
carries a non-binding "academic research use only" note, confirmed not to be a second enforceable
license term); LaMa is Apache-2.0, trained on Places2, whose dataset-redistribution terms raise an
unresolved (industry-wide, not GT4-specific) question about whether that restriction also binds a
model trained on it.

## Model loading and caching

`ModelDescriptor(FileName, Url, Sha256, DisplayName, License, Size)` (`UI/Utils/Abstraction`) pins
a model's identity and what the download prompt tells the user; `Sha256` is the uppercase hex digest
and is compared case-insensitively.

- The cache file name is `ModelDescriptor.CacheFileName`: the first 16 hex digits of `Sha256`, a
  dash, then `FileName`. A model whose bytes change therefore lands under a new name even when its
  `FileName` doesn't, so the release checklist below needs no file-renaming rule.
- `ModelDownloader.EnsureModelAsync` (`UI/Utils/ModelDownloader.cs`) downloads to
  `<cache name>.download` beside the final cache path, hashes the downloaded bytes, and only renames
  it into place on a match (`fileSystem.Move`, atomic). A mismatch throws `ModelDownloadException`
  and leaves no partial file at the final name. If the final file already exists, it's trusted
  without re-hashing — the integrity check runs once, at download time, never again on every load.
- Nothing downloads unasked. Before enhancing, `PhotoEnhancer` collects the
  enhancement's models that `IModelDownloader.IsDownloaded` doesn't find and asks
  (`IAlertService.ShowConfirmationAsync`), naming each with its license and size. On Yes it
  downloads them under their own `ProgressDialog`, then enhances; declining changes nothing.
  Restore asks for and downloads both of its models up front, though it only loads LaMa for a
  damaged photo.
- Every `IPhotoEnhancement` is an `IModelDefinition` listing its `ModelDescriptor`s, and
  `AddPhotoEnhancement<T>` registers it in both roles. At startup `ModelCacheSweep` (an
  `IMauiInitializeService`, so it runs inside `MauiApp.Build` before any page exists) calls
  `ModelDownloader.RemoveUnusedModels`, which deletes every file in the cache that isn't a listed
  model's cache name or its `.download` — the latter kept because the app isn't single-instance and
  another instance may be mid-download. This is what removes a retired model (e.g. the GFPGAN/YuNet
  pair) or a replaced model's previous bytes.
- `ModelSessionProvider.GetSessionAsync` (`UI/Utils/Platforms/Windows/ModelSessionProvider.cs`)
  opens `InferenceSession` from the cached file's path, not a buffered stream — matters once a
  model gets into the hundreds of MB (LaMa is ~208MB); a `MemoryStream` copy would transiently
  double that.
- Cache location: `IStorage.Models`, under `%APPDATA%\{app GUID}\.models`.
- `ModelSessionProvider` owns every `InferenceSession`. It caches one per model, and a gate makes
  a concurrent first load share a single session. The enhancers hold none themselves. A loaded
  session stays loaded until `PhotoEnhancer.ReleaseModels`. The person dialog and the family-name
  dialog call it as they complete, saved or cancelled. A person's own name dialogs can't enhance,
  so they don't call it. The photos enhanced in one dialog then pay each model's
  load once, and nothing stays held after it closes. Measured costs (#541): ORT's arena grows
  during the first `Run`, so a used session holds Upscale ~350 MB, Colorize ~2.1 GB, the
  detector ~530 MB and LaMa ~610 MB. Reloading costs 0.02 s, 0.45 s, 0.35 s and 7.7 s
  respectively. LaMa's load isn't graph optimization: it is still 6.2 s with optimization off.
  Closing a family-name dialog nested inside the person dialog releases the person dialog's
  sessions too, because the cache is shared.
- `RestorePhotoEnhancement` only loads the inpainter session if the detector found any damage
  (`HasAnyDamage`) — an undamaged photo is refused without paying LaMa's load cost.
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

## Test fixtures

The enhancer tests run real ONNX Runtime inference on tiny stand-in models in
`Tests/GT4.UI.Utils.Tests/Fixtures`, one per model plus `tiny-inpainter-mean.onnx` for Restore's
tile blending. Each `.onnx` has its generator beside it; what each stands in for is in the script's
header. To change one, edit its script, run it once `pip install -r requirements.txt` is done, and
commit the script and the regenerated `.onnx` together. Nothing regenerates them during a build:
the test project embeds the committed binaries, and CI doesn't check them against their scripts.
