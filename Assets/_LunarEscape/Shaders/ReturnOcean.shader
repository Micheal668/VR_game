Shader "LunarEscape/Return Ocean"
{
    Properties { _BaseColor("Sea", Color) = (.025,.22,.32,1) _Opacity("Atmospheric visibility", Range(0,1)) = 0 _WaveTime("Mission clock", Float) = 0 _Splash("Landing wake", Float) = 0 _CapsulePosition("Capsule", Vector) = (0,0,0,0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _CapsulePosition; float _Opacity, _WaveTime, _Splash;
            CBUFFER_END
            struct A { float4 p:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION; float3 w:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i) { V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.w=TransformObjectToWorld(i.p.xyz); o.p=TransformWorldToHClip(o.w); return o; }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(a),Hash(a+float2(1,0)),f.x),lerp(Hash(a+float2(0,1)),Hash(a+1),f.x),f.y);
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float distance=length(i.w-_WorldSpaceCameraPos);
                float2 q=i.w.xz*.17;
                float broad=Noise(q+float2(_WaveTime*.04,-_WaveTime*.02));
                float small=Noise(q*3.7-float2(_WaveTime*.08,0));
                float ax=dot(i.w.xz,float2(.28,.13))+_WaveTime*.65+broad*4;
                float az=dot(i.w.xz,float2(-.17,.36))-_WaveTime*.42+small*2;
                float3 normal=normalize(float3(cos(ax)*.12+cos(az)*.05,1,sin(az)*.13+sin(ax)*.04));
                float3 eye=normalize(_WorldSpaceCameraPos-i.w);
                float fresnel=pow(1-saturate(dot(normal,eye)),4);
                float glint=pow(saturate(dot(reflect(-normalize(float3(.3,.9,-.4)),normal),eye)),90);
                half3 sea=lerp(_BaseColor.rgb*.5,_BaseColor.rgb*1.25,broad*.7+small*.3);
                sea=lerp(sea,half3(.43,.62,.73),fresnel*.75)+glint*.28;
                float radial=length(i.w.xz-_CapsulePosition.xz);
                float wake=(1-smoothstep(2.2,7,radial))*smoothstep(1.9,2.4,radial)*(.5+.5*sin(radial*9-_WaveTime*4));
                sea=lerp(sea,half3(.82,.92,.94),wake*_Splash*.7);
                return half4(lerp(sea,half3(.48,.68,.76),saturate(distance/2000)),_Opacity);
            }
            ENDHLSL
        }
    }
}
