Shader "LunarEscape/Return Plasma"
{
    Properties { _BaseColor("Glow",Color)=(1,1,1,1) _FlowTime("Animation time",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor; float _FlowTime;
            CBUFFER_END
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; half4 c:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; half4 c:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i) { V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.c=i.c;return o; }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float edge=pow(saturate(1-abs(i.uv.y*2-1)),2);
                float tail=pow(saturate(1-i.uv.x),.7);
                float flow=.65+.35*sin(i.uv.x*24-_FlowTime*7);
                return half4(i.c.rgb*_BaseColor.rgb*1.5,edge*tail*flow*.65);
            }
            ENDHLSL
        }
    }
}
