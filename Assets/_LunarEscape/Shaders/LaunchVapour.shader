Shader "LunarEscape/Launch Vapour"
{
    Properties { _Age("Puff age", Float)=0 _Seed("Seed",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Age, _Seed;
            CBUFFER_END
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 center=TransformObjectToWorld(float3(0,0,0));
                float size=length(TransformObjectToWorldDir(float3(1,0,0),false));
                float3 right=UNITY_MATRIX_V[0].xyz, up=UNITY_MATRIX_V[1].xyz;
                o.p=TransformWorldToHClip(center+size*(right*i.p.x+up*i.p.y));o.uv=i.uv;return o;
            }
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p)
            {
                float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p=i.uv*2-1;
                float n=.67*noise(i.uv*5+_Seed+_Age*.12)+.33*noise(i.uv*13+_Seed);
                float density=1-smoothstep(.25,.93,length(p)+.4*(.5-n));
                half3 vapor=lerp(half3(.38,.36,.34), half3(.88,.86,.81), saturate(.35+n*.65+p.y*.2));
                float lifetime=smoothstep(0,.6,_Age)*(1-smoothstep(4.5,9,_Age));
                return half4(vapor,density*lifetime*.67);
            }
            ENDHLSL
        }
    }
}
