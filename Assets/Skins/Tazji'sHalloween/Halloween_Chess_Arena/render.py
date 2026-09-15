import bpy
bpy.context.window.scene=bpy.data.scenes['Halloween Arena']
bpy.ops.render.render(write_still=True)
