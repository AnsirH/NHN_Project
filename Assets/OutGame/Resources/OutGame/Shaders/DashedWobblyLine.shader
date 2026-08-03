Shader "OutGame/UI/DashedWobblyLine"
{
    // 방 그래프의 방-방 연결선 전용 셰이더 (상세 기획 §5.3) — 손그림처럼 살짝 휘고 점선으로 끊어지는
    // 선을 그린다. 곡선은 SDF가 아니라 "이 x에서 중심이 얼마나 벗어났나"를 사인 함수로 직접 표현하는
    // 방식(완만한 굴곡이라 자기 자신과 겹치지 않으므로 성립) — 진짜 베지어 거리장보다 훨씬 단순하다.
    // 좌표는 커스텀 인터폴런트 대신 Image가 항상 정확히 채워주는 UV(0..1)에서 역산한다 — 커스텀
    // TEXCOORD 보간이 이 프로젝트 환경에서 깨지는 걸 확인해서(디버그 결과 비정상값) UV 경유로 우회.
    // 인스턴스마다 다른 모양을 내려면 RoomMapPanel.CreateLine에서 재질 인스턴스를 만들어 _Amplitude1 등을
    // From/To 좌표 해시로 설정한다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _Length ("Length (local units)", Float) = 100
        _Height ("Rect Height (local units)", Float) = 31
        _Thickness ("Thickness (local units)", Float) = 4
        _DashLength ("Dash Length", Float) = 14
        _GapLength ("Gap Length", Float) = 8
        _Amplitude1 ("Main Bulge Amplitude (부호=방향)", Float) = 6
        _Amplitude2 ("Jitter Amplitude", Float) = 2
        _Freq2 ("Jitter Frequency", Float) = 6
        _Phase2 ("Jitter Phase", Float) = 0
        _AA ("Antialias Width", Float) = 1.5
        _BlobSizeJitter ("Blob Size Jitter (0~1)", Range(0, 1)) = 0.35
        _BlobBumpAmount ("Blob Bumpiness (0~1)", Range(0, 1)) = 0.3

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #define PI 3.14159265359

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _Length;
            float _Height;
            float _Thickness;
            float _DashLength;
            float _GapLength;
            float _Amplitude1;
            float _Amplitude2;
            float _Freq2;
            float _Phase2;
            float _AA;
            float _BlobSizeJitter;
            float _BlobBumpAmount;

            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // UV(0..1)에서 로컬 좌표를 역산 — x:[0,Length], y:[-Height/2,+Height/2]
                float x = IN.texcoord.x * _Length;
                float y = (IN.texcoord.y - 0.5) * _Height;

                // 점선 한 조각(blob)을 사각형이 아니라 울퉁불퉁한 타원으로 그린다 — 조각 인덱스를
                // 해시해 조각마다 크기/찌그러짐/울퉁불퉁한 정도를 다르게 준다(손그림 점 느낌).
                float period = max(_DashLength + _GapLength, 0.0001);
                float segIndex = floor(x / period);
                float segStartX = segIndex * period;
                float blobCenterX = segStartX + _DashLength * 0.5;

                // 굴곡 중심선은 각 조각(blob)의 중심 x에서 한 번만 평가한다 — 프래그먼트별 x로 평가하면
                // 곡선 기울기만큼 조각이 낫 모양으로 휘어 보이는 문제가 있었다(2026-08-04 발견).
                float tBlob = saturate(blobCenterX / max(_Length, 0.0001));
                float bulge = _Amplitude1 * sin(tBlob * PI);
                float jitter = _Amplitude2 * sin(tBlob * _Freq2 * PI * 2.0 + _Phase2);
                float centerY = bulge + jitter;

                float seedBase = segIndex * 17.0 + _Phase2 * 3.1;
                float rnd1 = hash11(seedBase);
                float rnd2 = hash11(seedBase + 11.7);
                float rnd3 = hash11(seedBase + 53.3);
                float rnd4 = hash11(seedBase + 91.1);

                float sizeJitter = _BlobSizeJitter;
                float rx = max(0.0001, (_DashLength * 0.5) * lerp(1.0 - sizeJitter, 1.0 + sizeJitter * 0.5, rnd1));
                float ry = max(0.0001, (_Thickness * 0.5) * lerp(1.0 - sizeJitter, 1.0 + sizeJitter, rnd2));

                // 실제 로컬 단위 거리에서 각도별 타원 반지름을 구하고, 그 차이(거리-반지름)에
                // fwidth를 적용한다 — 정규화된(rx≠ry 이방성) 좌표에서 fwidth(r)을 쓰면 타원의 긴 축
                // 끝부분에서 픽셀당 변화율이 비정상적으로 작아져 안티에일리어싱 폭이 그 방향에서만
                // 넓어지고, 그 결과 끝에 흐린 꼬리가 늘어지는 문제가 있었다(2026-08-04 발견).
                float dxReal = x - blobCenterX;
                float dyReal = y - centerY;
                float angle = atan2(dyReal, dxReal);
                float cosA = cos(angle);
                float sinA = sin(angle);
                float ellipseR = 1.0 / max(sqrt((cosA * cosA) / (rx * rx) + (sinA * sinA) / (ry * ry)), 0.0001);

                // 정수 주파수만 쓴다 — angle은 atan2라 ±π 경계에서 2π만큼 끊기는데, 정수가 아닌
                // 주파수를 그 각도에 그대로 곱해 sin에 넣으면 그 경계에서 값이 불연속이 되고,
                // fwidth(shapeDist)가 그 지점에서 폭발해 조각 한쪽에 흐린 꼬리가 늘어지는 문제가
                // 있었다(2026-08-04 발견 — 실제로는 이방성 정규화가 아니라 이게 원인이었다).
                float bumpFreq1 = 3.0 + floor(rnd3 * 3.0);
                float bumpFreq2 = 5.0 + floor(rnd4 * 3.0);
                float bump = _BlobBumpAmount * 0.18;
                float bumpyRadius = ellipseR * (1.0
                    + bump * sin(angle * bumpFreq1 + rnd1 * PI * 2.0)
                    + bump * 0.35 * sin(angle * bumpFreq2 + rnd2 * PI * 2.0));

                float distReal = length(float2(dxReal, dyReal));
                float shapeDist = distReal - bumpyRadius; // 안쪽 음수, 바깥쪽 양수 (실제 로컬 단위)
                float aa = clamp(fwidth(shapeDist) * 1.5, 0.001, 2.0);
                float blobAlpha = 1.0 - smoothstep(-aa, aa, shapeDist);

                fixed4 color = tex2D(_MainTex, IN.texcoord) * IN.color;
                color.a *= blobAlpha;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
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
