Shader "Hidden/Chess/Menu Artwork Mip Audit"
{
    Properties { _MainTex ("Artwork",2D)="white" {} _MipLevel ("Mip level",Float)=0 }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _MipLevel;
            float4 frag(v2f_img i):SV_Target
            { return tex2Dlod(_MainTex,float4(i.uv,0,_MipLevel)); }
            ENDCG
        }
    }
}
