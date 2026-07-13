Shader "Custom/VolumetricFlowURP_VR_StrictDither"
{
    Properties
    {
        [HDR] _BaseColor("Base Color", Color) = (0.5, 0.8, 1.0, 1.0)
        _FlowSpeed("Flow Speed", Float) = -1.0
        _NoiseScale("Noise Scale (X=Width, Y=Height)", Vector) = (10.0, 2.0, 0, 0)

        _FadeStart("Fade Start", Range(0.0, 1.0)) = 0.0
        _FadeEnd("Fade End", Range(0.0, 1.0)) = 1.0
        _AlphaMultiplier("Alpha Multiplier", Range(0.0, 4.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        LOD 100

        Pass
        {
            Name "Forward"
            Blend Off
            ZWrite On
            Cull Off
            
            // AlphaToMask убран, чтобы не мешать ручному clip()

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 localPos   : TEXCOORD0;
                float2 uv         : TEXCOORD1;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _FlowSpeed;
                float4 _NoiseScale;
                float  _FadeStart;
                float  _FadeEnd;
                float  _AlphaMultiplier;
            CBUFFER_END

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float valueNoiseTileable(float2 uv, float periodX)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);

                float ix0 = fmod(i.x, periodX);
                float ix1 = fmod(i.x + 1.0, periodX);

                if (ix0 < 0.0) ix0 += periodX;
                if (ix1 < 0.0) ix1 += periodX;

                float a = hash(float2(ix0, i.y));
                float b = hash(float2(ix1, i.y));
                float c = hash(float2(ix0, i.y + 1.0));
                float d = hash(float2(ix1, i.y + 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = pos.positionCS;
                output.localPos   = input.positionOS.xyz;
                output.uv         = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float periodX = max(1.0, floor(_NoiseScale.x));

                float u = (atan2(input.localPos.z, input.localPos.x) / 6.28318530718) + 0.5;
                float v = input.localPos.y;

                float2 noiseUV = float2(
                    u * periodX,
                    (v * _NoiseScale.y) + (_Time.y * _FlowSpeed)
                );

                float noiseVal = valueNoiseTileable(noiseUV, periodX);

                float fadeT = saturate((input.uv.y - _FadeStart) / max(0.0001, (_FadeEnd - _FadeStart)));
                float verticalFade = fadeT;

                float finalAlpha = saturate(noiseVal * 2.0) * verticalFade * _BaseColor.a * _AlphaMultiplier;

                static const float ditherMatrix[16] =
                {
                     1.0,  9.0,  3.0, 11.0,
                    13.0,  5.0, 15.0,  7.0,
                     4.0, 12.0,  2.0, 10.0,
                    16.0,  8.0, 14.0,  6.0
                };

                uint2 pix = uint2(input.positionCS.xy) & 3;
                uint index = pix.x + pix.y * 4;

                clip(finalAlpha - (ditherMatrix[index] / 17.0));

                // Возвращаем жесткую непрозрачность для выживших пикселей
                return half4(_BaseColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}