Shader "LunarEscape/Lunar Regolith"
{
    Properties
    {
        _BaseColor("Soil tint",Color)=(.48,.46,.43,1)
        _BaseMap("Moon albedo",2D)="white"{}
        _UseMap("Use lunar albedo",Float)=0
        _NoiseScale("Grain scale",Float)=1
        _Visibility("Visibility",Range(0,1))=1
        _InvertFade("Complementary distant fade",Float)=0
        [HideInInspector] _SrcBlend("Source blend",Float)=1
        [HideInInspector] _DstBlend("Destination blend",Float)=0
        [HideInInspector] _ZWrite("Depth write",Float)=1
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Tags{"LightMode"="UniversalForward"}
            Cull Back ZWrite [_ZWrite] Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor,_BaseMap_ST;float _UseMap,_NoiseScale,_Visibility,_InvertFade;
            CBUFFER_END
            struct Attributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float3 local:TEXCOORD2;float2 uv:TEXCOORD3;float3 tangentWS:TEXCOORD4;float3 bitangentWS:TEXCOORD5;float4 tint:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);}
            // 全球纹理不足以表达几公里处的细节，添加两级程序坑缘法线；不伪造可行走碰撞。
            float2 CraterSlope(float2 p,float cellSize,float depthRatio)
            {
                float2 cell=floor(p/cellSize),slope=0;
                for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                {
                    float2 id=cell+float2(x,y);
                    if(Hash(id+71.8)<.35)continue;
                    float2 center=(id+.2+.6*float2(Hash(id),Hash(id+19.3)))*cellSize;
                    float radius=cellSize*lerp(.06,.46,pow(Hash(id+37.2),2));
                    float2 delta=p-center;float distance=length(delta),q=distance/radius*(.93+.14*Noise(p*1.6+id*29));
                    float bowl=q<1 ? 4*depthRatio*q*(1-q*q) : 0;
                    float rim=-.52*depthRatio*(q-1)/.0225*exp(-pow((q-1)/.15,2));
                    slope+=(bowl+rim)*delta/max(distance,.0001);
                }
                return slope;
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);output.local=input.positionOS.xyz;output.uv=input.uv;output.tint=input.color;
                float3 tangent=normalize(float3(-input.positionOS.z,.000001,input.positionOS.x));
                output.tangentWS=TransformObjectToWorldDir(tangent);output.bitangentWS=TransformObjectToWorldDir(normalize(cross(tangent,input.normalOS)));return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p=input.local.xz*_NoiseScale;
                float grain=.69+Noise(p*.26)*.32+Noise(p*2.3)*.15+Noise(p*17)*.10;
                grain=lerp(grain,1,_UseMap);
                float3 albedo=_BaseColor.rgb*grain*lerp(1,input.tint.rgb,.35);
                albedo*=lerp(1,SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*2.2,_UseMap);
                float3 normal=normalize(input.normalWS);Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                if(_UseMap>.5)
                {
                    float2 mapPoint=input.uv*float2(10916.4,5458.2);
                    float smallFade=1-smoothstep(.3,1.2,length(fwidth(mapPoint)));
                    float2 slope=CraterSlope(mapPoint,2.4,.10)*smallFade+CraterSlope(mapPoint,13,.07);
                    normal=normalize(normal-slope.x*normalize(input.tangentWS)-slope.y*normalize(input.bitangentWS));
                    albedo*=.94+.12*Noise(mapPoint*.7);
                }
                float3 illumination=.10+SampleSH(normal)*.65+sun.color*saturate(dot(normal,sun.direction))*sun.shadowAttenuation;
                return half4(albedo*illumination,lerp(1,_Visibility,_UseMap));
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
