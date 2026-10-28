using Onnxify;
using Onnxify.TorchSharp;
using TorchSharp.Modules;
using TorchSharp.PyBridge;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;
using Tensor = TorchSharp.torch.Tensor;

if (args.Length != 2)
{
  Console.Error.WriteLine("Usage: GT4.Tools.ModelExport <checkpoint.pt> <output.onnx>");
  return 1;
}

var checkpointPath = args[0];
var outputPath = args[1];

var model = new ScratchDetectorUNet();
model.eval();

var loadedParameters = new Dictionary<string, bool>();
model.load_py(checkpointPath, strict: false, loadedParameters: loadedParameters);

var missing = loadedParameters.Where(p => !p.Value).Select(p => p.Key).ToArray();
Console.WriteLine($"Loaded {loadedParameters.Count - missing.Length}/{loadedParameters.Count} parameters from checkpoint.");
Console.WriteLine($"Not loaded from checkpoint ({missing.Length}), expected to be exactly the {ScratchDetectorUNet.DownSampleStages} fixed antialiasing filters:");
foreach (var name in missing)
{
  Console.WriteLine($"  {name}");
}

model.SetAntialiasingFilters();

var onnxModel = model.Export();
await onnxModel.SaveAsync(outputPath, overwrite: true);
Console.WriteLine($"Exported to {outputPath}");
return 0;

// Reimplements microsoft/Bringing-Old-Photos-Back-to-Life's Global/detection_models/networks.py
// UNet, constructed exactly as Global/detection.py instantiates it:
// UNet(in_channels=1, out_channels=1, depth=4, conv_num=2, wf=6, padding=True, batch_norm=True,
// up_mode="upsample", with_tanh=False, antialiasing=True). Field names mirror the Python
// attribute names exactly (not C# convention) so TorchSharp.PyBridge's name-based state_dict
// matching lines up with the original checkpoint.
sealed class ScratchDetectorUNet : Module
{
  public const int DownSampleStages = 4;
  private const int Wf = 6;

  public readonly Module<Tensor, Tensor> first;
  public readonly ModuleList<Module<Tensor, Tensor>> down_sample;
  public readonly ModuleList<ConvBlock> down_path;
  public readonly ModuleList<UpBlock> up_path;
  public readonly Module<Tensor, Tensor> last;

  public ScratchDetectorUNet() : base(nameof(ScratchDetectorUNet))
  {
    first = Sequential(ReflectionPad2d(3L), Conv2d(1L, 1L << Wf, 7L), LeakyReLU(0.2, inplace: true));

    var downSample = new List<Module<Tensor, Tensor>>();
    var downPath = new List<ConvBlock>();
    var prevChannels = 1L << Wf;
    for (var i = 0; i < DownSampleStages; i++)
    {
      downSample.Add(Sequential(
        ReflectionPad2d(1L),
        Conv2d(prevChannels, prevChannels, 3L, stride: 1L, padding: 0L),
        BatchNorm2d(prevChannels),
        LeakyReLU(0.2, inplace: true),
        ReflectionPad2d(1L),
        Conv2d(prevChannels, prevChannels, 3L, stride: 2L, padding: 0L, groups: prevChannels, bias: false)
      ));
      var outChannels = 1L << (Wf + i + 1);
      downPath.Add(new ConvBlock(prevChannels, outChannels));
      prevChannels = outChannels;
    }
    down_sample = ModuleList(downSample.ToArray());
    down_path = ModuleList(downPath.ToArray());

    var upPath = new List<UpBlock>();
    for (var i = DownSampleStages - 1; i >= 0; i--)
    {
      var outChannels = 1L << (Wf + i);
      upPath.Add(new UpBlock(prevChannels, outChannels));
      prevChannels = outChannels;
    }
    up_path = ModuleList(upPath.ToArray());

    last = Sequential(ReflectionPad2d(1L), Conv2d(prevChannels, 1L, 3L));

    RegisterComponents();
  }

  public void SetAntialiasingFilters()
  {
    using var _ = no_grad();
    for (var i = 0; i < DownSampleStages; i++)
    {
      var channels = 1L << (Wf + i);
      var conv = (Conv2d)((Sequential)down_sample[i])[5];
      conv.weight!.copy_(AntialiasingKernel(channels));
    }
  }

  // detection_models/antialiasing.py's Downsample(filt_size=3): outer([1,2,1],[1,2,1])/16,
  // repeated per channel, applied as a fixed (non-learned) depthwise convolution.
  private static Tensor AntialiasingKernel(long channels)
  {
    var a = tensor(new float[] { 1f, 2f, 1f });
    var filt = outer(a, a);
    filt /= filt.sum();
    return filt.unsqueeze(0).unsqueeze(0).repeat(channels, 1, 1, 1);
  }

