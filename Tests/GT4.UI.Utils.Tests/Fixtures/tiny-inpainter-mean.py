# Builds tiny-inpainter-mean.onnx: python tiny-inpainter-mean.py tiny-inpainter-mean.onnx
# Needs the onnx package; onnx 1.23.2 reproduces the committed file byte for byte.
import sys
import onnx
from onnx import TensorProto, helper

# Fills every pixel with the tile's per-channel mean, scaled to 0-255 like lama_fp32.onnx's output,
# so two tiles over different content fill differently.
image = helper.make_tensor_value_info("image", TensorProto.FLOAT, [1, 3, 512, 512])
mask = helper.make_tensor_value_info("mask", TensorProto.FLOAT, [1, 1, 512, 512])
output = helper.make_tensor_value_info("output", TensorProto.FLOAT, [1, 3, 512, 512])
scale = helper.make_tensor("scale", TensorProto.FLOAT, [], [255.0])
zero = helper.make_tensor("zero", TensorProto.FLOAT, [], [0.0])
nodes = [
    helper.make_node("ReduceMean", ["image"], ["mean"], axes=[2, 3], keepdims=1),
    helper.make_node("Mul", ["mean", "scale"], ["scaled"]),
    helper.make_node("Mul", ["image", "zero"], ["zeros"]),
    helper.make_node("Mul", ["mask", "zero"], ["maskZeros"]),
    helper.make_node("Add", ["zeros", "maskZeros"], ["allZeros"]),
    helper.make_node("Add", ["allZeros", "scaled"], ["output"]),
]
graph = helper.make_graph(nodes, "tiny-inpainter-mean", [image, mask], [output], [scale, zero])
model = helper.make_model(graph, opset_imports=[helper.make_opsetid("", 13)])
model.ir_version = 7
onnx.checker.check_model(model)
onnx.save(model, sys.argv[1])
