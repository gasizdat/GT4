# Builds tiny-inpainter-constant.onnx: python tiny-inpainter-constant.py tiny-inpainter-constant.onnx
# Needs pip install -r requirements.txt, whose onnx reproduces the committed file byte for byte.
import sys
import onnx
from onnx import TensorProto, helper

# Fills every pixel with 128 on lama_fp32.onnx's 0-255 output scale. The mask is accepted but unused.
image = helper.make_tensor_value_info("image", TensorProto.FLOAT, [1, 3, 512, 512])
mask = helper.make_tensor_value_info("mask", TensorProto.FLOAT, [1, 1, 512, 512])
output = helper.make_tensor_value_info("output", TensorProto.FLOAT, [1, 3, 512, 512])
zero = helper.make_tensor("zero", TensorProto.FLOAT, [1], [0.0])
constant = helper.make_tensor("constant_value", TensorProto.FLOAT, [1], [128.0])
nodes = [
    helper.make_node("Mul", ["image", "zero"], ["zero_out_image_output_c"], name="zero_out_image"),
    helper.make_node("Add", ["zero_out_image_output_c", "constant_value"], ["output"], name="add_constant"),
]
graph = helper.make_graph(nodes, "tiny_inpainter_constant", [image, mask], [output], [zero, constant])
model = helper.make_model(graph, opset_imports=[helper.make_opsetid("", 19)])
model.ir_version = 9
onnx.checker.check_model(model)
onnx.save(model, sys.argv[1])
