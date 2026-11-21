Shader "Chess But Weird/M3.5/Royal Sketch"
{
    Properties
    {
        _Accent ("Rarity ink", Color) = (1,0.65,0.22,1)
        _SourceTime ("Local ritual time", Float) = 0
        _FlashStrength ("Local light response", Range(0,1)) = 1
        _LightBuild ("Ritual charge", Range(0,1)) = 0
        _Metallic ("Metallic highlights", Range(0,1)) = 0.7
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "RoyalInkForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Accent; float _SourceTime; half _FlashStrength; half _LightBuild; half _Metallic;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 color:COLOR;float2 uv:TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                Varyings o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.color=v.color;o.uv=v.uv;return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 n=normalize(i.normalWS),v=normalize(GetWorldSpaceViewDir(i.positionWS));Light key=GetMainLight();
                half ndl=saturate(dot(n,key.direction));half ramp=.24+smoothstep(.04,.23,ndl)*.20+smoothstep(.38,.58,ndl)*.43;
                half metal=step(.55,i.color.a)*(1-step(.65,i.color.a));half glow=step(.75,i.color.a)*(1-step(.85,i.color.a));
                half3 base=i.color.rgb;base=lerp(base,_Accent.rgb*.76,(metal+glow)*.62);
                half3 colour=base*(.27+ramp*key.color);
                #if defined(_ADDITIONAL_LIGHTS)
                uint count=GetAdditionalLightsCount();
                for(uint index=0u;index<count;index++){Light l=GetAdditionalLight(index,i.positionWS);colour+=base*l.color*saturate(dot(n,l.direction))*l.distanceAttenuation*.27;}
                #endif
                half spec=pow(saturate(dot(n,normalize(v+key.direction))),48)*metal*_Metallic;
                colour+=spec*key.color*.58;
                // Solid ink silhouette and tiny cross-hatching. No outline pass or screen-space texture.
                half edge=smoothstep(.77,.94,1-saturate(dot(n,v)));
                float stroke=abs(frac((i.positionWS.x+i.positionWS.z*.68+i.positionWS.y*.17)*22)-.5);
                half hatch=(1-smoothstep(.020,.070,stroke))*(1-ndl)*.13;
                colour*=1-edge*.75-hatch;
                half focus=1-smoothstep(.6,4.1,length(i.positionWS.xz));colour*=.56+.44*focus;
                half wave=saturate(1-abs(_SourceTime-i.uv.x)/.20)*i.uv.y;
                half impact=saturate(1-abs(_SourceTime-3.65)/.30);
                colour+=_Accent.rgb*_FlashStrength*(glow*(.10+_LightBuild*.21+impact*.13)+metal*.025+wave*.31);
                return half4(max(colour,0),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A{float4 positionOS:POSITION;};struct V{float4 positionCS:SV_POSITION;};
            V Vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);return o;}half4 Frag(V i):SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags {"LightMode"="DepthNormalsOnly"} ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;};struct V{float4 positionCS:SV_POSITION;float3 n:TEXCOORD0;};
            V Vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.n=TransformObjectToWorldNormal(i.normalOS);return o;}
            half4 Frag(V i):SV_Target
            {
                float3 n=normalize(i.n);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct=PackNormalOctQuadEncode(n);return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
                #else
                return half4(n,0);
                #endif
            }
            ENDHLSL
        }
    }
}
