#ifndef KR_WIND_INCLUDED
#define KR_WIND_INCLUDED
float _KRWindStrength;
float3 KRWindPosition(float3 p)
{
    // Imported FBX meshes retain Blender's local axes: animate in world space.
    float3 world = TransformObjectToWorld(p);
    float scale = max(0.001, length(TransformObjectToWorldDir(float3(1,0,0), false)));
    float height = (world.y - TransformObjectToWorld(float3(0,0,0)).y) / scale;
    float weight = smoothstep(0.0, 2.0, height);
    float phase = (world.x * 0.63 + world.z * 0.41) / scale;
    float sway = sin(_Time.y * 0.78 + phase) + 0.28 * sin(_Time.y * 1.23 + phase * 1.7);
    world.xz += float2(0.82, 0.38) * (sway * 0.027 * weight * _KRWindStrength * scale);
    return TransformWorldToObject(world);
}
#endif
