// Grass blades of Mow The Lawn, drawn with GPU instancing by MowLawn.
// Dark at the root, light at the tip, a little colour change per tuft and a soft wind sway.
// Lit by the main light (with its shadows) plus the ambient light, both sides of the blade.
Shader "UnCredibles/MowTheLawn/GrassBlade"
{
    Properties
    {
        _BaseColor ("Root Colour", Color) = (0.1, 0.3, 0.07, 1)
        _TipColor ("Tip Colour", Color) = (0.55, 0.83, 0.3, 1)
        _DryColor ("Dry Tip Colour", Color) = (0.78, 0.8, 0.35, 1)
        _DryAmount ("Dry Tufts Amount", Range(0, 1)) = 0.25
        _Variation ("Brightness Variation", Range(0, 0.5)) = 0.15
        _WindStrength ("Wind Strength", Range(0, 0.5)) = 0.12
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1.6
        _WindScale ("Wind Wave Size", Range(0.01, 2)) = 0.25
        _Translucency ("Light Through Blades", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                half4 _DryColor;
                half _DryAmount;
                half _Variation;
                half _WindStrength;
                half _WindSpeed;
                half _WindScale;
                half _Translucency;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 color : TEXCOORD1;
                half height : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;

                // The mesh is 1 unit tall: y is how far up the blade this vertex is.
                half height = saturate(input.positionOS.y);
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                // Wind: waves crossing the lawn, only the upper part of the blade moves.
                float wave = sin(_Time.y * _WindSpeed + (origin.x + origin.z * 0.6) * _WindScale * 6.2831);
                float gust = sin(_Time.y * _WindSpeed * 0.37 + origin.x * 0.11) * 0.5 + 0.5;
                float bend = height * height * _WindStrength * (0.6 + gust * 0.8);
                positionWS.xz += float2(wave, wave * 0.4) * bend;

                // Per-tuft colour: brightness change and some drier, yellowish tips.
                float random = Hash(origin.xz);
                half3 tip = lerp(_TipColor.rgb, _DryColor.rgb, step(1.0 - _DryAmount, Hash(origin.zx + 3.1)));
                half3 color = lerp(_BaseColor.rgb, tip, height * height * (3.0 - 2.0 * height));
                color *= 1.0 + (random - 0.5) * 2.0 * _Variation;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = color;
                output.height = height;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Blades are thin: light them as if they pointed up, like the ground they cover.
                half3 normalWS = half3(0, 1, 0);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = light.shadowAttenuation * light.distanceAttenuation;
                half diffuse = saturate(dot(normalWS, light.direction)) * 0.75 + 0.25;
                half3 lit = input.color * light.color * diffuse * shadow;
                // A bit of sunlight shining through the tips.
                lit += input.color * light.color * _Translucency * input.height * shadow * 0.5;
                lit += input.color * SampleSH(normalWS);

                half3 finalColor = MixFog(lit, input.fogFactor);
                return half4(finalColor, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
