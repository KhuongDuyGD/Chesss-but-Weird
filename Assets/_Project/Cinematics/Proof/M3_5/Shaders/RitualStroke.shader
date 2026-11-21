Shader "Chess But Weird/M3.5/Ritual Stroke"
{
    Properties { _Accent("Ink",Color)=(1,.65,.22,1) _Strength("Restrained emission",Range(0,1))=.3 }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10"}
        Pass
        {
            Name "InkStroke" Tags{"LightMode"="UniversalForwardOnly"} Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _Accent;half _Strength; CBUFFER_END
            struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;};
            V Vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);return o;}half4 Frag(V i):SV_Target{return half4(_Accent.rgb*(.40+_Strength),1);}
            ENDHLSL
        }
    }
}
