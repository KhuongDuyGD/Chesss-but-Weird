Shader "Chess/Buff Prop Ink"
{
    Properties { _Color("Paint", Color)=(.2,.2,.2,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END
            struct A { float4 pos:POSITION; float3 normal:NORMAL; };
            struct V { float4 pos:SV_POSITION; half3 normal:TEXCOORD0; };
            V vert(A i){V o;o.pos=TransformObjectToHClip(i.pos.xyz);o.normal=TransformObjectToWorldNormal(i.normal);return o;}
            half4 frag(V i):SV_Target {Light light=GetMainLight();half diffuse=saturate(dot(normalize(i.normal),light.direction));return half4(_Color.rgb*(.48h+diffuse*.52h)*lerp(half3(1,1,1),light.color,.35h),1);}
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct A {float4 pos:POSITION;float3 normal:NORMAL;};
            struct V {float4 pos:SV_POSITION;float3 normal:TEXCOORD0;};
            V vert(A i){V o;o.pos=UnityObjectToClipPos(i.pos);o.normal=UnityObjectToWorldNormal(i.normal);return o;}
            fixed4 frag(V i):SV_Target {return fixed4(_Color.rgb*(.48+.52*saturate(dot(normalize(i.normal),normalize(float3(-.3,1,-.4))))),1);}
            ENDCG
        }
    }
}
