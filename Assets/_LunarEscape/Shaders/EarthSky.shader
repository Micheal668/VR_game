Shader "LunarEscape/Earth Sky"
{
    Properties
    {
        _EarthPhoto("Original Earth photograph", 2D) = "black" {}
        _StarPhoto("Star field photograph", 2D) = "black" {}
        _StarExposure("Star field brightness", Range(0, 2)) = .65
        _SkyRight("Sky right", Vector) = (1,0,0,0)
        _SkyUp("Sky up", Vector) = (0,1,0,0)
        _SkyForward("Sky forward", Vector) = (0,0,1,0)
        _EarthDirection("Earth direction", Vector) = (0,.304776, .952424,0)
        _EarthRight("Earth right", Vector) = (1,0,0,0)
        _EarthUp("Earth up", Vector) = (0,.952424,-.304776,0)
        _EarthHalfSize("Photograph half angular size", Float) = .0602
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_EarthPhoto); SAMPLER(sampler_EarthPhoto);
            TEXTURE2D(_StarPhoto); SAMPLER(sampler_StarPhoto);
            CBUFFER_START(UnityPerMaterial)
            float4 _EarthDirection, _EarthRight, _EarthUp;
            float4 _SkyRight, _SkyUp, _SkyForward;
            float _EarthHalfSize, _StarExposure;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }
            half3 Stars(float3 ray)
            {
                // 原图不是全景照片；按经纬包裹，并在背面交界处混合两侧，避免明显硬接缝。
                float3 local = float3(dot(ray,_SkyRight.xyz), dot(ray,_SkyUp.xyz), dot(ray,_SkyForward.xyz));
                float2 uv = float2(atan2(local.x, local.z) / 6.2831853 + .5, asin(clamp(local.y,-1,1)) / 3.14159265 + .5);
                half3 stars = SAMPLE_TEXTURE2D(_StarPhoto,sampler_StarPhoto,uv).rgb;
                float edge = min(uv.x,1-uv.x);
                half3 opposite = SAMPLE_TEXTURE2D(_StarPhoto,sampler_StarPhoto,float2(1-uv.x,uv.y)).rgb;
                stars = lerp(stars,opposite,.5*(1-smoothstep(0,.045,edge)));
                // 收拢极点处的纹理，避免把非全景照片拉成亮色放射线。
                stars *= smoothstep(0,.065,min(uv.y,1-uv.y));
                return stars * _StarExposure;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 ray = normalize(input.direction);
                half3 stars = Stars(ray);
                float facing = dot(ray, _EarthDirection.xyz);
                float2 uv = .5 + float2(dot(ray, _EarthRight.xyz), dot(ray, _EarthUp.xyz)) / (2 * _EarthHalfSize * max(facing, .0001));
                if (facing <= 0 || any(uv < 0) || any(uv > 1)) return half4(stars,1);
                // 直接采样用户原图中的地球区域；排除照片底部月面，不生成或改写照片。
                float2 photoUV = float2((390 + uv.x * 170) / 899, (321 + uv.y * 170) / 621);
                half3 colour = SAMPLE_TEXTURE2D(_EarthPhoto, sampler_EarthPhoto, photoUV).rgb;
                // 压掉扫描照片黑背景的噪点，避免天空出现矩形边框；保留蓝白云层。
                float brightness = max(colour.r, max(colour.g, colour.b));
                colour *= smoothstep(.008, .035, brightness);
                // 地球整个圆盘（包括夜面）遮挡背景恒星，照片外侧不留下黑色矩形。
                float disk = 1 - smoothstep(.44, .45, length(uv - .5));
                return half4(lerp(stars,colour,disk), 1);
            }
            ENDHLSL
        }
    }
}
