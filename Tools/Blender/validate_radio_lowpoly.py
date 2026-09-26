from pathlib import Path

import bpy


required = {"Radio_Body", "Frequency_Display", "Tuning_Knob_Large", "Volume_Knob_Small"}
objects = {obj.name: obj for obj in bpy.data.objects if obj.type == "MESH"}
missing = required.difference(objects)
if missing:
    raise RuntimeError(f"Missing required objects: {sorted(missing)}")

depsgraph = bpy.context.evaluated_depsgraph_get()
triangles = 0
for obj in objects.values():
    if not obj.data.uv_layers:
        raise RuntimeError(f"Object has no exportable UV map: {obj.name}")
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    mesh.calc_loop_triangles()
    triangles += len(mesh.loop_triangles)
    evaluated.to_mesh_clear()

if triangles > 10000:
    raise RuntimeError(f"Triangle budget exceeded: {triangles}")

print(f"VALIDATION_OK objects={len(objects)} evaluated_triangles={triangles}")
print("REQUIRED_OBJECTS=" + ",".join(sorted(required)))
print("FILE=" + str(Path(bpy.data.filepath)))
