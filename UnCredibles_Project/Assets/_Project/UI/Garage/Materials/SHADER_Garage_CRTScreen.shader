// Old CRT TV screen of the garage. _BaseMap is the settings menu rendered by GarageFrontend.
// _Power drives the whole switch: 0 is dark glass, then a dot opens into a bright line, the line opens
// into the full picture with a flash, and the static clears. Going back down plays it in reverse.
// Unlit: the picture glows on its own, scanlines, RGB mask, colour fringes, flicker and a rolling band on top.
// Over everything sits the glass: grey-green tube when off, a Fresnel sheen of the room (_ReflectionColor,
// there are no reflection probes) and the main light's highlight, both moving with the view.
Shader "UnCredibles/Garage/CRTScreen"
{
    Properties
    {
        [MainTexture] _BaseMap ("Picture", 2D) = "black" {}
        _Power ("Power", Range(0, 1)) = 0
        _Brightness ("Brightness", Range(0, 3)) = 1.25
        _GlassColor ("Glass Colour (off)", Color) = (0.12, 0.14, 0.13, 1)
        _ReflectionColor ("Room Reflection", Color) = (1, 0.6, 0.72, 1)
        _Reflection ("Reflection Strength", Range(0, 1)) = 0.55
        _Specular ("Light Highlight", Range(0, 3)) = 1.2
        _Gloss ("Glossiness", Range(8, 512)) = 160
        _ReflectionWhenOn ("Reflections When On", Range(0, 1)) = 0.35
        _Curvature ("Picture Curvature", Range(0, 0.3)) = 0.06
        _ScanLines ("Scanlines", Float) = 240
        _ScanLineStrength ("Scanline Strength", Range(0, 1)) = 0.3
        _MaskStrength ("RGB Mask Strength", Range(0, 1)) = 0.18
        _Aberration ("Colour Fringes", Range(0, 0.02)) = 0.004
        _Static ("Static", Range(0, 1)) = 0.02
        _Flicker ("Flicker", Range(0, 0.2)) = 0.03
        _RollStrength ("Rolling Band", Range(0, 0.3)) = 0.06
        _Vignette ("Vignette", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _GlassColor;
            half4 _ReflectionColor;
            half _Reflection;
            half _Specular;
            half _Gloss;
            half _ReflectionWhenOn;
            half _Power;
            half _Brightness;
            half _Curvature;
            half _ScanLines;
            half _ScanLineStrength;
            half _MaskStrength;
            half _Aberration;
            half _Static;
            half _Flicker;
            half _RollStrength;
            half _Vignette;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half3 normalWS : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half3 Picture(float2 uv, float2 fringe)
            {
                half3 color;
                color.r = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + fringe).r;
                color.g = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).g;
                color.b = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - fringe).b;
                return color;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv - 0.5;
                float time = _Time.y;

                // Dead tube: grey-green, a little darker towards the rim.
                half rim = saturate(1.0 - dot(centered * float2(1.1, 1.3), centered * float2(1.1, 1.3)) * 2.2);
                half3 tube = _GlassColor.rgb * (0.7 + 0.3 * rim);

                // Glass: the curved front reflects more of the room at grazing angles (Fresnel), the room is
                // lighter above than below, the main light leaves a sharp highlight and a window a soft one.
                half3 normal = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = 0.04 + 0.96 * pow(1.0 - saturate(dot(normal, view)), 4.0);
                half3 reflected = reflect(-view, normal);
                half3 room = _ReflectionColor.rgb * lerp(0.35, 1.0, saturate(reflected.y * 0.5 + 0.5));
                Light light = GetMainLight();
                half highlight = pow(saturate(dot(normal, normalize(light.direction + view))), _Gloss) * _Specular;
                float2 windowUV = input.uv - float2(0.28, 0.74) + reflected.xy * 0.08;
                half window = smoothstep(0.2, 0.0, length(windowUV * float2(1.0, 1.8))) * 0.06;
                half3 glass = room * (fresnel + 0.06) * _Reflection + light.color * highlight + window;

                // Switch: a dot widens into a line, the line opens to the full height.
                half power = saturate(_Power);
                half width = smoothstep(0.0, 0.18, power);
                half height = max(smoothstep(0.18, 0.5, power), 0.008);
                half lit = (1.0 - smoothstep(width * 0.5 - 0.01, width * 0.5, abs(centered.x)))
                         * (1.0 - smoothstep(height * 0.5 - 0.004, height * 0.5, abs(centered.y)))
                         * step(0.001, power);

                // The picture squeezed into the lit area, with a little extra tube curvature.
                float2 picture = centered / float2(max(width, 0.001), height);
                picture *= 1.0 + _Curvature * dot(picture, picture);
                float2 uv = picture + 0.5;

                half3 color = Picture(uv, picture * _Aberration * 2.0);
                color *= step(abs(picture.x), 0.5) * step(abs(picture.y), 0.5);

                half scan = 1.0 - _ScanLineStrength * (0.5 + 0.5 * sin(uv.y * _ScanLines * TWO_PI));
                half3 mask = 0.5 + 0.5 * cos(TWO_PI * (frac(input.uv.x * _ScanLines * 1.5) - half3(0.0, 1.0 / 3.0, 2.0 / 3.0)));
                color *= scan * lerp(1.0, mask * 1.5, _MaskStrength);

                half roll = frac(uv.y * 0.5 - time * 0.11);
                color += _RollStrength * exp(-pow((roll - 0.5) * 16.0, 2.0));
                color *= 1.0 + _Flicker * sin(time * 97.0) * sin(time * 13.0);

                // Snow and the flash while the tube warms up (or dies).
                half warmUp = 1.0 - smoothstep(0.45, 1.0, power);
                half snow = Hash(floor(input.uv * float2(320.0, 240.0)) + floor(time * 30.0)) - 0.5;
                half flash = 1.0 - smoothstep(0.35, 0.85, power);
                color = color * smoothstep(0.4, 0.9, power) + snow * (_Static + warmUp * 0.6);
                color = lerp(color, half3(1.3, 1.35, 1.4), flash);

                // Lit, the picture covers the tube and the reflections fade under its light.
                half vignette = lerp(1.0, rim, _Vignette);
                half on = lit * smoothstep(0.4, 0.9, power);
                half3 result = tube * (1.0 - 0.75 * on) + max(color, 0.0) * vignette * _Brightness * lit
                             + glass * lerp(1.0, _ReflectionWhenOn, on);
                result = MixFog(result, input.fogFactor);
                return half4(result, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half Frag(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
