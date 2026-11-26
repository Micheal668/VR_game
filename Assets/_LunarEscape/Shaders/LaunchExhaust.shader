Shader "LunarEscape/Launch Exhaust"
{
    Properties { _Hot("Hot core",Color)=(1,.86,.53,1) _Tail("Exhaust",Color)=(1,.22,.06,1) _FilmTime("Film time",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off Cull Back Blend SrcAlpha One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Hot, _Tail; float _FilmTime;
            CBUFFER_END
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i){ V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;return o; }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float y=i.uv.y;
                float wave=.53+.47*sin(y*41-_FilmTime*19+sin(6.283185*i.uv.x*5+y*13));
                float filament=.55+.45*sin(6.283185*i.uv.x*11-_FilmTime*5+y*21);
                float alpha=(1-y)*(.55+.40*wave+.15*filament);
                return half4(lerp(_Hot.rgb, _Tail.rgb, smoothstep(.02,.67,y)) * (1.15+wave*.5), alpha);
            }
            ENDHLSL
        }
    }
}
