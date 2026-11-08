# Builds tiny-detector-identity.onnx: python tiny-detector-identity.py tiny-detector-identity.onnx
# Needs pip install -r requirements.txt, whose onnx reproduces the committed file byte for byte.
import sys
import onnx
from onnx import TensorProto, helper

# Passes the input through, standing in for scratch-detector.onnx, so the damage mask follows the
# photo's own brightness: white reads as damage, black as clean.
input = helper.make_tensor_value_info("input", TensorProto.FLOAT, [1, 1, "h", "w"])
output = helper.make_tensor_value_info("output", TensorProto.FLOAT, [1, 1, "h", "w"])
nodes = [
    helper.make_node("Identity", ["input"], ["output"], name="passthrough"),
]
graph = helper.make_graph(nodes, "tiny_detector_identity", [input], [output])
model = helper.make_model(graph, opset_imports=[helper.make_opsetid("", 19)])
model.ir_version = 9
onnx.checker.check_model(model)
onnx.save(model, sys.argv[1])
