Shader "ChessButWeird/KingdomRush/ElementalFlame"
{
    Properties
    {
        [HDR] _BaseColor("Magic color",Color)=(.26,.8,.08,1)
        _MotionAmount("Decorative motion",Range(0,1))=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "ElementalFire"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _MotionAmount;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 normalWS:TEXCOORD1;float3 positionWS:TEXCOORD2;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            Varyings Vert(Attributes input)
            {
                Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_TRANSFER_INSTANCE_ID(input,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float t=_Time.y*_MotionAmount;
                float3 origin=TransformObjectToWorld(float3(0,0,0));
                input.positionOS.xz+=float2(sin(t*1.3+origin.x*1.1),cos(t*1.1+origin.z*.8))*input.uv.y*input.uv.y*.10;
                o.positionWS=TransformObjectToWorld(input.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(input.normalOS);o.uv=input.uv;return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float t=_Time.y*_MotionAmount;
                float flow=sin(i.uv.y*14-t*2.2+sin(i.uv.x*18+t*.9)*1.7)*.5+.5;
                float facing=abs(dot(normalize(i.normalWS),SafeNormalize(GetWorldSpaceViewDir(i.positionWS))));
                float density=(.32+.20*flow)*pow(saturate(1-i.uv.y),.6)*smoothstep(0,.12,i.uv.y);
                float alpha=saturate(density*(.45+.55*facing));
                float3 core=lerp(_BaseColor.rgb,float3(.8,1,.45),.25*(1-i.uv.y));
                return half4(core*1.75*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
