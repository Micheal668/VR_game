Shader "LunarEscape/Launch Globe"
{
    Properties
    {
        _BaseMap("Surface map", 2D) = "white" {}
        _Air("Atmosphere", Range(0,1)) = 0
        _Visibility("Visibility", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half _Air, _Visibility;
            CBUFFER_END
            struct Input { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 clip : SV_POSITION; float3 surface : TEXCOORD0; float3 worldNormal : TEXCOORD1; float3 world : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Input input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.clip = TransformObjectToHClip(input.vertex.xyz);
                output.surface = input.normal;
                output.worldNormal = TransformObjectToWorldNormal(input.normal);
                output.world = TransformObjectToWorld(input.vertex.xyz);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal = normalize(input.worldNormal), local = normalize(input.surface);
                float2 uv = float2(.5 + atan2(local.x, local.z) / 6.2831853, .5 + asin(clamp(local.y, -1, 1)) / 3.14159265);
                // A derivative taken across the longitude wrap selects the lowest mip and draws a bright meridian.
                float2 dx = ddx(uv), dy = ddy(uv);
                dx.x -= round(dx.x);
                dy.x -= round(dy.x);
                half3 terrain = SAMPLE_TEXTURE2D_GRAD(_BaseMap,sampler_BaseMap,uv,dx,dy).rgb;
                Light sun = GetMainLight();
                half lit = smoothstep(-.12, .7, dot(normal, sun.direction));
                float rim = pow(1 - saturate(dot(normal, normalize(GetCameraPositionWS() - input.world))), 3.5);
                half3 radiance = terrain * lerp(.045, 1.18, lit) + half3(.14,.35,.61) * rim * _Air * (.16 + .84*lit);
                return half4(radiance, _Visibility);
            }
            ENDHLSL
        }
    }
}
