// Обводка интерактивного объекта. Рисуется как «оболочка»: раздутая копия меша,
// у которой видны только изнаночные грани — снаружи от силуэта они и образуют кайму.
// Материал добавляется вторым в массив рендерера и убирается, когда подсветка не нужна.
Shader "Radio/InteractionOutline"
{
    Properties
    {
        [HDR] _OutlineColor("Цвет обводки", Color) = (1, 1, 1, 1)
        _OutlineWidth("Толщина, м (режимы Position и Normal)", Range(0.001, 0.1)) = 0.015
        _OutlineWidthPixels("Толщина, пикселей (режим Screen)", Range(0.5, 8)) = 1.5
        [KeywordEnum(Position, Normal, Screen)] _Extrude("Режим выдавливания", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            // Geometry+1: рисуемся после всей непрозрачной геометрии, когда глубина уже записана.
            "Queue" = "Geometry+1"
        }

        Pass
        {
            Name "InteractionOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            // Изнанка оболочки: там, где она перекрыта самим объектом, тест глубины её отсечёт,
            // и останется только кайма по краю силуэта.
            Cull Front
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _EXTRUDE_POSITION _EXTRUDE_NORMAL _EXTRUDE_SCREEN
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Все свойства обязаны лежать в этом буфере, иначе материал выпадет из SRP Batcher.
            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineWidthPixels;
                float _Extrude;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                #ifdef _EXTRUDE_SCREEN
                    // Раздуваем силуэт прямо в пикселях экрана. Два других режима задают
                    // толщину в метрах, и кайма растёт при приближении: наклонившись к столу,
                    // игрок получил бы вместо тонкого контура широкую рамку. Здесь толщина
                    // постоянная, поэтому её можно выставить в полтора пикселя и не думать
                    // ни о расстоянии, ни о разрешении.
                    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                    float4 positionCS = TransformWorldToHClip(positionWS);
                    float3 normalCS = mul((float3x3)UNITY_MATRIX_VP, normalWS);

                    // Нормализуем в пикселях, а не в отсечённых координатах: у них разный
                    // масштаб по осям, и иначе кайма вышла бы толще по вертикали.
                    float2 directionPixels = float2(normalCS.x * _ScreenParams.x,
                                                    normalCS.y * _ScreenParams.y);
                    float lengthSquared = dot(directionPixels, directionPixels);

                    // Нормаль строго на камеру проецируется в точку: такую вершину не двигаем,
                    // она внутри силуэта и каймы не образует.
                    if (lengthSquared > 1e-8)
                    {
                        directionPixels *= rsqrt(lengthSquared) * _OutlineWidthPixels;

                        // Сдвиг в отсечённых координатах: умножение на w нужно, чтобы после
                        // деления на него смещение осталось ровно заданным в пикселях.
                        positionCS.xy += directionPixels * (2.0 / _ScreenParams.xy) * positionCS.w;
                    }

                    output.positionCS = positionCS;
                    return output;
                #endif

                #ifdef _EXTRUDE_NORMAL
                    // Для мешей со сглаженными нормалями — обычное выдавливание по нормали.
                    float3 directionWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                #else
                    // Для блокаута из примитивов. У куба 24 вершины: нормали разбиты по граням,
                    // и выдавливание по ним рвёт кайму на рёбрах. Позиции же у совпадающих
                    // угловых вершин одинаковые, поэтому смещение от центра двигает их синхронно.
                    float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                    float3 delta = positionWS - centerWS;
                    float lengthSquared = dot(delta, delta);
                    float3 directionWS = lengthSquared > 1e-8 ? delta * rsqrt(lengthSquared) : float3(0, 1, 0);
                #endif

                // Смещаем в мировом пространстве: иначе неравномерный масштаб объекта
                // сделает кайму разной толщины с разных сторон, а у блокаута он неравномерный везде.
                positionWS += directionWS * _OutlineWidth;

                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
