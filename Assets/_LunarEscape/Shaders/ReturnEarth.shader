Shader "LunarEscape/Return Earth Globe"
{
    Properties { _BaseMap("NASA equirectangular surface",2D)="white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct A { float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION;float3 local:TEXCOORD0;float3 normal:TEXCOORD1;float3 world:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(i.p.xyz);o.local=i.p.xyz;o.normal=TransformObjectToWorldNormal(i.n);o.world=TransformObjectToWorld(i.p.xyz);return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.local),wn=normalize(i.normal),v=normalize(GetCameraPositionWS()-i.world);
                float2 uv=float2(atan2(n.x,n.z)/6.2831853+.5,asin(n.y)/3.14159265+.5);
                half3 surface=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb;
                float clouds=noise(uv*float2(34,20)+noise(uv*18)*2)*.6+noise(uv*float2(90,60))*.3+noise(uv*190)*.1;
                clouds=smoothstep(.57,.76,clouds)*.80;
                surface=lerp(surface,half3(.88,.92,.96),clouds);
                float light=smoothstep(-.12,.6,dot(wn,normalize(float3(-.5,.5,-1))));
                float rim=pow(1-saturate(dot(wn,v)),4);
                return half4(surface*(.08+light*.92)+half3(.12,.36,.66)*rim*(.15+light*.7),1);
            }
            ENDHLSL
        }
    }
}
