// Облако светящихся точек для Сна-Изнанки (GDD 02.21б). Меш строит PointCloudSurface:
// у каждой точки четыре вершины с одним центром, здесь они разворачиваются к камере
// в мягкий круглый огонёк.
//
// Слои вида (по референсу автора):
//   • поверхности — россыпь точек, рёбра — ярче (яркость приходит из меша);
//   • мерцание — каждая точка слегка «дышит» со своей фазой;
//   • сканлайны — по миру медленно ползут светлые горизонтальные полосы;
//   • нити — часть точек стекает вниз и гаснет, как капли света;
//   • даль — точки гаснут к черноте, мир растворяется в пустоте;
//   • волны (только у воды, _WaveHeight > 0) — рябь из шума, который сам меняется во
//     времени: волны возникают и пропадают, а не бегут ровными рядами. Крупный медленный
//     шум поверх задаёт высоту по участкам — где-то почти штиль, где-то волна выше.
//   • глитч — изредка участок облака «ломается»: полосы рвутся вбок, точки слипаются
//     в крупную сетку, часть гаснет или вспыхивает, кадры дёргаются. Время делится на
//     окна; в каждом окне объект с шансом получает один короткий сбой в случайный
//     момент. У каждого облака своё зерно (_GlitchSeed, ставит PointCloudSurface) —
//     объекты сбоят не вместе. Внутри объекта задевается только пятно из шума.
// Смешивание аддитивное: точки складываются в свечение, bloom делает остальное.
Shader "Radio/Dream/PointCloud"
{
    Properties
    {
        [HDR] _Color ("Цвет", Color) = (0.35, 1.1, 1.5, 1)
        _PointSize ("Размер точки, м", Float) = 0.022
        _MinPixelSize ("Минимальный размер, пикс", Float) = 1.5
        _Twinkle ("Сила мерцания", Range(0, 1)) = 0.45
        _TwinkleSpeed ("Скорость мерцания", Float) = 2.2
        _ScanStrength ("Яркость сканлайнов", Range(0, 2)) = 0.6
        _ScanSpacing ("Шаг сканлайнов, м", Float) = 2.5
        _ScanSpeed ("Скорость сканлайнов, м/с", Float) = 0.6
        _DripLength ("Длина нитей, м", Float) = 2.2
        _DripSpeed ("Скорость нитей", Float) = 0.25
        _FadeStart ("Гаснет с расстояния, м", Float) = 18
        _FadeEnd ("Гаснет полностью, м", Float) = 70
        _WaveHeight ("Волны: наибольшая высота, м (0 — без волн)", Float) = 0
        _WaveCalm ("Волны: высота в самом спокойном месте (доля)", Range(0, 1)) = 0.15
        _WaveLength ("Волны: размер ряби, м", Float) = 3.5
        _WaveSpeed ("Волны: скорость", Float) = 0.8
        _WaveGustSize ("Волны: размер участков разной высоты, м", Float) = 14
        _WaveGustSpeed ("Волны: как быстро меняются участки", Float) = 0.12
        _WaveGlow ("Волны: насколько светлее гребни", Range(0, 2)) = 0.8
        _GlitchChance ("Глитч: шанс сбоя в окне (0 — без глитча)", Range(0, 1)) = 0.6
        _GlitchInterval ("Глитч: окно, с", Float) = 4
        _GlitchDuration ("Глитч: длительность, с (±50%)", Float) = 1.2
        _GlitchStep ("Глитч: смена кадра сбоя, с", Float) = 0.14
        _GlitchShift ("Глитч: сдвиг полос вбок, м", Float) = 0.35
        _GlitchSliceHeight ("Глитч: высота полос, м", Float) = 0.2
        _GlitchSnap ("Глитч: шаг крупной сетки, м", Float) = 0.12
        _GlitchArea ("Глитч: размер задетого пятна, м", Float) = 1.5
        _GlitchCover ("Глитч: доля задетой поверхности", Range(0, 1)) = 0.3
        [HideInInspector] _GlitchSeed ("Зерно глитча (ставит PointCloudSurface)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _PointSize;
                float _MinPixelSize;
                float _Twinkle;
                float _TwinkleSpeed;
                float _ScanStrength;
                float _ScanSpacing;
                float _ScanSpeed;
                float _DripLength;
                float _DripSpeed;
                float _FadeStart;
                float _FadeEnd;
                float _WaveHeight;
                float _WaveCalm;
                float _WaveLength;
                float _WaveSpeed;
                float _WaveGustSize;
                float _WaveGustSpeed;
                float _WaveGlow;
                float _GlitchChance;
                float _GlitchInterval;
                float _GlitchDuration;
                float _GlitchStep;
                float _GlitchShift;
                float _GlitchSliceHeight;
                float _GlitchSnap;
                float _GlitchArea;
                float _GlitchCover;
                float _GlitchSeed;
            CBUFFER_END

            // Шум значений в трёх измерениях: плоскость воды и время. Время — третья ось,
            // поэтому волна не просто едет, а рождается и гаснет на месте.
            float Hash3(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float Noise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);

                float n000 = Hash3(i);
                float n100 = Hash3(i + float3(1, 0, 0));
                float n010 = Hash3(i + float3(0, 1, 0));
                float n110 = Hash3(i + float3(1, 1, 0));
                float n001 = Hash3(i + float3(0, 0, 1));
                float n101 = Hash3(i + float3(1, 0, 1));
                float n011 = Hash3(i + float3(0, 1, 1));
                float n111 = Hash3(i + float3(1, 1, 1));

                float x00 = lerp(n000, n100, u.x);
                float x10 = lerp(n010, n110, u.x);
                float x01 = lerp(n001, n101, u.x);
                float x11 = lerp(n011, n111, u.x);
                return lerp(lerp(x00, x10, u.y), lerp(x01, x11, u.y), u.z) * 2.0 - 1.0;
            }

            // Высота воды в точке: рябь из трёх слоёв шума разного размера, помноженная
            // на «силу ветра» участка — крупный и медленный шум.
            float WaveHeightAt(float2 xz)
            {
                float t = _Time.y * _WaveSpeed;
                float2 p = xz / max(_WaveLength, 0.01);

                float ripple = 0.55 * Noise3(float3(p, t * 0.6))
                             + 0.30 * Noise3(float3(p * 2.1 + 17.3, t * 1.1))
                             + 0.15 * Noise3(float3(p * 4.3 - 9.1, t * 1.7));

                float gust = Noise3(float3(xz / max(_WaveGustSize, 0.01), _Time.y * _WaveGustSpeed)) * 0.5 + 0.5;
                gust = lerp(_WaveCalm, 1.0, smoothstep(0.15, 0.85, gust));
                return ripple * gust;
            }

            // Идёт ли сейчас сбой в этой точке: 1 — да, 0 — нет. stepId — номер «кадра»
            // сбоя, по нему меняются случайные сдвиги, чтобы картинка дёргалась.
            float GlitchAt(float3 world, out float stepId)
            {
                stepId = floor(_Time.y / max(_GlitchStep, 0.01));

                if (_GlitchChance <= 0.0)
                {
                    return 0.0;
                }

                // Окна у объектов сдвинуты по зерну — границы окон тоже не совпадают.
                float window = max(_GlitchInterval, 0.1);
                float t = _Time.y / window + frac(_GlitchSeed * 0.618);
                float slot = floor(t);
                float local = frac(t) * window;

                float roll = Hash3(float3(slot, _GlitchSeed, 1.7));
                float duration = min(_GlitchDuration * (0.5 + Hash3(float3(slot, _GlitchSeed, 4.1))), window);
                float start = Hash3(float3(slot, _GlitchSeed, 9.3)) * (window - duration);
                float active = (roll < _GlitchChance && local >= start && local <= start + duration) ? 1.0 : 0.0;

                // Пятно: в каждом сбое задета своя часть объекта.
                float patch = Noise3(world / max(_GlitchArea, 0.1) + float3(slot * 3.7, _GlitchSeed, slot * 1.3)) * 0.5 + 0.5;
                return active * step(1.0 - _GlitchCover, patch);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 corner : TEXCOORD0;
                float4 data : TEXCOORD1; // x — яркость, y — нить, z — фаза, w — случайное
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 corner : TEXCOORD0;
                half intensity : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float phase = input.data.z;
                float rnd = input.data.w;
                half intensity = input.data.x;

                // Нить: точка съезжает вниз и гаснет, потом начинает заново.
                if (input.data.y > 0.5)
                {
                    float t = frac(_Time.y * _DripSpeed * (0.6 + rnd) + phase);
                    world.y -= t * _DripLength * (0.5 + rnd);
                    intensity *= (1.0 - t) * 1.3;
                }

                // Волны: высота из шума. Гребни светлее впадин, так движение видно издали.
                if (_WaveHeight > 0.0)
                {
                    float wave = WaveHeightAt(world.xz);
                    world.y += wave * _WaveHeight;
                    intensity *= max(0.0, 1.0 + wave * _WaveGlow);
                }

                // Глитч: по горизонтальным полосам — свой сдвиг, сетка, вспышка или провал.
                float stepId;
                float glitchShift = 0.0;

                if (GlitchAt(world, stepId) > 0.5)
                {
                    float band = floor(world.y / max(_GlitchSliceHeight, 0.01));
                    float r1 = Hash3(float3(band, stepId, _GlitchSeed));
                    float r2 = Hash3(float3(band, stepId, _GlitchSeed + 3.3));

                    if (r1 > 0.55)
                    {
                        glitchShift = ((r1 - 0.55) / 0.45 * 2.0 - 1.0) * _GlitchShift;
                    }

                    if (r2 < 0.35 && _GlitchSnap > 0.0)
                    {
                        world = (floor(world / _GlitchSnap) + 0.5) * _GlitchSnap;
                    }

                    intensity *= r2 > 0.85 ? 0.0 : (r2 > 0.6 ? 2.2 : 1.2);
                }

                // Мерцание: у каждой точки своя фаза.
                intensity *= 1.0 - _Twinkle * (0.5 + 0.5 * sin(_Time.y * _TwinkleSpeed * (0.5 + rnd) + phase * 6.2831));

                // Сканлайны: узкие светлые полосы ползут вверх по миру.
                float scan = frac(world.y / _ScanSpacing - _Time.y * _ScanSpeed / _ScanSpacing);
                intensity *= 1.0 + _ScanStrength * pow(saturate(1.0 - abs(scan - 0.5) * 12.0), 2.0);

                // Даль растворяется в черноте.
                float3 view = TransformWorldToView(world);
                view.x += glitchShift; // рвётся по экрану, а не по миру
                float distance = -view.z;
                intensity *= 1.0 - smoothstep(_FadeStart, _FadeEnd, distance);

                // Размер в мире, но не меньше пары пикселей — иначе далёкие точки мерцают в ноль.
                float pixelWorld = distance * 2.0 / (_ScreenParams.y * UNITY_MATRIX_P._m11);
                float size = max(_PointSize, _MinPixelSize * pixelWorld);
                view.xy += input.corner * size;

                output.positionCS = TransformWViewToHClip(view);
                output.corner = input.corner;
                output.intensity = intensity;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Мягкий круглый огонёк: яркое ядро и спадающий ореол.
                float d = length(input.corner);
                half glow = saturate(1.0 - d);
                glow = glow * glow;
                return half4(_Color.rgb * glow * input.intensity, 0);
            }
            ENDHLSL
        }
    }
}
