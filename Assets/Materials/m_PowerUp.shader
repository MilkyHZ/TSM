Shader "Custom/m_PowerUp"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _Speed("Cycle Speed", Float) = 0.5
        _Saturation("Saturation", Range(0, 1)) = 1.0
        _Brightness("Brightness (HDR Glow)", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
                UNITY_DEFINE_INSTANCED_PROP(float, _Speed)
                UNITY_DEFINE_INSTANCED_PROP(float, _Saturation)
                UNITY_DEFINE_INSTANCED_PROP(float, _Brightness)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            half3 HSVtoRGB(float3 hsv)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(hsv.xxx + K.xyz) * 6.0 - K.www);
                return half3(hsv.z * lerp(K.xxx, saturate(p - K.xxx), hsv.y));
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                
                float4 baseMapST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
                OUT.uv = IN.uv * baseMapST.xy + baseMapST.zw;
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float speed = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Speed);
                float saturation = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Saturation);
                float brightness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Brightness);
                half4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);

                float hue = frac(_Time.y * speed);
                half3 wheelColor = HSVtoRGB(float3(hue, saturation, brightness));

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * baseColor;
                return half4(texColor.rgb * wheelColor, texColor.a);
            }
            ENDHLSL
        }
    }
}
