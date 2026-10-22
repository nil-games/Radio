// Помехи VHS за текстом выбранного варианта ответа.
// Серые горизонтальные штрихи с прозрачными просветами между ними: зерно вытянуто по
// горизонтали, строки дёргаются вбок, кадр шума меняется рывками, как на плёнке, и
// снизу вверх ползёт светлая полоса прокрутки. Края мягко гаснут, чтобы не было рамки.
//
// Зерно считается в пикселях холста, а не в долях картинки: у короткого и длинного
// варианта штрихи одного размера, ничего не растягивается.
// Прозрачность всего слоя — альфа цвета Image: ею плавно включает и гасит OptionStatic.
Shader "Radio/UI/VhsStatic"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.65,0.65,0.65,1)

        _Brightness ("Непрозрачность штрихов", Range(0, 1)) = 0.11
        _Coverage ("Доля штрихов (остальное — просветы)", Range(0, 1)) = 0.11
        _CellSize ("Зерно, пикс холста (ширина, высота)", Vector) = (9, 2, 0, 0)
        _Fps ("Смен кадра шума в секунду", Float) = 8
        _Jitter ("Дрожание строк вбок, пикс", Float) = 5
        _BandSpeed ("Скорость полосы прокрутки", Float) = 0.175
        _BandStrength ("Яркость полосы прокрутки", Range(0, 1)) = 0.25
        _EdgeSoftness ("Мягкость краёв (доля ширины, высоты)", Vector) = (0.06, 0.25, 0, 0)

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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 canvasPos : TEXCOORD1;
            };

            fixed4 _Color;
            float4 _ClipRect;
            float _Brightness;
            float _Coverage;
            float4 _CellSize;
            float _Fps;
            float _Jitter;
            float _BandSpeed;
            float _BandStrength;
            float4 _EdgeSoftness;

            // Хеш без sin: у sin на больших аргументах плывёт точность, и через
            // полчаса игры шум начал бы складываться в узор.
            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.canvasPos = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Кадр шума меняется рывками; по модулю — чтобы не росла бесконечно.
                float frame = fmod(floor(_Time.y * _Fps), 4096.0);
                float2 cellSize = max(_CellSize.xy, 1.0);
                float2 p = i.canvasPos.xy;

                // Строка дёргается вбок целиком, как сорванная синхронизация плёнки.
                float row = floor(p.y / cellSize.y);
                float shift = (Hash(float2(row, frame)) - 0.5) * 2.0 * _Jitter;
                float2 cell = float2(floor((p.x + shift) / cellSize.x), row);

                // Строки неровные: одни почти пустые, другие гуще — отсюда «полосатость».
                float rowDensity = lerp(0.35, 1.4, Hash(float2(row * 1.37, floor(frame / 3.0))));

                // Полоса прокрутки: светлее и гуще, медленно ползёт снизу вверх.
                float bandPos = frac(i.uv.y - _Time.y * _BandSpeed) - 0.5;
                float band = exp(-bandPos * bandPos * 40.0) * _BandStrength;

                float coverage = saturate(_Coverage * rowDensity + band * 0.5);
                float n = Hash(cell + frame * 17.0);
                float on = step(1.0 - coverage, n);

                // Яркость штриха тоже случайна: так шум выглядит живым, а не трафаретом.
                float grey = lerp(0.45, 1.0, Hash(cell * 1.7 + frame * 3.1));
                grey = saturate(grey + band);

                float2 soft = max(_EdgeSoftness.xy, 0.0001);
                float edge = smoothstep(0.0, soft.x, i.uv.x) * smoothstep(0.0, soft.x, 1.0 - i.uv.x)
                           * smoothstep(0.0, soft.y, i.uv.y) * smoothstep(0.0, soft.y, 1.0 - i.uv.y);

                fixed4 col;
                col.rgb = grey * i.color.rgb;
                col.a = on * _Brightness * edge * i.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.canvasPos.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
