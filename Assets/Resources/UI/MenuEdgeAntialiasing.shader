Shader "Chess/UI/Menu Edge Antialiasing"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture",2D)="white" {}
        _Color ("Tint",Color)=(1,1,1,1)
        _StencilComp ("Stencil Comparison",Float)=8
        _Stencil ("Stencil ID",Float)=0
        _StencilOp ("Stencil Operation",Float)=0
        _StencilWriteMask ("Stencil Write Mask",Float)=255
        _StencilReadMask ("Stencil Read Mask",Float)=255
        _ColorMask ("Color Mask",Float)=15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Alpha Clip",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct Input
            {
                float4 vertex:POSITION;
                float4 tint:COLOR;
                float2 distance:TEXCOORD0;
                float4 primary:TEXCOORD1;
                float4 secondary:TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Output
            {
                float4 vertex:SV_POSITION;
                float4 tint:COLOR;
                float2 distance:TEXCOORD0;
                float4 primary:TEXCOORD1;
                float4 secondary:TEXCOORD2;
                float2 position:TEXCOORD3;
                float4 mask:TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            float4 _Color;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            Output vert(Input v)
            {
                Output o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(v.vertex);
                o.position=v.vertex.xy;
                float4 clipRect=clamp(_ClipRect,-2e10,2e10);
                float2 pixelSize=o.vertex.w/abs(mul((float2x2)UNITY_MATRIX_P,_ScreenParams.xy));
                o.mask=float4(v.vertex.xy*2-clipRect.xy-clipRect.zw,.25/(.25*float2(_UIMaskSoftnessX,_UIMaskSoftnessY)+abs(pixelSize)));
                o.tint=v.tint*_Color;
                o.distance=v.distance;
                o.primary=v.primary;
                o.secondary=v.secondary;
                // Canvas converts COLOR streams in linear projects; UV colour payloads need the same conversion.
                #ifndef UNITY_COLORSPACE_GAMMA
                o.primary.rgb=UIGammaToLinear(o.primary.rgb);
                o.secondary.rgb=UIGammaToLinear(o.secondary.rgb);
                #endif
                return o;
            }
            float4 frag(Output i):SV_Target
            {
                float2 pixelWidth=max(fwidth(i.distance),float2(.0001,.0001));
                float coverage=saturate(i.distance.x/pixelWidth.x+.5);
                float blend=saturate(i.distance.y/pixelWidth.y+.5);
                float4 color=lerp(i.primary,i.secondary,blend)*i.tint;
                color.a*=coverage;
                #ifdef UNITY_UI_CLIP_RECT
                float2 mask=saturate((_ClipRect.zw-_ClipRect.xy-abs(i.mask.xy))*i.mask.zw);
                color.a*=mask.x*mask.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a-.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
