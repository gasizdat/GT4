"""Verifies a GT4.Tools.ModelExport ONNX export against the real upstream PyTorch model.

Run manually (not part of CI or the publish checklist's automated steps) after exporting or
re-exporting the scratch detector, before publishing it. Needs a local Python with torch and
onnxruntime installed -- see doc/dev/subsystems/photo-enhancement.md for the environment and
checkpoint-sourcing notes.

Usage: python verify_parity.py <checkpoint.pt> <exported.onnx>

The reference UNet/UNetConvBlock/UNetUpBlock/Downsample classes below are copied verbatim (minus
the sync_bn=True DataParallelWithCallback wrapping, which this script doesn't need) from
microsoft/Bringing-Old-Photos-Back-to-Life's Global/detection_models/networks.py and
antialiasing.py (MIT License, Copyright (c) Microsoft Corporation) -- an independent reference,
not GT4.Tools.ModelExport's own TorchSharp reimplementation, so a bug shared by both wouldn't be
masked.
"""

import sys

import numpy as np
import onnxruntime as ort
import torch
import torch.nn as nn
import torch.nn.functional as F


class Downsample(nn.Module):
    def __init__(self, pad_type="reflect", filt_size=3, stride=2, channels=None):
        super().__init__()
        a = np.array([1.0, 2.0, 1.0])
        filt = torch.Tensor(a[:, None] * a[None, :])
        filt = filt / torch.sum(filt)
        self.register_buffer("filt", filt[None, None, :, :].repeat((channels, 1, 1, 1)))
        self.pad = nn.ReflectionPad2d(1)
        self.stride = stride

    def forward(self, inp):
        return F.conv2d(self.pad(inp), self.filt, stride=self.stride, groups=inp.shape[1])


class UNetConvBlock(nn.Module):
    def __init__(self, conv_num, in_size, out_size, padding, batch_norm):
        super().__init__()
        block = []
        for _ in range(conv_num):
            block.append(nn.ReflectionPad2d(padding=int(padding)))
            block.append(nn.Conv2d(in_size, out_size, kernel_size=3, padding=0))
            if batch_norm:
                block.append(nn.BatchNorm2d(out_size))
            block.append(nn.LeakyReLU(0.2, True))
            in_size = out_size
        self.block = nn.Sequential(*block)

    def forward(self, x):
        return self.block(x)


class UNetUpBlock(nn.Module):
    def __init__(self, conv_num, in_size, out_size, up_mode, padding, batch_norm):
        super().__init__()
        assert up_mode == "upsample"
        self.up = nn.Sequential(
            nn.Upsample(mode="bilinear", scale_factor=2, align_corners=False),
            nn.ReflectionPad2d(1),
            nn.Conv2d(in_size, out_size, kernel_size=3, padding=0),
        )
        self.conv_block = UNetConvBlock(conv_num, in_size, out_size, padding, batch_norm)

    def center_crop(self, layer, target_size):
        _, _, layer_height, layer_width = layer.size()
        diff_y = (layer_height - target_size[0]) // 2
        diff_x = (layer_width - target_size[1]) // 2
        return layer[:, :, diff_y:(diff_y + target_size[0]), diff_x:(diff_x + target_size[1])]

    def forward(self, x, bridge):
        up = self.up(x)
        crop1 = self.center_crop(bridge, up.shape[2:])
        out = torch.cat([up, crop1], 1)
        return self.conv_block(out)


class UNet(nn.Module):
    def __init__(self, in_channels, out_channels, depth, conv_num, wf, padding, batch_norm, up_mode, with_tanh):
        super().__init__()
        self.first = nn.Sequential(
            nn.ReflectionPad2d(3), nn.Conv2d(in_channels, 2 ** wf, kernel_size=7), nn.LeakyReLU(0.2, True)
        )
        prev_channels = 2 ** wf

        self.down_path = nn.ModuleList()
        self.down_sample = nn.ModuleList()
        for i in range(depth):
            self.down_sample.append(
                nn.Sequential(
                    nn.ReflectionPad2d(1),
                    nn.Conv2d(prev_channels, prev_channels, kernel_size=3, stride=1, padding=0),
                    nn.BatchNorm2d(prev_channels),
                    nn.LeakyReLU(0.2, True),
                    Downsample(channels=prev_channels, stride=2),
                )
            )
            self.down_path.append(UNetConvBlock(conv_num, prev_channels, 2 ** (wf + i + 1), padding, batch_norm))
            prev_channels = 2 ** (wf + i + 1)

        self.up_path = nn.ModuleList()
        for i in reversed(range(depth)):
            self.up_path.append(UNetUpBlock(conv_num, prev_channels, 2 ** (wf + i), up_mode, padding, batch_norm))
            prev_channels = 2 ** (wf + i)

        self.last = (
            nn.Sequential(nn.ReflectionPad2d(1), nn.Conv2d(prev_channels, out_channels, kernel_size=3), nn.Tanh())
            if with_tanh
            else nn.Sequential(nn.ReflectionPad2d(1), nn.Conv2d(prev_channels, out_channels, kernel_size=3))
        )

    def forward(self, x):
        x = self.first(x)
        blocks = []
        for i, down_block in enumerate(self.down_path):
            blocks.append(x)
            x = self.down_sample[i](x)
            x = down_block(x)
        for i, up in enumerate(self.up_path):
            x = up(x, blocks[-i - 1])
        return self.last(x)


def main():
    checkpoint_path, onnx_path = sys.argv[1], sys.argv[2]

    checkpoint = torch.load(checkpoint_path, map_location="cpu", weights_only=False)
    state_dict = checkpoint["model_state"] if "model_state" in checkpoint else checkpoint

    model = UNet(
        in_channels=1, out_channels=1, depth=4, conv_num=2, wf=6,
        padding=True, batch_norm=True, up_mode="upsample", with_tanh=False,
    )
    model.load_state_dict(state_dict, strict=True)
    model.eval()

    session = ort.InferenceSession(onnx_path, providers=["CPUExecutionProvider"])
    input_name = session.get_inputs()[0].name
    output_name = session.get_outputs()[0].name

    sizes = [(32, 32), (256, 256), (48, 112)]
    for h, w in sizes:
        torch.manual_seed(0)
        x = torch.rand(1, 1, h, w) * 2.0 - 1.0

        with torch.no_grad():
            reference = model(x).numpy()
        exported = session.run([output_name], {input_name: x.numpy()})[0]

        max_diff = np.abs(reference - exported).max()
        print(f"{w}x{h}: max abs diff = {max_diff:.2e}")

    # Below the architecture's minimum size (depth-4 downsampling + padding=1 on 3x3 convs):
    # both the reference and the export are expected to fail the same way, not just the export.
    x = torch.rand(1, 1, 16, 16) * 2.0 - 1.0
    reference_failed = False
    try:
        with torch.no_grad():
            model(x)
    except RuntimeError:
        reference_failed = True
    exported_failed = False
    try:
        session.run([output_name], {input_name: x.numpy()})
    except Exception:
        exported_failed = True
    print(f"16x16 (below minimum): reference failed = {reference_failed}, export failed = {exported_failed}")
    if reference_failed != exported_failed:
        print("MISMATCH: the export's edge-case failure mode doesn't match the reference's.")
        sys.exit(1)


if __name__ == "__main__":
    main()
