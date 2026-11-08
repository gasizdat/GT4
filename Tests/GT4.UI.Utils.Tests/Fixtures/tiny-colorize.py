# Builds tiny-colorize.onnx: python tiny-colorize.py tiny-colorize.onnx
# Needs pip install -r requirements.txt, whose onnx reproduces the committed file byte for byte.
import sys
import onnx
from onnx import TensorProto, helper

# Returns the input's first two channels as the ab output, standing in for ddcolor-tiny-fp16.onnx.
input = helper.make_tensor_value_info("input", TensorProto.FLOAT, [1, 3, 512, 512])
output = helper.make_tensor_value_info("output", TensorProto.FLOAT, [1, 2, 512, 512])
starts = helper.make_tensor("starts", TensorProto.INT64, [1], [0])
ends = helper.make_tensor("ends", TensorProto.INT64, [1], [2])
axes = helper.make_tensor("axes", TensorProto.INT64, [1], [1])
nodes = [
    helper.make_node("Slice", ["input", "starts", "ends", "axes"], ["output"], name="slice_ab"),
]
graph = helper.make_graph(nodes, "tiny_colorize", [input], [output], [starts, ends, axes])
model = helper.make_model(graph, opset_imports=[helper.make_opsetid("", 19)])
model.ir_version = 9
onnx.checker.check_model(model)
onnx.save(model, sys.argv[1])
