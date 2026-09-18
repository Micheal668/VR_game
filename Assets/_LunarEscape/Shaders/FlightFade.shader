Shader "LunarEscape/Flight Fade"
{
    Properties { _Alpha("Black opacity", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay+100" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
            half _Alpha;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(0,0,0,_Alpha);
            }
            ENDHLSL
        }
    }
}