  public OnnxModel Export()
  {
    var onnxModel = OnnxModel.Create(new OnnxModelCreationOptions { Opset = 19, IrVersion = 9 });
    var graph = onnxModel.Graph;
    var input = graph.AddInput("input", OnnxTensorType.Create<float>([1, 1, "h", "w"]));

    var x = first.Export(graph, input);
    var bridges = new IOnnxGraphEdge[DownSampleStages];
    for (var i = 0; i < DownSampleStages; i++)
    {
      bridges[i] = x;
      x = down_sample[i].Export(graph, x);
      x = down_path[i].block.Export(graph, x);
    }

    for (var i = 0; i < DownSampleStages; i++)
    {
      var bridge = bridges[DownSampleStages - 1 - i];
      var upped = up_path[i].up.Export(graph, x);

      // center_crop: match Global/detection_models/networks.py's UNetUpBlock.center_crop exactly
      // -- crop the bridge (always >= upped in spatial size here, since `padding=True` keeps
      // shapes aligned) to upped's shape, symmetric offset.
      var croppedBridge = CenterCrop(graph, bridge, upped, $"crop_{i}");

      var merged = graph.Concat($"merge_{i}", new ConcatInputOptions { In = [upped, croppedBridge], Axis = 1 });
      x = up_path[i].conv_block.block.Export(graph, merged);
    }

    var output = last.Export(graph, x);
    var outputEdge = graph.AddEdge("output");
    graph.Identity("publish_output", new IdentityInputOutputOptions { Input = output, Output = outputEdge });
    graph.AddOutput("output", OnnxTensorType.Create<float>([1, 1, "oh", "ow"]));

    return onnxModel;
  }

  // Both `padding=True` (every conv keeps its input's HxW) and the fixed depth-4, stride-2
  // antialiased downsampling mean bridge and upped always already match spatially at this
  // UNet's configuration -- but build a real dynamic-shape Slice, not an assumption, since a
  // future caller could pass a size where that drifts by a pixel from odd-size rounding.
  private static IOnnxGraphEdge CenterCrop(OnnxGraph graph, IOnnxGraphEdge bridge, IOnnxGraphEdge upped, string name)
  {
    var bridgeShape = graph.Shape($"{name}_bridge_shape", new ShapeInputOptions { Data = bridge });
    var uppedShape = graph.Shape($"{name}_upped_shape", new ShapeInputOptions { Data = upped });
    var two = graph.AddTensor<long>($"{name}_two", [1], [2]);
    var diff = graph.Sub($"{name}_diff", new SubInputOptions { A = bridgeShape, B = uppedShape });
    var halfDiff = graph.Div($"{name}_half_diff", new DivInputOptions { A = diff, B = two });
    var starts = graph.Slice($"{name}_starts", new SliceInputOptions
    {
      Data = halfDiff,
      Starts = graph.AddTensor<long>($"{name}_starts_s", [1], [2]),
      Ends = graph.AddTensor<long>($"{name}_starts_e", [1], [4]),
      Axes = graph.AddTensor<long>($"{name}_starts_a", [1], [0]),
    });
    var ends = graph.Add($"{name}_ends", new AddInputOptions
    {
      A = starts,
      B = graph.Slice($"{name}_upped_hw", new SliceInputOptions
      {
        Data = uppedShape,
        Starts = graph.AddTensor<long>($"{name}_hw_s", [1], [2]),
        Ends = graph.AddTensor<long>($"{name}_hw_e", [1], [4]),
        Axes = graph.AddTensor<long>($"{name}_hw_a", [1], [0]),
      }),
    });
    return graph.Slice($"{name}_apply", new SliceInputOptions
    {
      Data = bridge,
      Starts = starts,
      Ends = ends,
      Axes = graph.AddTensor<long>($"{name}_axes", [2], [2, 3]),
    });
  }
}

sealed class ConvBlock : Module
{
  public readonly Module<Tensor, Tensor> block;

  public ConvBlock(long inSize, long outSize) : base(nameof(ConvBlock))
  {
    block = Sequential(
      ReflectionPad2d(1L),
      Conv2d(inSize, outSize, 3L, padding: 0L),
      BatchNorm2d(outSize),
      LeakyReLU(0.2, inplace: true),
      ReflectionPad2d(1L),
      Conv2d(outSize, outSize, 3L, padding: 0L),
      BatchNorm2d(outSize),
      LeakyReLU(0.2, inplace: true)
    );
    RegisterComponents();
  }
}

sealed class UpBlock : Module
{
  public readonly Module<Tensor, Tensor> up;
  public readonly ConvBlock conv_block;

  public UpBlock(long inSize, long outSize) : base(nameof(UpBlock))
  {
    up = Sequential(
      Upsample(mode: UpsampleMode.Bilinear, scale_factor: [2, 2], align_corners: false),
      ReflectionPad2d(1L),
      Conv2d(inSize, outSize, 3L, padding: 0L)
    );
    conv_block = new ConvBlock(inSize, outSize);
    RegisterComponents();
  }
}
