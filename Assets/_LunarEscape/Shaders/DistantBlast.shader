Shader "LunarEscape/Distant Blast"
{
    Properties { _Age("Seconds after explosion",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
            float _Age;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz); output.uv=input.uv; return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p=input.uv*2-1;
                float radius=length(p);
                float cloud=sin(p.x*17+_Age*.8)*sin(p.y*21-_Age*.6)*.045+sin((p.x+p.y)*31)*.035;
                float edge=1-smoothstep(.48+cloud,.95+cloud,radius);
                float glow=exp(-_Age*4)*exp(-radius*radius*7);
                float3 dust=float3(.44,.43,.4)*(1-radius*.28);
                float3 flash=lerp(float3(1,.39,.08),float3(1,1,.87),glow);
                float3 color=lerp(dust,flash,saturate(exp(-_Age*3)*2));
                float alpha=edge*saturate((6-_Age)/3)*.8;
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
