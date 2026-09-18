Shader "UI/LiquidCircleGauge"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _LiquidColor ("Liquid Color", Color) = (0.04, 0.78, 0.74, 0.92)
        _FillAmount ("Fill Amount", Range(0, 1)) = 0.7
        _WaveAmplitude1 ("Wave Amplitude 1", Range(0, 0.08)) = 0.018
        _WaveAmplitude2 ("Wave Amplitude 2", Range(0, 0.08)) = 0.01
        _WaveFrequency1 ("Wave Frequency 1", Float) = 13
        _WaveFrequency2 ("Wave Frequency 2", Float) = 21
        _WaveSpeed1 ("Wave Speed 1", Float) = 2.1
        _WaveSpeed2 ("Wave Speed 2", Float) = 1.35
        _CircleRadius ("Liquid Inner Radius", Range(0, 0.5)) = 0.445
        _EdgeSoftness ("Edge Softness", Range(0.0001, 0.03)) = 0.004
        _WaveBoost ("Wave Boost", Range(1, 3)) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "LiquidGauge"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float4 _ClipRect;

            fixed4 _LiquidColor;
            float _FillAmount;
            float _WaveAmplitude1;
            float _WaveAmplitude2;
            float _WaveFrequency1;
            float _WaveFrequency2;
            float _WaveSpeed1;
            float _WaveSpeed2;
            float _CircleRadius;
            float _EdgeSoftness;
            float _WaveBoost;

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(output.worldPosition);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 centeredUv = input.texcoord - 0.5;
                float distanceFromCenter = length(centeredUv);
                float circleMask = 1.0 - smoothstep(
                    _CircleRadius - _EdgeSoftness,
                    _CircleRadius,
                    distanceFromCenter);
                clip(circleMask - 0.001);

                float waterMask = 0.0;
                if (_FillAmount >= 0.9999)
                {
                    waterMask = 1.0;
                }
                else if (_FillAmount > 0.0001)
                {
                    float wave1 = sin(input.texcoord.x * _WaveFrequency1 + _Time.y * _WaveSpeed1)
                        * _WaveAmplitude1;
                    float wave2 = sin(input.texcoord.x * _WaveFrequency2 - _Time.y * _WaveSpeed2)
                        * _WaveAmplitude2;
                    float surfaceHeight = _FillAmount + (wave1 + wave2) * _WaveBoost;
                    float antialiasWidth = max(fwidth(input.texcoord.y), 0.001);
                    waterMask = 1.0 - smoothstep(
                        surfaceHeight - antialiasWidth,
                        surfaceHeight + antialiasWidth,
                        input.texcoord.y);
                }

                fixed4 color = _LiquidColor * input.color;
                color.a *= circleMask * waterMask;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
