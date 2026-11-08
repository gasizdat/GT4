# Builds tiny-upscale-x4.onnx: python tiny-upscale-x4.py tiny-upscale-x4.onnx
# Needs the onnx package; onnx 1.23.2 reproduces the committed file byte for byte.
import sys
import onnx
from onnx import TensorProto, helper

# Nearest-neighbour x4 of any NCHW input, standing in for realesr-general-x4v3.onnx.
input = helper.make_tensor_value_info("input", TensorProto.FLOAT, ["batch", "channels", "height", "width"])
output = helper.make_tensor_value_info("output", TensorProto.FLOAT, ["batch", "channels", "height4", "width4"])
scales = helper.make_tensor("scales", TensorProto.FLOAT, [4], [1.0, 1.0, 4.0, 4.0])
nodes = [
    helper.make_node(
        "Resize",
        ["input", "", "scales"],
        ["output"],
        name="resize_x4",
        mode="nearest",
        coordinate_transformation_mode="half_pixel",
        nearest_mode="round_prefer_floor",
    ),
]
graph = helper.make_graph(nodes, "tiny_upscale_x4", [input], [output], [scales])
model = helper.make_model(graph, opset_imports=[helper.make_opsetid("", 19)])
model.ir_version = 9
onnx.checker.check_model(model)
onnx.save(model, sys.argv[1])
