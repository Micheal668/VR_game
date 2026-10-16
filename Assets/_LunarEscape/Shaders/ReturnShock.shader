Shader "LunarEscape/Return Shock Layer"
{
    Properties { _Energy("Heating",Range(0,1))=0 _FlowTime("Flight time",Float)=0 }
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
            float _Energy,_FlowTime;
            CBUFFER_END
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION;float3 local:TEXCOORD0;float2 uv:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(i.p.xyz);o.local=i.p.xyz;o.uv=i.uv;return o;}
            float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
            float noise(float3 p)
            {
                float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),
                lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 p=i.local*float3(3,.75,3);p.y-=_FlowTime*2.4;
                float turbulence=noise(p*1.6+noise(p*.7)*3)*.58+noise(p*3.8)*.28+noise(p*8)*.14;
                float folds=sin(i.local.y*5-_FlowTime*10+noise(p*.9)*8)*.5+.5;
                float density=smoothstep(.27,.78,turbulence+folds*.20);
                float tail=1-smoothstep(1.4,5,i.local.y);
                float heat=saturate(_Energy);
                half3 colour=lerp(half3(.75,.05,.005),half3(1,.42,.065),density);
                colour=lerp(colour,half3(1,.78,.35),pow(density,5)*heat);
                return half4(colour*1.7,heat*tail*(.12+density*.6));
            }
            ENDHLSL
        }
    }
}
