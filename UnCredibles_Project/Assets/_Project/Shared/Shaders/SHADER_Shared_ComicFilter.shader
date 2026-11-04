// Comic filter drawn ON TOP of an object that keeps its own material (see ComicFilter.cs).
// The mesh is drawn a second time with a "2x multiply" blend: grey 0.5 leaves the colour as it is,
// darker greys darken it and lighter greys brighten it. That adds, over the original look:
// - a flat darker band on the shadow side (two-tone comic light),
// - halftone dots over the shadows, in screen space so they keep their size on screen,
// - a light rim on the lit side,
// and a second pass draws a black ink outline (inverted hull) of constant width in pixels.
// The outline follows the mesh normals: on hard-edged models it can open at the corners.
Shader "UnCredibles/Shared/ComicFilter"
{
    Properties
    {
        [Header(Shadow band)]
        _ShadowDarkness ("Shadow Darkness", Range(0, 1)) = 0.35
        _ShadowTint ("Shadow Tint", Color) = (0.75, 0.72, 0.95, 1)
        _ShadowThreshold ("Shadow Edge", Range(-1, 1)) = 0.1
        _Softness ("Edge Softness", Range(0.001, 0.5)) = 0.02

        [Header(Halftone dots in the shadows)]
        _HalftoneStrength ("Dot Strength", Range(0, 1)) = 0.5
        _HalftoneScale ("Dots Across The Screen Height", Range(20, 300)) = 110
        _HalftoneReach ("Dots Into The Light", Range(0, 1)) = 0.35

        [Header(Rim of light)]
        _RimAmount ("Rim Width (lower = wider)", Range(0, 1)) = 0.72
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.3

        [Header(Ink outline)]
        _OutlineColor ("Outline Colour", Color) = (0.05, 0.04, 0.07, 1)
        _OutlineWidth ("Outline Width (pixels)", Range(0, 8)) = 2.5
    }

    SubShader
    {
        // Right after the opaque objects it covers.
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half _ShadowDarkness;
            half4 _ShadowTint;
            half _ShadowThreshold;
            half _Softness;
            half _HalftoneStrength;
            half _HalftoneScale;
            half _HalftoneReach;
            half _RimAmount;
            half _RimStrength;
            half4 _OutlineColor;
            half _OutlineWidth;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ComicOverlay"
            Tags { "LightMode" = "UniversalForward" }
            Blend DstColor SrcColor // result = 2 x filter x original colour
            ZWrite Off
            ZTest LEqual
            Offset -1, -1 // same surface as the original: win the depth tie

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            // Dots on a 45° grid in screen pixels; darker areas get bigger dots. 1 inside a dot.
            half Halftone(float2 pixel, half darkness)
            {
                float2 grid = pixel / _ScreenParams.y * _HalftoneScale;
                grid = float2(grid.x + grid.y, grid.y - grid.x) * 0.7071;
                float distanceToCentre = length(frac(grid) - 0.5);
                float radius = sqrt(saturate(darkness)) * 0.62;
                float edge = max(fwidth(distanceToCentre), 1e-4);
                return 1.0 - smoothstep(radius - edge, radius + edge, distanceToCentre);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal = normalize(input.normalWS);
                half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half light = dot(normal, mainLight.direction) * mainLight.shadowAttenuation;
                half lit = smoothstep(_ShadowThreshold - _Softness, _ShadowThreshold + _Softness, light);

                // 1 = untouched. The shadow side gets one flat, tinted step darker.
                half3 filter = lerp(_ShadowTint.rgb * (1.0 - _ShadowDarkness), half3(1, 1, 1), lit);

                half darkness = saturate((_ShadowThreshold + _HalftoneReach - light) / max(_HalftoneReach * 2.0, 1e-3));
                filter *= 1.0 - Halftone(input.positionCS.xy, darkness) * _HalftoneStrength;

                half rim = smoothstep(_RimAmount - 0.02, _RimAmount + 0.02, 1.0 - saturate(dot(normal, viewDirection))) * lit;
                filter += rim * _RimStrength;

                // The blend doubles it: 0.5 is "no change".
                return half4(filter * 0.5, 1);
            }
            ENDHLSL
        }

        // Ink outline: the mesh again, pushed out along its normals and drawn from the back.
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half fog : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // Push in screen space: the same width in pixels near and far.
                float3 normalCS = mul((float3x3)UNITY_MATRIX_VP, TransformObjectToWorldNormal(input.normalOS));
                float2 direction = normalCS.xy;
                if (dot(direction, direction) > 1e-8) direction = normalize(direction);
                positionCS.xy += direction * (_OutlineWidth * 2.0 / _ScreenParams.xy) * positionCS.w;
                output.positionCS = positionCS;
                output.fog = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(MixFog(_OutlineColor.rgb, input.fog), 1);
            }
            ENDHLSL
        }
    }
}
