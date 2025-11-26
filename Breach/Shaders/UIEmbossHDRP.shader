Shader "UI/Emboss HDRP" {
    Properties {
        [PerRendererData]_MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BaseColor ("Base Surface Color", Color) = (0.42, 0.33, 0.21, 1)
        _HighlightColor ("Highlight Color", Color) = (1, 0.93, 0.82, 1)
        _ShadowColor ("Shadow Color", Color) = (0.1, 0.08, 0.06, 1)
        _Depth ("Depth", Range(0, 5)) = 1
        _LightDir ("Light Direction", Vector) = (0.35, 0.5, 0.8, 0)
        [ToggleUI]_Embossed ("Embossed (On) / Engraved (Off)", Float) = 1
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader {
        Tags {
            "RenderPipeline" = "HDRenderPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass {
            Name "UIEmbossHDRP"
            Tags { "LightMode" = "HDUnlitPass" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local __ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local __ UNITY_UI_ALPHACLIP
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "UnityUI.cginc"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _BaseColor;
                float4 _HighlightColor;
                float4 _ShadowColor;
                float _Depth;
                float4 _LightDir;
                float _Embossed;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            struct appdata_t {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata_t v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                o.worldPos = v.vertex;
                return o;
            }

            float4 frag (v2f i) : SV_Target {
                #ifdef UNITY_UI_CLIP_RECT
                    if (UnityGet2DClipping(i.worldPos.xy, _ClipRect) == 0) discard;
                #endif

                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                tex.rgb *= tex.a;

                float height = tex.a;
                if (_Embossed < 0.5)
                {
                    height = 1 - height;
                }

                float2 pixel = _MainTex_TexelSize.xy;
                float hL = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - float2(pixel.x, 0)).a;
                float hR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(pixel.x, 0)).a;
                float hD = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - float2(0, pixel.y)).a;
                float hU = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(0, pixel.y)).a;

                if (_Embossed < 0.5)
                {
                    hL = 1 - hL; hR = 1 - hR; hD = 1 - hD; hU = 1 - hU;
                }

                float dhdx = (hR - hL);
                float dhdy = (hU - hD);
                float3 normal = normalize(float3(-dhdx * _Depth, -dhdy * _Depth, 1));

                float3 lightDir = normalize(_LightDir.xyz);
                float ndotl = saturate(dot(normal, lightDir));
                float rim = pow(saturate(1 - ndotl), 2);

                float3 lit = lerp(_ShadowColor.rgb, _HighlightColor.rgb, ndotl);
                lit = lerp(lit, _BaseColor.rgb, 0.35) + rim * 0.08;

                float alpha = tex.a * i.color.a * _Color.a;
                float3 finalColor = lerp(_ShadowColor.rgb, lit, saturate(height * 1.2)) * i.color.rgb;

                #ifdef UNITY_UI_ALPHACLIP
                    clip(alpha - 0.001);
                #endif

                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}
